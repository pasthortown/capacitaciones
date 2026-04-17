using Capacitaciones.Application.Dtos.Responsables;
using Capacitaciones.Application.UseCases.Responsable;
using Capacitaciones.Application.UseCases.Responsables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Endpoints consumidos por la página pública del responsable autenticada vía JWT
/// firmado con claims <c>role=Responsable</c> y <c>rid=&lt;responsableId&gt;</c>.
///
/// Todas las rutas leen el responsable implícito desde el claim <c>rid</c>:
/// el responsable NO puede pasar el id por URL — solo opera sobre su propio perfil.
/// </summary>
[ApiController]
[Authorize(Policy = "Responsable")]
[Route("api/responsable/perfil")]
public class ResponsableController : ControllerBase
{
    // Firmas base64 pueden ser grandes — mismo tope que el resto de endpoints con firma.
    private const int MaxRequestBodyBytes = 10_000_000;

    private readonly ObtenerPerfilResponsableUseCase _obtener;
    private readonly ActualizarPerfilResponsableUseCase _actualizar;

    public ResponsableController(
        ObtenerPerfilResponsableUseCase obtener,
        ActualizarPerfilResponsableUseCase actualizar)
    {
        _obtener = obtener;
        _actualizar = actualizar;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!TryGetResponsableId(out var responsableId))
        {
            return Unauthorized();
        }

        try
        {
            var dto = await _obtener.ExecuteAsync(responsableId, ct);
            return Ok(dto);
        }
        catch (ResponsableNotFoundException)
        {
            return NotFound();
        }
        catch (ResponsableForbiddenException ex)
        {
            return Forbidden(ex.Message);
        }
    }

    [HttpPut]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateResponsablePerfilDto input,
        CancellationToken ct)
    {
        if (!TryGetResponsableId(out var responsableId))
        {
            return Unauthorized();
        }

        try
        {
            var dto = await _actualizar.ExecuteAsync(responsableId, input, ct);
            return Ok(dto);
        }
        catch (ResponsableNotFoundException)
        {
            return NotFound();
        }
        catch (ResponsableForbiddenException ex)
        {
            return Forbidden(ex.Message);
        }
        catch (ResponsableServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Extrae el id del responsable del claim <c>rid</c> emitido por
    /// <c>IJwtTokenGenerator.GenerateResponsableToken</c>. Si falta o no parsea
    /// como Guid, devuelve <c>false</c> para que el caller responda 401.
    /// </summary>
    private bool TryGetResponsableId(out Guid id)
    {
        id = Guid.Empty;
        var raw = User.FindFirst("rid")?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return Guid.TryParse(raw, out id);
    }

    private ObjectResult Forbidden(string message)
    {
        var problem = new
        {
            error = "RESPONSABLE_FORBIDDEN",
            message
        };
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
    }

    private static ObjectResult ToProblem(ResponsableServiceException ex)
    {
        var status = ex.Codigo switch
        {
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };
        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
