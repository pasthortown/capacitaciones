using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

public class ConfiguracionNotificacionRepository : IConfiguracionNotificacionRepository
{
    private readonly AppDbContext _db;

    public ConfiguracionNotificacionRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<ConfiguracionNotificacion>> ListAsync(CancellationToken ct = default) =>
        _db.ConfiguracionNotificaciones.ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
