using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador EF Core del puerto <see cref="IResponsableRepository"/>.
/// </summary>
public class ResponsableRepository : IResponsableRepository
{
    private readonly AppDbContext _db;

    public ResponsableRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Responsable>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        IQueryable<Responsable> q = _db.Responsables.AsNoTracking();
        if (!includeInactive)
        {
            q = q.Where(r => r.Activo);
        }
        return await q.OrderBy(r => r.Nombres).ToListAsync(ct);
    }

    public Task<Responsable?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _db.Responsables.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task AddAsync(Responsable entity, CancellationToken ct = default)
    {
        await _db.Responsables.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Responsable entity, CancellationToken ct = default)
    {
        var entry = _db.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            _db.Responsables.Update(entity);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetInactivoAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Responsables.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return;
        if (!entity.Activo) return;

        entity.Activo = false;
        entity.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> ExistsActivoAsync(Guid id, CancellationToken ct = default)
    {
        return _db.Responsables.AnyAsync(r => r.Id == id && r.Activo, ct);
    }

    public async Task<bool> ExistenActivosAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idsList = ids?.Distinct().ToList() ?? new List<Guid>();
        if (idsList.Count == 0) return true;

        var count = await _db.Responsables
            .CountAsync(r => idsList.Contains(r.Id) && r.Activo, ct);
        return count == idsList.Count;
    }
}
