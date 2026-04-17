using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Base genérica para repositorios de catálogos. Centraliza las operaciones CRUD
/// comunes sobre entidades que heredan de <see cref="CatalogoBase"/>.
/// </summary>
public abstract class CatalogoRepositoryBase<T> : ICatalogoRepository<T> where T : CatalogoBase
{
    protected readonly AppDbContext Db;

    protected CatalogoRepositoryBase(AppDbContext db)
    {
        Db = db;
    }

    protected DbSet<T> Set => Db.Set<T>();

    public virtual async Task<IEnumerable<T>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        IQueryable<T> q = Set.AsNoTracking();
        if (!includeInactive)
        {
            q = q.Where(x => x.Activo);
        }
        return await q.OrderBy(x => x.Nombre).ToListAsync(ct);
    }

    public virtual Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(x => x.Id == id, ct);

    public virtual Task<T?> GetByNombreAsync(string nombre, CancellationToken ct = default) =>
        // La collation por defecto de SQL Server (CI) hace que esta igualdad sea case-insensitive.
        Set.FirstOrDefaultAsync(x => x.Nombre == nombre, ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
    {
        await Set.AddAsync(entity, ct);
        await Db.SaveChangesAsync(ct);
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        await Set.AddRangeAsync(entities, ct);
        await Db.SaveChangesAsync(ct);
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        Set.Update(entity);
        await Db.SaveChangesAsync(ct);
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await Set.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return;
        entity.Activo = false;
        entity.FechaActualizacion = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
    }
}
