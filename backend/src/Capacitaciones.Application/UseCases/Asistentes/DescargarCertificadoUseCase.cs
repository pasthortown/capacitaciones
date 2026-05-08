using Capacitaciones.Application.Dtos.Certificados;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Certificados;

namespace Capacitaciones.Application.UseCases.Asistentes;

/// <summary>
/// Fase 6 — descarga del certificado PDF de un asistente.
///
/// Algoritmo:
///   1. Valida que la capacitación exista y que el asistente le pertenezca (404 si no).
///   2. Construye el nombre esperado del archivo: <c>{codigo}_{identificacion}.pdf</c>
///      (sanitizados — solo alfanumérico + <c>-</c> + <c>_</c>).
///   3. Si <c>{OutputDir}/{filename}</c> existe → abre el stream y lo devuelve (descarga directa).
///   4. Si NO existe → invoca <see cref="GenerarCertificadoAsistenteUseCase"/> para producirlo
///      contra el servicio emisor y luego abre el stream.
///   5. Si tras generar sigue sin existir el archivo → <see cref="InvalidOperationException"/>
///      (falla de contrato con el emisor o volumen no montado).
///
/// <para>
/// El volumen compartido entre backend y emisor es <c>/output/</c> (mapeado en docker-compose
/// a <c>./output/</c> del host). Si el volumen no está montado, la lectura del archivo lanzará
/// <see cref="FileNotFoundException"/> tras la generación y se convertirá en
/// <see cref="InvalidOperationException"/> (500) — es responsabilidad de Infra garantizar el mount.
/// </para>
/// </summary>
public class DescargarCertificadoUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;
    private readonly GenerarCertificadoAsistenteUseCase _generar;
    private readonly CertificadosOptions _options;

    public DescargarCertificadoUseCase(
        ICapacitacionRepository capacitaciones,
        IAsistenteRepository asistentes,
        GenerarCertificadoAsistenteUseCase generar,
        CertificadosOptions options)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
        _generar = generar;
        _options = options;
    }

    public async Task<CertificadoDescargaDto> ExecuteAsync(
        Guid capacitacionId,
        Guid asistenteId,
        CancellationToken ct = default)
    {
        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        // Si el evento no emite certificados, bloqueamos la descarga aunque el PDF
        // exista físicamente (puede haber quedado de una emisión previa al cambio de flag).
        if (!capacitacion.EmiteCertificado)
        {
            throw CertificadoNoDisponibleException.CapacitacionNoEmiteCertificado();
        }

        var asistente = await _asistentes.GetByIdAsync(asistenteId, ct);
        if (asistente is null || asistente.CapacitacionId != capacitacionId)
        {
            throw new CapacitacionServiceException(
                "ASISTENTE_NOT_FOUND",
                $"No existe un asistente con Id={asistenteId} para la capacitación {capacitacionId}.");
        }

        var filename = BuildFilename(capacitacion.Codigo, asistente.Identificacion);
        var outputDir = string.IsNullOrWhiteSpace(_options.OutputDir) ? "/output" : _options.OutputDir;
        var fullPath = Path.Combine(outputDir, filename);

        if (!File.Exists(fullPath))
        {
            // Primera descarga (o archivo borrado): disparamos la emisión y confiamos en que
            // el emisor escribe en el volumen compartido antes de responder 201.
            await _generar.ExecuteAsync(capacitacionId, asistenteId, ct);
        }

        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"El certificado no fue encontrado en '{fullPath}' tras la emisión. " +
                "Revisa que el volumen '/output' esté montado en el contenedor backend y que " +
                "el emisor haya respondido 201 correctamente.");
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new CertificadoDescargaDto(stream, filename);
    }

    /// <summary>
    /// Construye <c>{codigo}_{identificacion}.pdf</c> con ambos segmentos sanitizados:
    /// cualquier carácter que no sea alfanumérico, <c>-</c> o <c>_</c> se descarta.
    /// Evita path traversal y caracteres problemáticos para filesystems heterogéneos.
    /// </summary>
    public static string BuildFilename(string codigo, string identificacion)
    {
        var cSan = Sanitize(codigo);
        var iSan = Sanitize(identificacion);
        return $"{cSan}_{iSan}.pdf";
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var buf = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            {
                buf.Append(ch);
            }
        }
        return buf.ToString();
    }
}

/// <summary>
/// Opciones bindeadas desde la sección <c>Certificados</c> de <c>appsettings</c>.
/// Controla el directorio de salida donde el emisor deposita los PDFs (volumen compartido).
/// </summary>
public class CertificadosOptions
{
    public const string SectionName = "Certificados";

    /// <summary>Directorio montado en el contenedor backend donde residen los PDFs emitidos.</summary>
    public string OutputDir { get; set; } = "/output";
}
