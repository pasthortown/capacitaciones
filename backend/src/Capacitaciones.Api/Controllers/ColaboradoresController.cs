using Capacitaciones.Application.Dtos.Colaboradores;
using Capacitaciones.Application.UseCases.Colaboradores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Módulo Colaboradores (Entrenamiento). Dos orígenes:
///  - <b>DOS</b>: colaboradores internos traídos del API de ControlTareas (solo lectura).
///  - <b>Externos</b>: personas ajenas a DOS, administradas localmente con CRUD completo.
/// Una cédula que ya existe en DOS no puede registrarse como externa.
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/colaboradores")]
public class ColaboradoresController : ControllerBase
{
    private readonly ListarColaboradoresDosUseCase _listarDos;
    private readonly ListarColaboradoresExternosUseCase _listarExternos;
    private readonly ObtenerColaboradorExternoUseCase _obtener;
    private readonly CrearColaboradorExternoUseCase _crear;
    private readonly EditarColaboradorExternoUseCase _editar;
    private readonly EliminarColaboradorExternoUseCase _eliminar;

    public ColaboradoresController(
        ListarColaboradoresDosUseCase listarDos,
        ListarColaboradoresExternosUseCase listarExternos,
        ObtenerColaboradorExternoUseCase obtener,
        CrearColaboradorExternoUseCase crear,
        EditarColaboradorExternoUseCase editar,
        EliminarColaboradorExternoUseCase eliminar)
    {
        _listarDos = listarDos;
        _listarExternos = listarExternos;
        _obtener = obtener;
        _crear = crear;
        _editar = editar;
        _eliminar = eliminar;
    }

    /// <summary>Colaboradores internos de DOS (ControlTareas). Solo lectura.</summary>
    [HttpGet("dos")]
    public async Task<IActionResult> ListarDos(
        [FromQuery] string? buscar,
        [FromQuery] bool incluirInactivos = false,
        CancellationToken ct = default)
    {
        var items = await _listarDos.ExecuteAsync(buscar, incluirInactivos, ct);
        return Ok(new { integracionDisponible = _listarDos.IntegracionDisponible, items });
    }

    /// <summary>Colaboradores externos (locales).</summary>
    [HttpGet("externos")]
    public async Task<IActionResult> ListarExternos(
        [FromQuery] string? buscar,
        [FromQuery] bool incluirInactivos = false,
        CancellationToken ct = default)
    {
        var items = await _listarExternos.ExecuteAsync(buscar, incluirInactivos, ct);
        return Ok(items);
    }

    [HttpGet("externos/{id:guid}", Name = "Colaboradores_GetExterno")]
    public async Task<IActionResult> ObtenerExterno(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _obtener.ExecuteAsync(id, ct));
        }
        catch (ColaboradorNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("externos")]
    public async Task<IActionResult> CrearExterno([FromBody] ColaboradorRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _crear.ExecuteAsync(req, ct);
            return CreatedAtRoute("Colaboradores_GetExterno", new { id = dto.Id }, dto);
        }
        catch (ColaboradorServiceException ex)
        {
            return MapError(ex);
        }
        catch (InvalidOperationException ex)
        {
            // Falló la verificación contra ControlTareas: no podemos garantizar la regla.
            return new ObjectResult(new { error = "CONTROLTAREAS_NO_DISPONIBLE", message = ex.Message })
            {
                StatusCode = StatusCodes.Status502BadGateway,
            };
        }
    }

    [HttpPut("externos/{id:guid}")]
    public async Task<IActionResult> EditarExterno(Guid id, [FromBody] ColaboradorRequest req, CancellationToken ct)
    {
        try
        {
            return Ok(await _editar.ExecuteAsync(id, req, ct));
        }
        catch (ColaboradorServiceException ex)
        {
            return MapError(ex);
        }
    }

    [HttpDelete("externos/{id:guid}")]
    public async Task<IActionResult> EliminarExterno(Guid id, CancellationToken ct)
    {
        try
        {
            await _eliminar.ExecuteAsync(id, ct);
            return NoContent();
        }
        catch (ColaboradorNotFoundException)
        {
            return NotFound();
        }
    }

    private IActionResult MapError(ColaboradorServiceException ex) => ex switch
    {
        ColaboradorNotFoundException => NotFound(),
        ColaboradorValidacionException => BadRequest(new { error = ex.Codigo, message = ex.Message }),
        _ => new ObjectResult(new { error = ex.Codigo, message = ex.Message })
        {
            StatusCode = StatusCodes.Status409Conflict,
        },
    };
}
