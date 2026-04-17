namespace Capacitaciones.Application.Dtos.Admin;

/// <summary>DTO de lectura de un administrador.</summary>
public class AdminUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
}
