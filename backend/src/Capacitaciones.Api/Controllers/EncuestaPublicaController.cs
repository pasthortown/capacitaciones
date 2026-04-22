using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.UseCases.Encuesta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Endpoints públicos (sin auth) para la encuesta de satisfacción: obtener el formulario
/// y enviar las respuestas. El asistente se autoidentifica con su cédula; el link que
/// se comparte es público y usa el id de la capacitación directamente.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/publico/encuesta")]
public class EncuestaPublicaController : ControllerBase
{
    private readonly ObtenerEncuestaPublicaUseCase _obtener;
    private readonly SubmitEncuestaUseCase _submit;

    public EncuestaPublicaController(
        ObtenerEncuestaPublicaUseCase obtener,
        SubmitEncuestaUseCase submit)
    {
        _obtener = obtener;
        _submit = submit;
    }

    [HttpGet("{capacitacionId:guid}")]
    public async Task<IActionResult> Get(Guid capacitacionId, CancellationToken ct)
    {
        try
        {
            var dto = await _obtener.ExecuteAsync(capacitacionId, ct);
            return Ok(dto);
        }
        catch (EncuestaServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    [HttpPost("{capacitacionId:guid}/responder")]
    public async Task<IActionResult> Responder(
        Guid capacitacionId,
        [FromBody] SubmitEncuestaDto input,
        CancellationToken ct)
    {
        try
        {
            await _submit.ExecuteAsync(capacitacionId, input, ct);
            return NoContent();
        }
        catch (EncuestaServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    private static ObjectResult ToProblem(EncuestaServiceException ex)
    {
        var status = ex.Codigo switch
        {
            "CAPACITACION_NOT_FOUND" => StatusCodes.Status404NotFound,
            "CAPACITACION_NO_FINALIZADA" => StatusCodes.Status409Conflict,
            "ASISTENTE_NO_INSCRITO" => StatusCodes.Status404NotFound,
            "ENCUESTA_YA_RESPONDIDA" => StatusCodes.Status409Conflict,
            "SIN_PREGUNTAS_CONFIGURADAS" => StatusCodes.Status409Conflict,
            "IDENTIFICACION_REQUERIDA" => StatusCodes.Status400BadRequest,
            "RESPUESTA_FALTANTE" => StatusCodes.Status400BadRequest,
            "VALOR_FUERA_DE_RANGO" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };
        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
