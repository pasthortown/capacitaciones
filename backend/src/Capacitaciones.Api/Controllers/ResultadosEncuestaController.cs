using Capacitaciones.Application.UseCases.Encuesta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Admin — dashboard y descarga del PDF con los resultados de la encuesta
/// de satisfacción de una capacitación.
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/capacitaciones/{capacitacionId:guid}/encuesta")]
public class ResultadosEncuestaController : ControllerBase
{
    private readonly ObtenerResultadosEncuestaUseCase _obtener;
    private readonly DescargarReporteEncuestaUseCase _descargar;

    public ResultadosEncuestaController(
        ObtenerResultadosEncuestaUseCase obtener,
        DescargarReporteEncuestaUseCase descargar)
    {
        _obtener = obtener;
        _descargar = descargar;
    }

    /// <summary>
    /// Devuelve los datos agregados que alimentan el dashboard del admin.
    /// </summary>
    [HttpGet("resultados")]
    public async Task<IActionResult> GetResultados(Guid capacitacionId, CancellationToken ct)
    {
        try
        {
            var dto = await _obtener.ExecuteAsync(capacitacionId, ct);
            return Ok(dto);
        }
        catch (EncuestaServiceException ex) when (ex.Codigo == "CAPACITACION_NOT_FOUND")
        {
            return NotFound(new { error = ex.Codigo, message = ex.Message });
        }
    }

    /// <summary>
    /// Emite (o regenera) el PDF del dashboard vía servicio externo emisor_reportes
    /// y devuelve el archivo al cliente.
    /// </summary>
    [HttpGet("reporte")]
    public async Task<IActionResult> DescargarReporte(Guid capacitacionId, CancellationToken ct)
    {
        try
        {
            var descarga = await _descargar.ExecuteAsync(capacitacionId, ct);
            return File(descarga.FileStream, descarga.ContentType, descarga.Filename);
        }
        catch (EncuestaServiceException ex) when (ex.Codigo == "CAPACITACION_NOT_FOUND")
        {
            return NotFound(new { error = ex.Codigo, message = ex.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "SERVICIO_REPORTES_NO_DISPONIBLE",
                      message = "El servicio de generación de reportes no respondió. Intenta en unos minutos." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(
                StatusCodes.Status504GatewayTimeout,
                new { error = "SERVICIO_REPORTES_TIMEOUT",
                      message = "El generador de reportes tardó demasiado. Intenta nuevamente." });
        }
    }
}
