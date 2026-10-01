namespace Capacitaciones.Application.Dtos.Configuracion;

/// <summary>Respuesta de <c>GET/PUT /api/configuracion/correo</c>. Nunca incluye la contraseña.</summary>
public class ConfiguracionCorreoDto
{
    /// <summary>false = no hay fila guardada; mail_sender está usando el .env.</summary>
    public bool Configurado { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public bool TienePassword { get; set; }

    /// <summary>true = hay contraseña guardada pero no se puede descifrar (llave cambió o se perdió).</summary>
    public bool PasswordInvalida { get; set; }
    public bool UsarTls { get; set; } = true;
    public string RemitenteCorreo { get; set; } = string.Empty;
    public string? RemitenteNombre { get; set; }
    public string? CcGlobal { get; set; }
    public string? BccGlobal { get; set; }
    public string? ActualizadoPor { get; set; }
    public DateTime? ActualizadoEn { get; set; }
}

/// <summary>Payload de <c>PUT /api/configuracion/correo</c> y de <c>POST .../prueba</c>.</summary>
public class UpdateConfiguracionCorreoDto
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string? SmtpUser { get; set; }

    /// <summary>Vacío o null = conservar la contraseña guardada.</summary>
    public string? Password { get; set; }

    /// <summary>true = borrar la contraseña guardada (modo relay sin autenticación).</summary>
    public bool QuitarPassword { get; set; }
    public bool UsarTls { get; set; } = true;
    public string RemitenteCorreo { get; set; } = string.Empty;
    public string? RemitenteNombre { get; set; }
    public string? CcGlobal { get; set; }
    public string? BccGlobal { get; set; }
}

/// <summary>Elemento de <c>GET /api/configuracion/correo/notificaciones</c>.</summary>
public class NotificacionDto
{
    public string Plantilla { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string? AsuntoPersonalizado { get; set; }
    public string AsuntoActual { get; set; } = string.Empty;
    public List<string> Variables { get; set; } = new();
}

/// <summary>Elemento del payload de <c>PUT /api/configuracion/correo/notificaciones</c>.</summary>
public class UpdateNotificacionDto
{
    public string Plantilla { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string? AsuntoPersonalizado { get; set; }
}

/// <summary>Respuesta de <c>POST /api/configuracion/correo/prueba</c>.</summary>
public class CorreoPruebaResultadoDto
{
    public bool Ok { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

/// <summary>Respuesta de <c>GET /api/internal/correo-config</c> (solo para mail_sender).</summary>
public class CorreoConfigInternaDto
{
    public bool Configurado { get; set; }
    public bool PasswordInvalida { get; set; }
    public SmtpInternoDto? Smtp { get; set; }
    public List<string> CcGlobal { get; set; } = new();
    public List<string> BccGlobal { get; set; } = new();
    public Dictionary<string, NotificacionReglaDto> Notificaciones { get; set; } = new();
}

/// <summary>Configuración SMTP con la contraseña en claro. Mismo contrato que <c>SmtpSettings</c> de mail_sender.</summary>
public class SmtpInternoDto
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; }
    public string From { get; set; } = string.Empty;
    public string? FromName { get; set; }
}

public class NotificacionReglaDto
{
    public bool Activo { get; set; }
    public string? Asunto { get; set; }
}
