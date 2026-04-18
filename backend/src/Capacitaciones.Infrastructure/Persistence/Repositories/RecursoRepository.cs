using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador EF Core del puerto <see cref="IRecursoRepository"/>.
/// La eliminación es soft (<c>Activo=false</c>) para preservar la trazabilidad histórica
/// y por simplicidad de auditoría; el archivo físico sí se borra por el UseCase.
/// </summary>
public class RecursoRepository : IRecursoRepository
{
    private readonly AppDbContext _db;

    public RecursoRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Recurso entity, CancellationToken ct = default)
    {
        await _db.Recursos.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Recurso>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        IQueryable<Recurso> q = _db.Recursos.AsNoTracking();
        if (!includeInactive)
        {
            q = q.Where(r => r.Activo);
        }
        return await q.OrderByDescending(r => r.FechaCreacion).ToListAsync(ct);
    }

    public Task<Recurso?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _db.Recursos.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task UpdateAsync(Recurso entity, CancellationToken ct = default)
    {
        var entry = _db.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            _db.Recursos.Update(entity);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Recursos.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return;
        if (!entity.Activo) return;

        entity.Activo = false;
        entity.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
