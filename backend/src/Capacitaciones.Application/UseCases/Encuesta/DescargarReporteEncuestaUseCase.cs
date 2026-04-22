using Capacitaciones.Application.Dtos.Certificados;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Asistentes;

namespace Capacitaciones.Application.UseCases.Encuesta;

/// <summary>
/// Admin — arma el payload con datos agregados y llama al servicio externo
/// <c>emisor_reportes</c> (Python) para generar el PDF. Devuelve el stream
/// abierto del archivo resultante dentro del volumen compartido <c>/output</c>.
/// </summary>
public class DescargarReporteEncuestaUseCase
{
    private readonly ObtenerResultadosEncuestaUseCase _obtener;
    private readonly IEmisorReportesClient _emisor;
    private readonly CertificadosOptions _options;

    public DescargarReporteEncuestaUseCase(
        ObtenerResultadosEncuestaUseCase obtener,
        IEmisorReportesClient emisor,
        CertificadosOptions options)
    {
        _obtener = obtener;
        _emisor = emisor;
        _options = options;
    }

    public async Task<CertificadoDescargaDto> ExecuteAsync(
        Guid capacitacionId,
        CancellationToken ct)
    {
        var payload = await _obtener.ExecuteAsync(capacitacionId, ct);
        var relativePath = await _emisor.EmitirReporteEncuestaAsync(payload, ct);

        var outputDir = string.IsNullOrWhiteSpace(_options.OutputDir) ? "/output" : _options.OutputDir;
        // relativePath viene como "/output/<archivo>.pdf" o "<archivo>.pdf" — normalizamos.
        var fileName = Path.GetFileName(relativePath);
        var fullPath = Path.Combine(outputDir, fileName);

        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"El reporte de encuesta no fue encontrado en '{fullPath}' tras la emisión. " +
                "Revisa que el volumen '/output' esté montado y que el servicio emisor_reportes haya respondido OK.");
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new CertificadoDescargaDto(stream, fileName);
    }
}
