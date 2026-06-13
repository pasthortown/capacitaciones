using System.Globalization;
using Capacitaciones.Application.Common;
using Capacitaciones.Application.Dtos.Certificados;
using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Asistentes;
using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.Certificados;

/// <summary>
/// Caso de uso "Generar y Enviar todos los certificados": ejecuta el flujo
/// Fase 6 (lote) y, para cada asistente que recibió certificado válido,
/// le envía el PDF como adjunto vía <c>mail_sender</c> con la plantilla
/// <c>certificado_participante</c>.
///
/// Estrategia:
///  1. Delega la generación a <see cref="GenerarCertificadosCapacitacionUseCase"/>.
///  2. Reabre la lista de asistentes para conocer email/identificación/etc.
///  3. Filtra los que ya quedaron como NoElegibles o con error en la fase 1.
///  4. Para cada asistente válido, lee el PDF de <c>OutputDir</c>, lo embebe
///     en base64 y dispara el correo.
///  5. Procesa en bloques de <see cref="BatchSize"/> con un <c>Task.Delay</c>
///     entre bloques para no saturar el SMTP.
/// </summary>
public class GenerarYEnviarCertificadosUseCase
{
    private const int BatchSize = 5;
    private const int DelayBetweenBatchesMs = 2000;

    private readonly GenerarCertificadosCapacitacionUseCase _generar;
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;
    private readonly IMailSenderClient _mail;
    private readonly CertificadosOptions _options;

    public GenerarYEnviarCertificadosUseCase(
        GenerarCertificadosCapacitacionUseCase generar,
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

    public async Task<GeneracionEnvioLoteResultadoDto> ExecuteAsync(
        Guid capacitacionId,
        CancellationToken ct = default)
    {
        // ----- Fase 1: Generación (delegada al use case existente) -----
        var gen = await _generar.ExecuteAsync(capacitacionId, ct);

        var resultado = new GeneracionEnvioLoteResultadoDto
        {
            Total = gen.Total,
            Emitidos = gen.Emitidos,
            NoElegibles = gen.NoElegibles,
            NoElegiblesDetalle = gen.NoElegiblesDetalle,
            Errores = gen.Errores
        };

        // ----- Fase 2: Envío -----
        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        var asistentes = await _asistentes.ListByCapacitacionAsync(capacitacionId, ct);

        var noElegiblesIds = gen.NoElegiblesDetalle.Select(n => n.AsistenteId).ToHashSet();
        var fallidosIds = gen.Errores.Select(e => e.AsistenteId).ToHashSet();

        var elegibles = asistentes
            .Where(a => !noElegiblesIds.Contains(a.Id) && !fallidosIds.Contains(a.Id))
            .ToList();

        if (elegibles.Count == 0)
        {
            return resultado;
        }

        var outputDir = string.IsNullOrWhiteSpace(_options.OutputDir) ? "/output" : _options.OutputDir;

        // Procesamos en bloques para no saturar el SMTP. Dentro de cada bloque
        // los envíos van en serie (mail_sender no es thread-safe respecto a SMTP).
        for (var i = 0; i < elegibles.Count; i += BatchSize)
        {
            var batch = elegibles.Skip(i).Take(BatchSize).ToList();

            foreach (var asistente in batch)
            {
                ct.ThrowIfCancellationRequested();
                await EnviarUnoAsync(capacitacion, asistente, outputDir, resultado, ct);
            }

            // Pequeña pausa entre bloques. La saltamos en el último para no
            // hacer esperar al admin innecesariamente.
            if (i + BatchSize < elegibles.Count)
            {
                await Task.Delay(DelayBetweenBatchesMs, ct);
            }
        }

        return resultado;
    }

    private async Task EnviarUnoAsync(
        Domain.Entities.Capacitacion capacitacion,
        Domain.Entities.Asistente asistente,
        string outputDir,
        GeneracionEnvioLoteResultadoDto resultado,
        CancellationToken ct)
    {
        var email = (asistente.EmailUsuario ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            resultado.ErroresEnvio.Add(new EnvioCertificadoErrorDto
            {
                AsistenteId = asistente.Id,
                Email = string.Empty,
                Mensaje = "El asistente no tiene email registrado."
            });
            return;
        }

        var filename = DescargarCertificadoUseCase.BuildFilename(capacitacion.Codigo, asistente.Identificacion);
        var fullPath = Path.Combine(outputDir, filename);

        if (!File.Exists(fullPath))
        {
            resultado.ErroresEnvio.Add(new EnvioCertificadoErrorDto
            {
                AsistenteId = asistente.Id,
                Email = email,
                Mensaje = $"El PDF '{filename}' no existe en el volumen tras la emisión."
            });
            return;
        }

        try
        {
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

            await _mail.SendMailAsync(request, ct);
            resultado.Enviados++;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            resultado.ErroresEnvio.Add(new EnvioCertificadoErrorDto
            {
                AsistenteId = asistente.Id,
                Email = email,
                Mensaje = ex.Message
            });
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
