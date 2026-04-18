using Capacitaciones.Application.Dtos.PaseLista;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Application.UseCases.PaseLista;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Endpoints consumidos por la pantalla pública del capacitador para el pase de lista (Fase 10).
/// Todas las rutas leen la capacitación implícita desde el claim <c>cid</c> emitido por
/// <see cref="Application.Ports.IJwtTokenGenerator.GeneratePaseListaToken"/>.
///
/// El capacitador NO puede pasar el id por URL: solo opera sobre la capacitación que el admin
/// autorizó al emitirle el link, mismo patrón que <see cref="CapacitadorController"/>.
/// </summary>
[ApiController]
[Authorize(Policy = "PaseLista")]
[Route("api/capacitador/pase-lista")]
public class PaseListaController : ControllerBase
{
    private readonly ObtenerPaseListaUseCase _obtener;
    private readonly MarcarAsistenciaUseCase _marcar;

    public PaseListaController(
        ObtenerPaseListaUseCase obtener,
        MarcarAsistenciaUseCase marcar)
    {
        _obtener = obtener;
        _marcar = marcar;
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

    [HttpPut("asistentes/{asistenteId:guid}")]
    public async Task<IActionResult> Marcar(
        Guid asistenteId,
        [FromBody] MarcarAsistenciaDto? input,
        CancellationToken ct)
    {
        if (!TryGetCapacitacionId(out var capacitacionId))
        {
            return Unauthorized();
        }

        try
        {
            var estado = MarcarAsistenciaUseCase.ParseEstado(input?.EstadoAsistencia);
            var dto = await _marcar.ExecuteAsync(capacitacionId, asistenteId, estado, ct);
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
            var status = ex.Codigo switch
            {
                "ESTADO_ASISTENCIA_INVALIDO" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest
            };
            return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
        }
    }

    /// <summary>
    /// Extrae la capacitación del claim <c>cid</c> del token. Si falta o no parsea como Guid,
    /// devuelve <c>false</c> para que el caller responda 401. Mismo patrón que <see cref="CapacitadorController"/>.
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
            error = "PASE_LISTA_FORBIDDEN",
            message
        };
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
