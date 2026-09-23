using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>Puerto de <see cref="ConfiguracionNotificacion"/>. Las filas vienen del seed; no se crean ni borran.</summary>
public interface IConfiguracionNotificacionRepository
{
    /// <summary>Devuelve las entidades rastreadas: modificarlas y llamar <see cref="SaveChangesAsync"/>.</summary>
    Task<List<ConfiguracionNotificacion>> ListAsync(CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
