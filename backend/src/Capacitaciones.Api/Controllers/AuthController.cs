using System.Security.Claims;
using Capacitaciones.Application.Dtos.Auth;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly LoginUseCase _login;
    private readonly IAdminUserRepository _users;

    public AuthController(LoginUseCase login, IAdminUserRepository users)
    {
        _login = login;
        _users = users;
    }

    /// <summary>Login de administradores. Devuelve un JWT con TTL de 8 horas.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var result = await _login.ExecuteAsync(request, ct);
        if (result is null)
        {
            return Unauthorized(new { error = "INVALID_CREDENTIALS", message = "Email o contraseña incorrectos." });
        }
        return Ok(result);
    }

    /// <summary>
    /// Logout (stateless JWT): no-op del lado servidor. El cliente descarta el token local.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout() => NoContent();

    /// <summary>Devuelve los datos del usuario autenticado.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var id))
        {
            return Unauthorized();
        }

        var user = await _users.GetByIdAsync(id, ct);
        if (user is null || !user.Activo)
        {
            return Unauthorized();
        }

        return Ok(new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Nombres = user.Nombres,
            Roles = new[] { "Admin" }
        });
    }
}
