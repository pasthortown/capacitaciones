using Capacitaciones.Application.UseCases.Notifications;

namespace Capacitaciones.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="INotificationsConfig"/> bindeada a la sección
/// <c>Notifications</c> de <c>appsettings</c> (env var
/// <c>Notifications__PublicBaseUrl</c>). El valor es la URL absoluta donde
/// está publicado el SPA — se usa para armar enlaces que viajan por correo
/// (inscripción pública, etc.).
/// </summary>
public class NotificationsConfigOptions : INotificationsConfig
{
    public const string SectionName = "Notifications";

    public string PublicBaseUrl { get; set; } = string.Empty;
}
