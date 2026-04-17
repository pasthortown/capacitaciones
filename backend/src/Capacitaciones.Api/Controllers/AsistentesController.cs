using Capacitaciones.Application.UseCases.Asistentes;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Admin — listado de asistentes de una capacitación y descarga de certificado (stub Fase 5).
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/capacitaciones/{capacitacionId:guid}/asistentes")]
public class AsistentesController : ControllerBase
{
    private readonly ListarAsistentesUseCase _listar;
    private readonly DescargarCertificadoUseCase _descargarCertificado;

    public AsistentesController(
        ListarAsistentesUseCase listar,
        DescargarCertificadoUseCase descargarCertificado)
    {
        _listar = listar;
        _descargarCertificado = descargarCertificado;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid capacitacionId, CancellationToken ct)
    {
        try
        {
            var items = await _listar.ExecuteAsync(capacitacionId, ct);
            return Ok(items);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Stub Fase 5: valida estado y devuelve 409 o 501 según corresponda. La integración
    /// con <c>emisor_documentos</c> se implementa en Fase 6.
    /// </summary>
    [HttpGet("{asistenteId:guid}/certificado")]
    public async Task<IActionResult> DescargarCertificado(
        Guid capacitacionId,
        Guid asistenteId,
        CancellationToken ct)
    {
        try
        {
            await _descargarCertificado.ExecuteAsync(capacitacionId, asistenteId, ct);
            // Si la implementación cambiara y no lanza excepción, es un caso no previsto en Fase 5.
            return StatusCode(StatusCodes.Status501NotImplemented, new
            {
                error = "CERTIFICADO_NO_DISPONIBLE",
                message = "Pendiente integración con emisor_documentos (Fase 6)."
            });
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CertificadoNoDisponibleException ex)
        {
            return new ObjectResult(new { error = ex.Codigo, message = ex.Message })
            {
                StatusCode = StatusCodes.Status501NotImplemented
            };
        }
        catch (CapacitacionServiceException ex)
        {
            var status = ex.Codigo switch
            {
                "CAPACITACION_NO_FINALIZADA" => StatusCodes.Status409Conflict,
                "ASISTENTE_NOT_FOUND" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest
            };
            return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
        }
    }
}
