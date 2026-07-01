namespace Capacitaciones.Application.Dtos.Admin;

/// <summary>DTO de lectura de un administrador.</summary>
public class AdminUserDto
{
    public Guid Id { get; set; }
    /// <summary>Usuario de red permitido.</summary>
    public string UsuarioRed { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? UltimoLogin { get; set; }
}
