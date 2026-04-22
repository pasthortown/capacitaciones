using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.UseCases.PreguntasEncuesta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// CRUD admin del catálogo de preguntas de encuesta (una N de preguntas por tipo de actividad).
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/preguntas-encuesta")]
public class PreguntasEncuestaController : ControllerBase
{
    private readonly ListarPreguntasEncuestaUseCase _listar;
    private readonly ObtenerPreguntaEncuestaUseCase _obtener;
    private readonly CrearPreguntaEncuestaUseCase _crear;
    private readonly EditarPreguntaEncuestaUseCase _editar;
    private readonly EliminarPreguntaEncuestaUseCase _eliminar;

    public PreguntasEncuestaController(
        ListarPreguntasEncuestaUseCase listar,
        ObtenerPreguntaEncuestaUseCase obtener,
        CrearPreguntaEncuestaUseCase crear,
        EditarPreguntaEncuestaUseCase editar,
        EliminarPreguntaEncuestaUseCase eliminar)
    {
        _listar = listar;
        _obtener = obtener;
        _crear = crear;
        _editar = editar;
        _eliminar = eliminar;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tipoActividadId = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var items = await _listar.ExecuteAsync(tipoActividadId, includeInactive, ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}", Name = "PreguntasEncuesta_GetById")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _obtener.ExecuteAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] UpsertPreguntaEncuestaDto input,
        CancellationToken ct)
    {
        try
        {
            var dto = await _crear.ExecuteAsync(input, ct);
            var location = Url.Action(
                action: nameof(GetById),
                values: new { id = dto.Id }) ?? string.Empty;
            return Created(location, dto);
        }
        catch (PreguntaEncuestaServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpsertPreguntaEncuestaDto input,
        CancellationToken ct)
    {
        try
        {
            var dto = await _editar.ExecuteAsync(id, input, ct);
            return Ok(dto);
        }
        catch (PreguntaEncuestaNotFoundException)
        {
            return NotFound();
        }
        catch (PreguntaEncuestaServiceException ex)
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
        catch (PreguntaEncuestaNotFoundException)
        {
            return NotFound();
        }
        catch (PreguntaEncuestaServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    private static ObjectResult ToProblem(PreguntaEncuestaServiceException ex)
    {
        var status = ex.Codigo switch
        {
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "TEXTO_VACIO" => StatusCodes.Status400BadRequest,
            "TEXTO_DEMASIADO_LARGO" => StatusCodes.Status400BadRequest,
            "TIPO_ACTIVIDAD_REQUERIDO" => StatusCodes.Status400BadRequest,
            "TIPO_ACTIVIDAD_NO_ENCONTRADO" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };
        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
