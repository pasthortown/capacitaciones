namespace Capacitaciones.Application.Dtos.Auth;

/// <summary>Respuesta de <c>POST /api/auth/login</c>.</summary>
public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
}
