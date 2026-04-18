using Capacitaciones.Application.Dtos.Calificaciones;
using Capacitaciones.Application.UseCases.Calificaciones;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Application.UseCases.PaseLista;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Endpoints consumidos por la pantalla pública del capacitador para el registro de
/// calificaciones (Fase 11). Todas las rutas leen la capacitación implícita desde el claim
/// <c>cid</c> emitido por <see cref="Application.Ports.IJwtTokenGenerator.GenerateCalificacionesToken"/>.
///
/// El capacitador NO puede pasar el id por URL: solo opera sobre la capacitación que el admin
/// autorizó al emitirle el link — mismo patrón que <see cref="PaseListaController"/>.
/// </summary>
[ApiController]
[Authorize(Policy = "Calificaciones")]
[Route("api/capacitador/calificaciones")]
public class CalificacionesController : ControllerBase
{
    private readonly ObtenerCalificacionesUseCase _obtener;
    private readonly CalificarAsistenteUseCase _calificar;

    public CalificacionesController(
        ObtenerCalificacionesUseCase obtener,
        CalificarAsistenteUseCase calificar)
    {
        _obtener = obtener;
        _calificar = calificar;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!TryGetCapacitacionId(out var capacitacionId))
        {
            return Unauthorized();
        }

        try
        {
            var dto = await _obtener.ExecuteAsync(capacitacionId, ct);
            return Ok(dto);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitadorForbiddenException ex)
        {
            return Forbidden(ex.Message);
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    [HttpPut("asistentes/{asistenteId:guid}")]
    public async Task<IActionResult> Calificar(
        Guid asistenteId,
        [FromBody] CalificarAsistenteDto? input,
        CancellationToken ct)
    {
        if (!TryGetCapacitacionId(out var capacitacionId))
        {
            return Unauthorized();
        }

        try
        {
            var dto = await _calificar.ExecuteAsync(capacitacionId, asistenteId, input?.Calificacion, ct);
            return Ok(dto);
        }
        catch (AsistenteNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitadorForbiddenException ex)
        {
            return Forbidden(ex.Message);
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Extrae la capacitación del claim <c>cid</c> del token. Si falta o no parsea como Guid,
    /// devuelve <c>false</c> para que el caller responda 401.
    /// </summary>
    private bool TryGetCapacitacionId(out Guid id)
    {
        id = Guid.Empty;
        var raw = User.FindFirst("cid")?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return Guid.TryParse(raw, out id);
    }

    private ObjectResult Forbidden(string message)
    {
        var problem = new
        {
            error = "CALIFICACIONES_FORBIDDEN",
            message
        };
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
    }

    private static ObjectResult ToProblem(CapacitacionServiceException ex)
    {
        var status = ex.Codigo switch
        {
            "CALIFICACIONES_NO_APLICA" => StatusCodes.Status409Conflict,
            "ASISTENTE_NO_PRESENTE" => StatusCodes.Status409Conflict,
            "CALIFICACION_FUERA_DE_RANGO" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };
        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
