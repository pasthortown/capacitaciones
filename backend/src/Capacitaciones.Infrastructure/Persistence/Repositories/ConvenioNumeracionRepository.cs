using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

public class ConvenioNumeracionRepository : IConvenioNumeracionRepository
{
    private readonly AppDbContext _db;

    public ConvenioNumeracionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ConvenioNumeracion> GetAsync(CancellationToken ct = default)
    {
        var cfg = await _db.ConvenioNumeracion.FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (cfg is null)
        {
            cfg = new ConvenioNumeracion { Id = 1, SiguienteNumero = 1, UltimaActualizacion = null };
            await _db.ConvenioNumeracion.AddAsync(cfg, ct);
            await _db.SaveChangesAsync(ct);
        }
        return cfg;
    }

    public async Task UpdateAsync(ConvenioNumeracion entity, CancellationToken ct = default)
    {
        _db.ConvenioNumeracion.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
