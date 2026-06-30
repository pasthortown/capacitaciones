using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

/// <summary>Adaptador EF Core de <see cref="IConvenioRepository"/>. Carga los ítems (Include). Baja lógica.</summary>
public class ConvenioRepository : IConvenioRepository
{
    private readonly AppDbContext _db;

    public ConvenioRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Convenio>> ListAsync(string? search, bool includeInactive, CancellationToken ct = default)
    {
        IQueryable<Convenio> q = _db.Convenios.AsNoTracking().Include(c => c.Items).Include(c => c.Anexos);
        if (!includeInactive) q = q.Where(c => c.Activo);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c =>
                c.Titulo.Contains(s) ||
                c.CedulaColaborador.Contains(s) ||
                c.NombreColaborador.Contains(s) ||
                (c.Tipo != null && c.Tipo.Contains(s)) ||
                (c.NombreCurso != null && c.NombreCurso.Contains(s)) ||
                (c.Marca != null && c.Marca.Contains(s)));
        }
        return await q.OrderByDescending(c => c.Fecha).ToListAsync(ct);
    }

    public Task<Convenio?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Convenios.Include(c => c.Items).Include(c => c.Anexos).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Convenio>> ListByCedulaAsync(string cedula, bool includeInactive, CancellationToken ct = default)
    {
        var c = (cedula ?? string.Empty).Trim();
        IQueryable<Convenio> q = _db.Convenios.AsNoTracking().Include(x => x.Items).Include(x => x.Anexos)
            .Where(x => x.CedulaColaborador == c);
        if (!includeInactive) q = q.Where(x => x.Activo);
        return await q.OrderByDescending(x => x.Fecha).ToListAsync(ct);
    }

    public async Task AddAsync(Convenio entity, CancellationToken ct = default)
    {
        await _db.Convenios.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> GetMaxNumeroRegistroAsync(CancellationToken ct = default)
        => await _db.Convenios.AsNoTracking()
            .Where(c => c.NumeroRegistro != null)
            .MaxAsync(c => (int?)c.NumeroRegistro, ct) ?? 0;

    public async Task UpdateAsync(Convenio entity, CancellationToken ct = default)
    {
        // La entidad viene tracked desde GetByIdAsync (con Include de Items). Al haber
        // reemplazado la colección en el UseCase, el change tracker ya marcó altas/bajas;
        // basta con persistir. (No usamos Update() para no re-marcar el grafo completo.)
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Convenios.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null || !entity.Activo) return;
        entity.Activo = false;
        entity.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
