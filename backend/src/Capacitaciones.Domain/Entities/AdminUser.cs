namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Usuario administrador del panel. Autenticación por email + contraseña hasheada con BCrypt.
/// Política de email: el dominio debe ser <c>@dos.com.ec</c> (se valida en el caso de uso de creación).
/// </summary>
public class AdminUser
{
    public Guid Id { get; set; }

    /// <summary>Correo corporativo completo (usuario@dos.com.ec). Único.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash BCrypt de la contraseña (nunca se almacena la contraseña en claro).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Nombre descriptivo del administrador. Se usa como claim <c>name</c> en el JWT.</summary>
    public string Nombres { get; set; } = string.Empty;

    /// <summary>Eliminación lógica.</summary>
    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaActualizacion { get; set; }

    public DateTime? UltimoLogin { get; set; }
}
