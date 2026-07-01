namespace Capacitaciones.Application.Dtos.Admin;

/// <summary>Payload de <c>POST /api/admin/users</c>.</summary>
public class CreateAdminUserDto
{
    /// <summary>Usuario de red (samAccountName) que podrá ingresar al sistema.</summary>
    public string UsuarioRed { get; set; } = string.Empty;
}
