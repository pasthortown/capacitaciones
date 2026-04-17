namespace Capacitaciones.Application.Dtos.Auth;

/// <summary>Payload de <c>POST /api/auth/login</c>.</summary>
public class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
