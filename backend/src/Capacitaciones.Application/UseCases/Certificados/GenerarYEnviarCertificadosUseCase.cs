using System.Globalization;
using Capacitaciones.Application.Common;
using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Asistentes;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Certificados;

/// <summary>
/// Caso de uso "Generar y Enviar todos los certificados", rediseñado para ejecutarse en
/// segundo plano con control de estado por asistente.
///
/// El flujo se parte en dos fases desacopladas del request HTTP:
///  1. <see cref="IniciarEnvioAsync"/> (síncrono, en el request): valida la capacitación,
///     marca a los asistentes elegibles (Presentes) como <see cref="EstadoEnvioCertificado.Pendiente"/>
///     y a los no elegibles los deja en <c>null</c>. Devuelve la cantidad de pendientes.
///  2. <see cref="ProcesarUnoAsync"/> (lo invoca el <c>BackgroundService</c> por cada pendiente):
///     genera el PDF y envía el correo con reintentos. En éxito marca <c>Enviado</c>; si agota
///     los reintentos marca <c>Error</c> con el detalle. Nunca deja al asistente en <c>Pendiente</c>.
///
/// <see cref="ReintentarErroresAsync"/> reabre los que quedaron en <c>Error</c> (los vuelve a
/// <c>Pendiente</c>) para una nueva pasada del worker.
/// </summary>
public class GenerarYEnviarCertificadosUseCase
{
    // Reintento acotado por asistente: hasta 4 intentos con backoff 2s / 5s / 15s entre ellos.
    // Si tras eso sigue fallando, el asistente queda en Error y se resuelve con "reintentar erróneos".
    private static readonly int[] BackoffMs = { 2000, 5000, 15000 };
    private const int MaxIntentos = 4;

    private readonly GenerarCertificadoAsistenteUseCase _generar;
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;
    private readonly IMailSenderClient _mail;
    private readonly CertificadosOptions _options;

    public GenerarYEnviarCertificadosUseCase(
        GenerarCertificadoAsistenteUseCase generar,
        ICapacitacionRepository capacitaciones,
        IAsistenteRepository asistentes,
        IMailSenderClient mail,
        CertificadosOptions options)
    {
        _generar = generar;
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
        _mail = mail;
        _options = options;
    }

    /// <summary>
    /// Fase 1 (síncrona): valida y marca a los elegibles como <c>Pendiente</c>. Devuelve cuántos
    /// quedaron pendientes. Lanza 404/409 igual que antes para que el UI muestre el error inmediato.
    /// </summary>
    public async Task<int> IniciarEnvioAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        if (CapacitacionEstadoCalculator.Calcular(capacitacion) != CapacitacionEstadoCalculator.Finalizada)
        {
            throw CertificadoNoDisponibleException.CapacitacionNoFinalizada();
        }

        if (!capacitacion.EmiteCertificado)
        {
            throw CertificadoNoDisponibleException.CapacitacionNoEmiteCertificado();
        }

        var asistentes = await _asistentes.ListByCapacitacionAsync(capacitacionId, ct);

        // Elegible = Presente (Fase 12: ausente / sin marcar no recibe certificado).
        var elegibles = asistentes
            .Where(a => a.EstadoAsistencia == EstadoAsistencia.Presente)
            .Select(a => a.Id)
            .ToHashSet();

