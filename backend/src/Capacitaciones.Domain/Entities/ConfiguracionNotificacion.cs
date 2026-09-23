namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Regla por tipo de notificación (clave = nombre de plantilla de mail_sender):
/// si se envía y con qué asunto.
/// </summary>
public class ConfiguracionNotificacion
{
    public string Plantilla { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    /// <summary>Plantilla Jinja del asunto. Null = asunto original del sistema.</summary>
    public string? AsuntoPersonalizado { get; set; }

    public string? ActualizadoPor { get; set; }
    public DateTime? ActualizadoEn { get; set; }
}
