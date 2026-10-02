using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Inscripcion;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador EF Core de <see cref="IInscritoRepository"/>. La lista proyecta sin la columna Firma
/// (solo si existe), así no viajan cientos de KB por fila.
/// </summary>
public class InscritoRepository : IInscritoRepository
{
    private readonly AppDbContext _db;

    public InscritoRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<InscritoResumen>> ListAsync(Guid? capacitacionId, string? buscar, CancellationToken ct = default)
    {
        // El filtro global de Capacitacion (Activo) oculta los inscritos de capacitaciones eliminadas.
        var q = _db.Asistentes.AsNoTracking().Where(a => a.Capacitacion != null);
        if (capacitacionId is Guid cid)
        {
            q = q.Where(a => a.CapacitacionId == cid);
        }
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var s = buscar.Trim().ToLower();
            q = q.Where(a =>
                a.Identificacion.ToLower().Contains(s) ||
                a.Nombres.ToLower().Contains(s) ||
                a.Apellidos.ToLower().Contains(s) ||
                a.EmailUsuario.ToLower().Contains(s));
        }

        return await q
            .OrderByDescending(a => a.FechaInscripcion)
            .Select(a => new InscritoResumen(
                a.Id,
                a.CapacitacionId,
                a.Capacitacion!.Codigo,
                a.Capacitacion.Tema,
                a.Nombres,
                a.Apellidos,
                a.Identificacion,
                a.AreaId,
                a.Area != null ? a.Area.Nombre : string.Empty,
                a.EmailUsuario,
                a.FechaInscripcion,
                a.Firma != null && a.Firma != ""))
            .ToListAsync(ct);
    }

    public Task<Asistente?> GetForEditAsync(Guid id, CancellationToken ct = default)
        => _db.Asistentes
            .Include(a => a.Area)
            .Include(a => a.Capacitacion)
            .FirstOrDefaultAsync(a => a.Id == id && a.Capacitacion != null, ct);

    public Task<bool> ExistsOtroConIdentificacionAsync(Guid capacitacionId, string identificacion, Guid excluirId, CancellationToken ct = default)
        => _db.Asistentes.AsNoTracking()
            .AnyAsync(a => a.CapacitacionId == capacitacionId && a.Identificacion == identificacion && a.Id != excluirId, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (Contiene(ex, "UX_PersonaInscrita_Identificacion"))
        {
            throw new InscripcionConcurrenteException();
        }
        catch (DbUpdateException ex) when (Contiene(ex, "UX_Asistente_Capacitacion_Identificacion"))
        {
            throw new InscripcionDuplicadaException();
        }
    }

    private static bool Contiene(DbUpdateException ex, string indexName)
        => (ex.InnerException?.Message ?? ex.Message).Contains(indexName, StringComparison.OrdinalIgnoreCase);
}
