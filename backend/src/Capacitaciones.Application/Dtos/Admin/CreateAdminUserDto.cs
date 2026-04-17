namespace Capacitaciones.Application.Dtos.Admin;

/// <summary>Payload de <c>POST /api/admin/users</c>.</summary>
public class CreateAdminUserDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
}
