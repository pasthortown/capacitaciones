using Capacitaciones.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Services;

/// <summary>
/// Implementación transaccional de <see cref="IConvenioNumeracionService"/>. Toma el próximo
/// número con <c>SELECT ... UPDLOCK, HOLDLOCK</c> para serializar accesos concurrentes y lo
/// formatea como <c>GIC-EC-REG-###</c>. Análogo a <c>NumeracionService</c> (capacitaciones).
/// </summary>
public class ConvenioNumeracionService : IConvenioNumeracionService
{
    private readonly AppDbContext _db;

    public ConvenioNumeracionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(int numero, string codigo)> ClaimNextAsync(CancellationToken ct = default)
    {
        // Si el caller ya inició una transacción, reutilízala (los ExecutionStrategy de SQL Server
        // no se anidan).
        if (_db.Database.CurrentTransaction is not null)
        {
            return await ClaimCoreAsync(ct);
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);
            var result = await ClaimCoreAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    private async Task<(int numero, string codigo)> ClaimCoreAsync(CancellationToken ct)
    {
        int current;
        if (_db.Database.IsSqlServer())
        {
            current = await _db.ConvenioNumeracion
                .FromSqlRaw("SELECT * FROM dbo.ConvenioNumeracion WITH (UPDLOCK, HOLDLOCK) WHERE Id = 1")
                .Select(c => c.SiguienteNumero)
                .FirstAsync(ct);
        }
        else
        {
            var c = await _db.ConvenioNumeracion.FirstAsync(x => x.Id == 1, ct);
            current = c.SiguienteNumero;
        }

        if (current < 1)
        {
            throw new InvalidOperationException(
                $"Contador de numeración de convenios fuera de rango ({current}). Ajústelo vía /api/convenios/numeracion.");
        }

        var entity = await _db.ConvenioNumeracion.FirstAsync(x => x.Id == 1, ct);
        entity.SiguienteNumero = current + 1;
        entity.UltimaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (current, IConvenioNumeracionService.Format(current));
    }
}
