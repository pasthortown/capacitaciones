using Capacitaciones.Application.Dtos.Inscritos;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Inscritos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Pantalla admin "Inscritos": lista transversal de asistentes (filtro por capacitación y
/// búsqueda), detalle con firma y edición.
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/inscritos")]
public class InscritosController : ControllerBase
{
    // La firma base64 puede ocupar MB (mismo margen que la inscripción pública).
    private const int MaxRequestBodyBytes = 10_000_000;

    private readonly ListarInscritosUseCase _listar;
    private readonly ObtenerInscritoUseCase _obtener;
    private readonly EditarInscritoUseCase _editar;

    public InscritosController(ListarInscritosUseCase listar, ObtenerInscritoUseCase obtener, EditarInscritoUseCase editar)
    {
        _listar = listar;
        _obtener = obtener;
        _editar = editar;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? capacitacionId, [FromQuery] string? buscar, CancellationToken ct)
        => Ok(await _listar.ExecuteAsync(capacitacionId, buscar, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _obtener.ExecuteAsync(id, ct));
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<IActionResult> Update(Guid id, [FromBody] EditarInscritoDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _editar.ExecuteAsync(id, input, ct));
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
            "INSCRITO_NO_ENCONTRADO" => StatusCodes.Status404NotFound,
            "INSCRIPCION_DUPLICADA" => StatusCodes.Status409Conflict,
            "INSCRIPCION_CONCURRENTE" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