        return await _asistentes.MarcarEstadoEnvioElegiblesAsync(capacitacionId, elegibles, ct);
    }

    /// <summary>
    /// Reabre los asistentes en estado <c>Error</c> (los vuelve a <c>Pendiente</c>) para reintentar.
    /// Valida la capacitación igual que el inicio. Devuelve cuántos se reabrieron.
    /// </summary>
    public async Task<int> ReintentarErroresAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        if (CapacitacionEstadoCalculator.Calcular(capacitacion) != CapacitacionEstadoCalculator.Finalizada)
        {
            throw CertificadoNoDisponibleException.CapacitacionNoFinalizada();
        }

        if (!capacitacion.EmiteCertificado)
        {
            throw CertificadoNoDisponibleException.CapacitacionNoEmiteCertificado();
        }

        return await _asistentes.MarcarErroresComoPendientesAsync(capacitacionId, ct);
    }

    /// <summary>
    /// Fase 2 (segundo plano): procesa UN asistente pendiente. Genera el PDF y envía el correo,
    /// cada paso con reintentos. Marca <c>Enviado</c> o <c>Error</c>. No relanza excepciones:
    /// cualquier fallo terminal queda persistido como <c>Error</c> para no dejar el asistente colgado.
    /// </summary>
    public async Task ProcesarUnoAsync(Guid capacitacionId, Guid asistenteId, CancellationToken ct = default)
    {
        try
        {
            // 1. Generación del PDF (emisor_documentos) con reintentos.
            var emitido = await ReintentarAsync(
                () => _generar.ExecuteAsync(capacitacionId, asistenteId, ct),
                ct);

            // 2. Datos para el correo.
            var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
                ?? throw new CapacitacionNotFoundException(capacitacionId);
            var asistente = await _asistentes.GetByIdAsync(asistenteId, ct)
                ?? throw new CapacitacionServiceException(
                    "ASISTENTE_NOT_FOUND",
                    $"No existe un asistente con Id={asistenteId} para la capacitación {capacitacionId}.");

            var email = (asistente.EmailUsuario ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                await MarcarErrorAsync(asistenteId, "El asistente no tiene email registrado.", ct);
                return;
            }

            var outputDir = string.IsNullOrWhiteSpace(_options.OutputDir) ? "/output" : _options.OutputDir;
            var filename = !string.IsNullOrWhiteSpace(emitido.Filename)
                ? emitido.Filename
                : DescargarCertificadoUseCase.BuildFilename(capacitacion.Codigo, asistente.Identificacion);
            var fullPath = Path.Combine(outputDir, filename);

            if (!File.Exists(fullPath))
            {
                await MarcarErrorAsync(asistenteId, $"El PDF '{filename}' no existe en el volumen tras la emisión.", ct);
                return;
            }

            var pdfBytes = await File.ReadAllBytesAsync(fullPath, ct);
            var pdfBase64 = Convert.ToBase64String(pdfBytes);

            var nombre = $"{(asistente.Nombres ?? string.Empty).Trim()} {(asistente.Apellidos ?? string.Empty).Trim()}".Trim();
            var tipoEfectivo = GenerarCertificadoAsistenteUseCase.CalcularCertificadoEfectivo(capacitacion, asistente);
            var tipoLegible = LegibleTipoCertificado(tipoEfectivo);

            var fechaLocal = EcuadorTime.FromUtc(capacitacion.FechaHoraInicio);
            var cultura = new CultureInfo("es-EC");
            var fechaTexto = fechaLocal.ToString("dd 'de' MMMM 'de' yyyy", cultura);

            var subject = $"Tu certificado: {capacitacion.Tema}";

            var request = new SendMailRequest
            {
                Template = "certificado_participante",
                Subject = subject,
                Recipients = new List<string> { email },
                Parameters = new Dictionary<string, object?>
                {
                    ["subject"] = subject,
                    ["nombre"] = nombre,
                    ["tema"] = capacitacion.Tema,
                    ["fecha"] = fechaTexto,
                    ["tipoCertificado"] = tipoLegible
                },
                Attachment = new MailAttachment
                {
                    Filename = filename,
                    ContentBase64 = pdfBase64,
                    MimeType = "application/pdf"
                }
            };

            // 3. Envío del correo (mail_sender → O365) con reintentos.
            await ReintentarAsync(async () =>
            {
                await _mail.SendMailAsync(request, ct);
                return true;
            }, ct);

            await _asistentes.ActualizarResultadoEnvioAsync(
                asistenteId, EstadoEnvioCertificado.Enviado, DateTime.UtcNow, null, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Apagado del host: dejamos el asistente en Pendiente para retomarlo luego. No marcamos error.
            throw;
        }
        catch (CertificadoAsistenteNoElegibleException ex)
        {
            // No debería ocurrir (solo marcamos Presentes), pero si pasa lo dejamos registrado.
            await MarcarErrorAsync(asistenteId, ex.Message, ct);
        }
        catch (Exception ex)
        {
            await MarcarErrorAsync(asistenteId, ex.Message, ct);
        }
    }

    private async Task MarcarErrorAsync(Guid asistenteId, string mensaje, CancellationToken ct)
    {
        // Recorta el mensaje al límite de la columna (1000) para no romper el SaveChanges.
        var msg = mensaje.Length > 1000 ? mensaje[..1000] : mensaje;
        await _asistentes.ActualizarResultadoEnvioAsync(
            asistenteId, EstadoEnvioCertificado.Error, null, msg, CancellationToken.None);
    }

    /// <summary>
    /// Ejecuta <paramref name="accion"/> con reintentos acotados y backoff. Relanza la última
    /// excepción si agota los intentos. Respeta la cancelación del host (no la reintenta).
    /// </summary>
    private static async Task<T> ReintentarAsync<T>(Func<Task<T>> accion, CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                return await accion();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch when (intento < MaxIntentos)
            {
                var esperaMs = BackoffMs[Math.Min(intento - 1, BackoffMs.Length - 1)];
                await Task.Delay(esperaMs, ct);
            }
        }
    }

    private static string LegibleTipoCertificado(string tipoEfectivo) => tipoEfectivo switch
    {
        "Aprobacion" => "Aprobación",
        "Participacion" => "Participación",
        "Asistencia" => "Asistencia",
        _ => tipoEfectivo
    };
}
