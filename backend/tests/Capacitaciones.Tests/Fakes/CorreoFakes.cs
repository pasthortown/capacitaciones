using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Configuracion;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests.Fakes;

internal sealed class InMemoryConfiguracionCorreoRepository : IConfiguracionCorreoRepository
{
    public ConfiguracionCorreo? Current { get; set; }

    public Task<ConfiguracionCorreo?> GetAsync(CancellationToken ct = default) => Task.FromResult(Current);

    public Task UpsertAsync(ConfiguracionCorreo entity, CancellationToken ct = default)
    {
        Current = entity;
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryConfiguracionNotificacionRepository : IConfiguracionNotificacionRepository
{
    public List<ConfiguracionNotificacion> Items { get; } = NotificacionesCatalogo.Todas
        .Select(d => new ConfiguracionNotificacion { Plantilla = d.Plantilla, Nombre = d.Nombre, Activo = true })
        .ToList();

    public int SaveCount { get; private set; }

    public Task<List<ConfiguracionNotificacion>> ListAsync(CancellationToken ct = default) => Task.FromResult(Items);

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
