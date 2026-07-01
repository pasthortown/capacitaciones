namespace Capacitaciones.Application.Dtos.Auth;

/// <summary>Payload de <c>POST /api/auth/login</c>.</summary>
public class LoginRequestDto
{
    /// <summary>Usuario de red (samAccountName) para validar contra el dominio.</summary>
    public string? Usuario { get; set; }
    /// <summary>Compatibilidad: si el front envía "email", se usa como usuario.</summary>
    public string? Email { get; set; }
    public string Password { get; set; } = string.Empty;

    /// <summary>Identificador efectivo enviado (usuario de red).</summary>
    public string Identificador => (Usuario ?? Email ?? string.Empty).Trim();
}
