namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Fila única (Id = 1) con el servidor SMTP, el remitente y las copias globales de las
/// notificaciones. No se siembra: mientras no exista, mail_sender usa el <c>.env</c>.
/// </summary>
public class ConfiguracionCorreo
{
    public int Id { get; set; } = 1;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;

    /// <summary>Usuario de autenticación. Null = se autentica con <see cref="RemitenteCorreo"/>.</summary>
    public string? SmtpUser { get; set; }

    /// <summary>Contraseña cifrada con <c>ISecretProtector</c>. Null = sin autenticación (relay).</summary>
    public string? SmtpPasswordCifrada { get; set; }

    public bool UsarTls { get; set; } = true;
    public string RemitenteCorreo { get; set; } = string.Empty;
    public string? RemitenteNombre { get; set; }

    /// <summary>Correos separados por coma.</summary>
    public string? CcGlobal { get; set; }

    /// <summary>Correos separados por coma.</summary>
    public string? BccGlobal { get; set; }

    public string ActualizadoPor { get; set; } = string.Empty;
    public DateTime ActualizadoEn { get; set; }
}
