using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

public class ConfiguracionCorreoRepository : IConfiguracionCorreoRepository
{
    private readonly AppDbContext _db;

    public ConfiguracionCorreoRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<ConfiguracionCorreo?> GetAsync(CancellationToken ct = default) =>
        _db.ConfiguracionCorreo.FirstOrDefaultAsync(c => c.Id == 1, ct);

    public async Task UpsertAsync(ConfiguracionCorreo entity, CancellationToken ct = default)
    {
        if (_db.Entry(entity).State == EntityState.Detached)
        {
            await _db.ConfiguracionCorreo.AddAsync(entity, ct);
        }
        await _db.SaveChangesAsync(ct);
    }
}
