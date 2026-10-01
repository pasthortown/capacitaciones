using System.Security.Claims;
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
    private readonly ObtenerConfiguracionCorreoUseCase _obtenerCorreo;
    private readonly ActualizarConfiguracionCorreoUseCase _actualizarCorreo;
    private readonly ListarNotificacionesUseCase _listarNotificaciones;
    private readonly ActualizarNotificacionesUseCase _actualizarNotificaciones;
    private readonly EnviarCorreoPruebaUseCase _enviarPrueba;

    public ConfiguracionController(
        ObtenerNumeracionUseCase obtener,
        ActualizarNumeracionUseCase actualizar,
        ObtenerConfiguracionCorreoUseCase obtenerCorreo,
        ActualizarConfiguracionCorreoUseCase actualizarCorreo,
        ListarNotificacionesUseCase listarNotificaciones,
        ActualizarNotificacionesUseCase actualizarNotificaciones,
        EnviarCorreoPruebaUseCase enviarPrueba)
    {
        _obtener = obtener;
        _actualizar = actualizar;
        _obtenerCorreo = obtenerCorreo;
        _actualizarCorreo = actualizarCorreo;
        _listarNotificaciones = listarNotificaciones;
        _actualizarNotificaciones = actualizarNotificaciones;
        _enviarPrueba = enviarPrueba;
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

    [HttpGet("correo")]
    public async Task<IActionResult> GetCorreo(CancellationToken ct) =>
        Ok(await _obtenerCorreo.ExecuteAsync(ct));

    [HttpPut("correo")]
    public Task<IActionResult> PutCorreo([FromBody] UpdateConfiguracionCorreoDto input, CancellationToken ct) =>
        Ejecutar(async email => Ok(await _actualizarCorreo.ExecuteAsync(input, email, ct)));

    [HttpGet("correo/notificaciones")]
    public async Task<IActionResult> GetNotificaciones(CancellationToken ct) =>
        Ok(await _listarNotificaciones.ExecuteAsync(ct));

    [HttpPut("correo/notificaciones")]
    public Task<IActionResult> PutNotificaciones([FromBody] List<UpdateNotificacionDto> input, CancellationToken ct) =>
        Ejecutar(async email => Ok(await _actualizarNotificaciones.ExecuteAsync(input, email, ct)));

    [HttpPost("correo/prueba")]
    public Task<IActionResult> PostPrueba([FromBody] UpdateConfiguracionCorreoDto input, CancellationToken ct) =>
        Ejecutar(async email => Ok(await _enviarPrueba.ExecuteAsync(input, email, ct)));

    /// <summary>Resuelve el email del admin y traduce <see cref="ConfiguracionCorreoException"/> a HTTP.</summary>
    private async Task<IActionResult> Ejecutar(Func<string, Task<IActionResult>> accion)
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "SIN_EMAIL", message = "El token no incluye el email del administrador." });
        }

        try
        {
            return await accion(email);
        }
        catch (ConfiguracionCorreoException ex)
        {
            var status = ex.Codigo == "ENCRYPTION_KEY_MISSING"
                ? StatusCodes.Status500InternalServerError
                : StatusCodes.Status400BadRequest;
            return new ObjectResult(new { error = ex.Codigo, message = ex.Message, errores = ex.Errores })
            {
                StatusCode = status
            };
        }
    }
}
