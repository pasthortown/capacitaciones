using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Inscripcion;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador EF Core para <see cref="IAsistenteRepository"/>.
/// </summary>
public class AsistenteRepository : IAsistenteRepository
{
    private readonly AppDbContext _db;

    public AsistenteRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Asistente entity, CancellationToken ct = default)
    {
        await _db.Asistentes.AddAsync(entity, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
        {
            // Carrera contra UX_Asistente_Capacitacion_Identificacion: dos requests concurrentes
            // que pasaron la pre-check del caso de uso. Re-lanzamos como excepción de dominio
            // para que el controller traduzca a 409 Conflict de forma consistente.
            throw new InscripcionDuplicadaException();
        }
    }

    /// <summary>
    /// Detecta si un <see cref="DbUpdateException"/> corresponde a un choque con el índice único
    /// de asistentes. Reconoce los códigos de SQL Server (2601/2627) cuando el provider los expone
    /// y cae al nombre del índice si no. Conservador: si no coincide con el patrón, no captura.
    /// </summary>
    private static bool IsUniqueIndexViolation(DbUpdateException ex)
    {
        // Provider InMemory: expone la violación como message "An item with the same key has been added."
        // Provider SqlServer: usa Microsoft.Data.SqlClient.SqlException con Number = 2601 | 2627.
        var msg = ex.InnerException?.Message ?? ex.Message;
        if (msg.Contains("UX_Asistente_Capacitacion_Identificacion", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var sqlEx = ex.InnerException as Microsoft.Data.SqlClient.SqlException;
        return sqlEx is not null && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }

    public async Task<IReadOnlyList<Asistente>> ListByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        return await _db.Asistentes
            .AsNoTracking()
            .Include(a => a.Area)
            .Where(a => a.CapacitacionId == capacitacionId)
            .OrderBy(a => a.FechaInscripcion)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsByCapacitacionAndIdentificacionAsync(
        Guid capacitacionId,
        string identificacion,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(identificacion)) return false;
        return await _db.Asistentes
            .AsNoTracking()
            .AnyAsync(a => a.CapacitacionId == capacitacionId && a.Identificacion == identificacion, ct);
    }

    public async Task<Asistente?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Asistentes
            .AsNoTracking()
            .Include(a => a.Area)
            .Include(a => a.Capacitacion)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public Task<int> CountByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        return _db.Asistentes
            .AsNoTracking()
            .CountAsync(a => a.CapacitacionId == capacitacionId, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountByCapacitacionesAsync(
        IEnumerable<Guid> capacitacionIds,
        CancellationToken ct = default)
    {
        var ids = capacitacionIds?.Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        var agrupados = await _db.Asistentes
            .AsNoTracking()
            .Where(a => ids.Contains(a.CapacitacionId))
            .GroupBy(a => a.CapacitacionId)
            .Select(g => new { CapacitacionId = g.Key, Total = g.Count() })
            .ToListAsync(ct);

        return agrupados.ToDictionary(x => x.CapacitacionId, x => x.Total);
    }
}
