namespace Capacitaciones.Infrastructure.Services;

/// <summary>
/// Configuración de la integración con ControlTareas (Sistema Gestión Interno). Los valores
/// llegan por entorno: <c>CONTROLTAREAS_API_URL</c>, <c>CONTROLTAREAS_API_USER</c>,
/// <c>CONTROLTAREAS_API_PASSWORD</c>. Sin URL/usuario/clave, la integración queda deshabilitada.
/// </summary>
public sealed class ControlTareasOptions
{
    public const string SectionName = "ControlTareas";

    public string? BaseUrl { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 30;

    public bool Enabled =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(User)
        && !string.IsNullOrWhiteSpace(Password);
}
