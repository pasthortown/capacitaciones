namespace Capacitaciones.Application.UseCases.Notifications;

/// <summary>
/// Configuración inyectada que expone la URL pública del SPA. La implementación
/// concreta vive en Infrastructure y se alimenta del binding
/// <c>Notifications:PublicBaseUrl</c> (env var <c>Notifications__PublicBaseUrl</c>).
/// La consumen los use cases de notificación para armar URLs absolutas que
/// viajan en los correos (inscripción pública, encuesta, etc.).
/// </summary>
public interface INotificationsConfig
{
    string PublicBaseUrl { get; }
}
