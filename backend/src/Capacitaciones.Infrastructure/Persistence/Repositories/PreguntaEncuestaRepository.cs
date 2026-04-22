using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

public class PreguntaEncuestaRepository : IPreguntaEncuestaRepository
{
    private readonly AppDbContext _db;

    public PreguntaEncuestaRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PreguntaEncuesta>> ListAsync(
        Guid? tipoActividadId,
        bool includeInactive,
        CancellationToken ct)
    {
        var q = _db.PreguntasEncuesta
            .AsNoTracking()
            .Include(p => p.TipoActividad)
            .AsQueryable();

        if (!includeInactive)
        {
            q = q.Where(p => p.Activo);
        }
        if (tipoActividadId.HasValue && tipoActividadId.Value != Guid.Empty)
        {
            q = q.Where(p => p.TipoActividadId == tipoActividadId.Value);
        }

        return await q
            .OrderBy(p => p.TipoActividad!.Nombre)
            .ThenBy(p => p.FechaCreacion)
            .ToListAsync(ct);
    }

    public async Task<PreguntaEncuesta?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.PreguntasEncuesta
            .Include(p => p.TipoActividad)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task AddAsync(PreguntaEncuesta entity, CancellationToken ct)
    {
        await _db.PreguntasEncuesta.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PreguntaEncuesta entity, CancellationToken ct)
    {
        _db.PreguntasEncuesta.Update(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(PreguntaEncuesta entity, CancellationToken ct)
    {
        entity.Activo = false;
        entity.FechaActualizacion = DateTime.UtcNow;
        _db.PreguntasEncuesta.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
