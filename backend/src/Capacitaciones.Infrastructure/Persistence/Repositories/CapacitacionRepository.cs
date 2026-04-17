using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador EF Core para <see cref="ICapacitacionRepository"/>.
/// <c>AddAsync</c> y <c>UpdateWithResponsablesAsync</c> envuelven la operación en
/// <c>IExecutionStrategy.ExecuteAsync</c> para tolerar errores transitorios de SQL Server
/// manteniendo la semántica transaccional.
/// </summary>
public class CapacitacionRepository : ICapacitacionRepository
{
    private const string CodigoPrefix = "CAP-PC-REG-";

    private readonly AppDbContext _db;

    public CapacitacionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Capacitacion>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        IQueryable<Capacitacion> q = _db.Capacitaciones
            .AsNoTracking()
            .Include(c => c.Modalidad)
            .Include(c => c.TipoActividad);

        if (!includeInactive)
        {
            q = q.Where(c => c.Activo);
        }

        return await q
            .OrderByDescending(c => c.FechaHoraInicio)
            .ToListAsync(ct);
    }

    public async Task<Capacitacion?> GetByIdWithResponsablesAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Capacitaciones
            .Include(c => c.Modalidad)
            .Include(c => c.TipoActividad)
            .Include(c => c.Responsables)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<string> AddAsync(
        Capacitacion entity,
        Func<CancellationToken, Task<string>> codeFactory,
        CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // BeginTransaction en providers in-memory es un no-op (controlado por warnings ignore).
            // En SQL Server envuelve el claim de código + inserts en una transacción atómica.
            var supportsTx = _db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
            if (supportsTx)
            {
                tx = await _db.Database.BeginTransactionAsync(ct);
            }

            try
            {
                var codigo = await codeFactory(ct);
                entity.Codigo = codigo;

                await _db.Capacitaciones.AddAsync(entity, ct);
                await _db.SaveChangesAsync(ct);

                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                return codigo;
            }
            catch
            {
                if (tx is not null)
                {
                    await tx.RollbackAsync(ct);
                }
                throw;
            }
            finally
            {
                if (tx is not null)
                {
                    await tx.DisposeAsync();
                }
            }
        });
    }

    public async Task UpdateWithResponsablesAsync(
        Capacitacion entity,
        IEnumerable<Responsable> nuevosResponsables,
        CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var supportsTx = _db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
            if (supportsTx)
            {
                tx = await _db.Database.BeginTransactionAsync(ct);
            }

            try
            {
                // Replace-all: borrar responsables existentes y agregar los nuevos.
                var existentes = await _db.Responsables
                    .Where(r => r.CapacitacionId == entity.Id)
                    .ToListAsync(ct);
                if (existentes.Count > 0)
                {
                    _db.Responsables.RemoveRange(existentes);
                }

                // Persistir cambios de la capacitación.
                _db.Capacitaciones.Update(entity);

                // Agregar nuevos responsables (con FK al mismo capacitacionId).
                var lista = nuevosResponsables.ToList();
                foreach (var r in lista)
                {
                    r.CapacitacionId = entity.Id;
                }
                if (lista.Count > 0)
                {
                    await _db.Responsables.AddRangeAsync(lista, ct);
                }

                await _db.SaveChangesAsync(ct);

                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }
            }
            catch
            {
                if (tx is not null)
                {
                    await tx.RollbackAsync(ct);
                }
                throw;
            }
            finally
            {
                if (tx is not null)
                {
                    await tx.DisposeAsync();
                }
            }
        });
    }

    public async Task UpdateAsync(Capacitacion entity, CancellationToken ct = default)
    {
        // Persiste escalares sin tocar la colección de responsables. La entidad puede venir
        // ya tracked (desde GetByIdWithResponsablesAsync) — en ese caso SaveChangesAsync basta.
        // Si vino detached, Update() asegura que EF la vea modificada.
        var entry = _db.Entry(entity);
        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
        {
            _db.Capacitaciones.Update(entity);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteLogicoAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Capacitaciones.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return;

        entity.Activo = false;
        entity.FechaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> GetMaxCodigoNumberAsync(CancellationToken ct = default)
    {
        // Incluye inactivos: el código es global y no se reutiliza aunque se borre lógicamente.
        // Se parsea el sufijo numérico en SQL cuando es posible para evitar traer toda la tabla.
        if (_db.Database.IsSqlServer())
        {
            // CAST del sufijo (a partir del carácter posterior a "CAP-PC-REG-", i.e. posición 12) a int.
            // COALESCE(MAX(...), 0) garantiza un resultado escalar y seguro si la tabla está vacía.
            var scalar = await _db.Database
                .SqlQuery<int>($@"
SELECT COALESCE(MAX(TRY_CAST(SUBSTRING(Codigo, {CodigoPrefix.Length + 1}, LEN(Codigo)) AS INT)), 0) AS Value
FROM dbo.Capacitacion")
                .ToListAsync(ct);

            return scalar.FirstOrDefault();
        }

        // Fallback (InMemory / SQLite tests): parseo en memoria — OK porque la tabla será acotada en tests.
        // TODO perf: evitar este fallback si alguna vez se usa un provider relacional distinto en producción.
        var codigos = await _db.Capacitaciones
            .AsNoTracking()
            .Select(c => c.Codigo)
            .ToListAsync(ct);

        int max = 0;
        foreach (var codigo in codigos)
        {
            if (string.IsNullOrWhiteSpace(codigo)) continue;
            if (!codigo.StartsWith(CodigoPrefix, StringComparison.Ordinal)) continue;
            var sufijo = codigo.Substring(CodigoPrefix.Length);
            if (int.TryParse(sufijo, out var n) && n > max)
            {
                max = n;
            }
        }
        return max;
    }
}
