namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Usuario administrador del panel. Autenticación por email + contraseña hasheada con BCrypt.
/// Política de email: el dominio debe ser <c>@dos.com.ec</c> (se valida en el caso de uso de creación).
/// </summary>
public class AdminUser
{
    public Guid Id { get; set; }

    /// <summary>Usuario de red (samAccountName) permitido para ingresar. <b>Llave de la lista de
    /// permitidos</b> y del cruce con el dominio. Único (case-insensitive).</summary>
    public string UsuarioRed { get; set; } = string.Empty;

    /// <summary>Correo corporativo (usuario@dos.com.ec). Opcional: se completa desde el AD al ingresar.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash BCrypt (legado). Con login por dominio ya no se usa; queda nullable en BD.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Nombre descriptivo. Se completa desde el AD al ingresar; se usa como claim <c>name</c>.</summary>
    public string Nombres { get; set; } = string.Empty;

    /// <summary>Eliminación lógica.</summary>
    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaActualizacion { get; set; }

    public DateTime? UltimoLogin { get; set; }
}
