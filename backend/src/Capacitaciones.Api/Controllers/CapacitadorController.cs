using Capacitaciones.Application.Dtos.Capacitador;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Endpoints consumidos por la página pública del capacitador (Fase 4) autenticada
/// vía JWT firmado con claims <c>role=Capacitador</c> y <c>cid=&lt;capacitacionId&gt;</c>.
///
/// Todas las rutas leen la capacitación implícita desde el claim <c>cid</c>:
/// el capacitador NO puede pasar el id por URL — solo opera sobre la capacitación
/// que el admin autorizó al emitirle el link.
/// </summary>
[ApiController]
[Authorize(Policy = "Capacitador")]
[Route("api/capacitador/capacitacion")]
public class CapacitadorController : ControllerBase
{
    // Mismo límite que CapacitacionesController: las firmas base64 pueden ser grandes.
    private const int MaxRequestBodyBytes = 10_000_000;

    private readonly ObtenerCapacitacionCapacitadorUseCase _obtener;
    private readonly ActualizarCapacitadorCapacitacionUseCase _actualizar;

    public CapacitadorController(
        ObtenerCapacitacionCapacitadorUseCase obtener,
        ActualizarCapacitadorCapacitacionUseCase actualizar)
    {
        _obtener = obtener;
        _actualizar = actualizar;
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
    }

    [HttpPut]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateCapacitadorCapacitacionDto input,
        CancellationToken ct)
    {
        if (!TryGetCapacitacionId(out var capacitacionId))
        {
            return Unauthorized();
        }

        try
        {
            var dto = await _actualizar.ExecuteAsync(capacitacionId, input, ct);
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
    }

    /// <summary>
    /// Extrae el id de capacitación del claim <c>cid</c> emitido por
    /// <c>IJwtTokenGenerator.GenerateCapacitadorToken</c>. Si falta o no parsea
    /// como Guid, devuelve <c>false</c> para que el caller responda 401.
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
            error = "CAPACITADOR_FORBIDDEN",
            message
        };
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
