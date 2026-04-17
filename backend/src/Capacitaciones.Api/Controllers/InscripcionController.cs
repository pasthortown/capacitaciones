using Capacitaciones.Application.Dtos.Inscripcion;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Inscripcion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Endpoints consumidos por la página pública de inscripción (Fase 5), autenticada
/// vía JWT firmado con claims <c>role=Inscripcion</c> y <c>cid=&lt;capacitacionId&gt;</c>.
///
/// Igual que el controlador del capacitador, el id de la capacitación se lee del claim
/// <c>cid</c> — no se acepta como parámetro de URL.
/// </summary>
[ApiController]
[Authorize(Policy = "Inscripcion")]
[Route("api/inscripcion/capacitacion")]
public class InscripcionController : ControllerBase
{
    // Mismo margen que los demás controladores: la firma base64 del asistente puede ocupar MB.
    private const int MaxRequestBodyBytes = 10_000_000;

    private readonly ObtenerInscripcionPublicaUseCase _obtener;
    private readonly InscribirAsistenteUseCase _inscribir;

    public InscripcionController(
        ObtenerInscripcionPublicaUseCase obtener,
        InscribirAsistenteUseCase inscribir)
    {
        _obtener = obtener;
        _inscribir = inscribir;
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
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    [HttpPost]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<IActionResult> Inscribir(
        [FromBody] CreateInscripcionDto input,
        CancellationToken ct)
    {
        if (!TryGetCapacitacionId(out var capacitacionId))
        {
            return Unauthorized();
        }

        try
        {
            var dto = await _inscribir.ExecuteAsync(capacitacionId, input, ct);
            return StatusCode(StatusCodes.Status201Created, dto);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Extrae el id de capacitación del claim <c>cid</c>. Si falta o no parsea → 401.
    /// Mismo helper que en <c>CapacitadorController</c>.
    /// </summary>
    private bool TryGetCapacitacionId(out Guid id)
    {
        id = Guid.Empty;
        var raw = User.FindFirst("cid")?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return Guid.TryParse(raw, out id);
    }

    private static ObjectResult ToProblem(CapacitacionServiceException ex)
    {
        var status = ex.Codigo switch
        {
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "INSCRIPCION_CERRADA" => StatusCodes.Status409Conflict,
            "INSCRIPCION_DUPLICADA" => StatusCodes.Status409Conflict,
            "CAPACITACION_INACTIVA" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
