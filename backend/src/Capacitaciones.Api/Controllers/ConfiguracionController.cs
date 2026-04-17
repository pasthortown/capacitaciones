using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.UseCases.Configuracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/configuracion")]
public class ConfiguracionController : ControllerBase
{
    private readonly ObtenerNumeracionUseCase _obtener;
    private readonly ActualizarNumeracionUseCase _actualizar;

    public ConfiguracionController(
        ObtenerNumeracionUseCase obtener,
        ActualizarNumeracionUseCase actualizar)
    {
        _obtener = obtener;
        _actualizar = actualizar;
    }

    [HttpGet("numeracion")]
    public async Task<IActionResult> GetNumeracion(CancellationToken ct)
    {
        var dto = await _obtener.ExecuteAsync(ct);
        return Ok(dto);
    }

    [HttpPut("numeracion")]
    public async Task<IActionResult> PutNumeracion(
        [FromBody] UpdateConfiguracionNumeracionDto input,
        CancellationToken ct)
    {
        try
        {
            var dto = await _actualizar.ExecuteAsync(input, ct);
            return Ok(dto);
        }
        catch (ConfiguracionNumeracionException ex)
        {
            return new ObjectResult(new { error = ex.Codigo, message = ex.Message })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
    }
}
