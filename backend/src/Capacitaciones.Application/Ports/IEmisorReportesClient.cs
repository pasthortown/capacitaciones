using Capacitaciones.Application.Dtos.Encuesta;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto del servicio externo <c>emisor_reportes</c> (Python + matplotlib + reportlab)
/// que genera el PDF del dashboard de resultados de encuesta.
/// </summary>
public interface IEmisorReportesClient
{
    /// <summary>
    /// Envía el payload con los datos agregados y devuelve la ruta del PDF emitido
    /// dentro del volumen compartido <c>/output</c>.
    /// </summary>
    Task<string> EmitirReporteEncuestaAsync(
        ResultadoEncuestaDto payload,
        CancellationToken ct = default);
}
