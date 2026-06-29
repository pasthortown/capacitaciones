using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

/// <summary>Adaptador EF Core de <see cref="IColaboradorRepository"/> (externos a DOS). Baja lógica.</summary>
public class ColaboradorRepository : IColaboradorRepository
{
    private readonly AppDbContext _db;

    public ColaboradorRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Colaborador>> ListAsync(string? search, bool includeInactive, CancellationToken ct = default)
    {
        IQueryable<Colaborador> q = _db.Colaboradores.AsNoTracking();
        if (!includeInactive)
        {
            q = q.Where(c => c.Activo);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c =>
                c.Cedula.Contains(s) ||
                c.Name.Contains(s) ||
                (c.Email != null && c.Email.Contains(s)) ||
                (c.JobPosition != null && c.JobPosition.Contains(s)) ||
                (c.WorkArea != null && c.WorkArea.Contains(s)));
        }
        return await q.OrderBy(c => c.Name).ToListAsync(ct);
    }

    public Task<Colaborador?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Colaboradores.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Colaborador?> GetByCedulaAsync(string cedula, CancellationToken ct = default)
    {
        var c = (cedula ?? string.Empty).Trim();
        return _db.Colaboradores.FirstOrDefaultAsync(x => x.Cedula == c, ct);
    }

    public Task<bool> ExistsByCedulaAsync(string cedula, CancellationToken ct = default)
    {
        var c = (cedula ?? string.Empty).Trim();
        return _db.Colaboradores.AnyAsync(x => x.Cedula == c, ct);
    }

    public async Task AddAsync(Colaborador entity, CancellationToken ct = default)
    {
        await _db.Colaboradores.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Colaborador entity, CancellationToken ct = default)
    {
        var entry = _db.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            _db.Colaboradores.Update(entity);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Colaboradores.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null || !entity.Activo) return;
        entity.Activo = false;
        entity.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
