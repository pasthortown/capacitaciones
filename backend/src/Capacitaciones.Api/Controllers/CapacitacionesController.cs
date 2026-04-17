using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Application.UseCases.Inscripcion;
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
    private readonly GenerarLinkCapacitadorUseCase _generarLinkCapacitador;
    private readonly GenerarLinkInscripcionUseCase _generarLinkInscripcion;

    public CapacitacionesController(
        ListarCapacitacionesUseCase listar,
        ObtenerCapacitacionUseCase obtener,
        CrearCapacitacionUseCase crear,
        EditarCapacitacionUseCase editar,
        EliminarCapacitacionUseCase eliminar,
        GenerarLinkCapacitadorUseCase generarLinkCapacitador,
        GenerarLinkInscripcionUseCase generarLinkInscripcion)
    {
        _listar = listar;
        _obtener = obtener;
        _crear = crear;
        _editar = editar;
        _eliminar = eliminar;
        _generarLinkCapacitador = generarLinkCapacitador;
        _generarLinkInscripcion = generarLinkInscripcion;
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

    /// <summary>
    /// Fase 4: genera un link firmado (JWT role=Capacitador) para entregarle al capacitador.
    /// El body de la respuesta incluye la URL relativa y la fecha de expiración.
    /// Cada invocación emite un token nuevo que convive con los anteriores hasta expirar
    /// (no hay lista negra — ver nota en <c>GenerarLinkCapacitadorUseCase</c>).
    /// </summary>
    [HttpPost("{id:guid}/link-capacitador")]
    public async Task<IActionResult> GenerarLinkCapacitador(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _generarLinkCapacitador.ExecuteAsync(id, ct);
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

    /// <summary>
    /// Fase 5: genera un link firmado (JWT role=Inscripcion) para el formulario público de inscripción.
    /// Cada invocación emite un token NUEVO que convive con los anteriores hasta expirar (no hay lista negra).
    /// </summary>
    [HttpPost("{id:guid}/link-inscripcion")]
    public async Task<IActionResult> GenerarLinkInscripcion(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _generarLinkInscripcion.ExecuteAsync(id, ct);
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

    private static ObjectResult ToProblem(CapacitacionServiceException ex)
    {
        var status = ex.Codigo switch
        {
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "CAPACITACION_INACTIVA" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
