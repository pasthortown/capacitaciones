using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// CRUD de capacitaciones. Todos los endpoints requieren la policy "Admin"
/// (Fase 2 configuró JWT + policy).
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/capacitaciones")]
public class CapacitacionesController : ControllerBase
{
    // Firmas en base64 pueden ser grandes (>100KB) y una capacitación puede traer múltiples
    // responsables. 10 MB es un margen amplio sin exponer a DoS por cuerpos gigantes.
    private const int MaxRequestBodyBytes = 10_000_000;

    private readonly ListarCapacitacionesUseCase _listar;
    private readonly ObtenerCapacitacionUseCase _obtener;
    private readonly CrearCapacitacionUseCase _crear;
    private readonly EditarCapacitacionUseCase _editar;
    private readonly EliminarCapacitacionUseCase _eliminar;

    public CapacitacionesController(
        ListarCapacitacionesUseCase listar,
        ObtenerCapacitacionUseCase obtener,
        CrearCapacitacionUseCase crear,
        EditarCapacitacionUseCase editar,
        EliminarCapacitacionUseCase eliminar)
    {
        _listar = listar;
        _obtener = obtener;
        _crear = crear;
        _editar = editar;
        _eliminar = eliminar;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? estado = null,
        CancellationToken ct = default)
    {
        var items = await _listar.ExecuteAsync(includeInactive, estado, ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}", Name = "Capacitaciones_GetById")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _obtener.ExecuteAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCapacitacionDto input,
        CancellationToken ct)
    {
        try
        {
            var dto = await _crear.ExecuteAsync(input, ct);
            var location = Url.Action("GetById", new { id = dto.Id }) ?? string.Empty;
            return Created(location, dto);
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCapacitacionDto input,
        CancellationToken ct)
    {
        try
        {
            var dto = await _editar.ExecuteAsync(id, input, ct);
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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _eliminar.ExecuteAsync(id, ct);
            return NoContent();
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
    }

    private static ObjectResult ToProblem(CapacitacionServiceException ex)
    {
        var status = ex.Codigo switch
        {
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };
        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
