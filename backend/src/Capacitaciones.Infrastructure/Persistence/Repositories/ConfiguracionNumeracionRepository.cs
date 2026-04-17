using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

public class ConfiguracionNumeracionRepository : IConfiguracionNumeracionRepository
{
    private readonly AppDbContext _db;

    public ConfiguracionNumeracionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ConfiguracionNumeracion> GetAsync(CancellationToken ct = default)
    {
        var cfg = await _db.ConfiguracionNumeracion.FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (cfg is null)
        {
            // Defensa extra: si por alguna razón la fila semilla no existe (ej. InMemory),
            // se crea on-the-fly para mantener el contrato del puerto.
            cfg = new ConfiguracionNumeracion { Id = 1, SiguienteNumero = 1, UltimaActualizacion = null };
            await _db.ConfiguracionNumeracion.AddAsync(cfg, ct);
            await _db.SaveChangesAsync(ct);
        }
        return cfg;
    }

    public async Task UpdateAsync(ConfiguracionNumeracion entity, CancellationToken ct = default)
    {
        _db.ConfiguracionNumeracion.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
