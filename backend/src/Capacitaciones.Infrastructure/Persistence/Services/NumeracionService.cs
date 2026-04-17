using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Services;

/// <summary>
/// Implementación transaccional de <see cref="INumeracionService"/>. Toma el próximo número
/// con un <c>SELECT ... UPDLOCK, HOLDLOCK</c> para serializar accesos concurrentes y lo
/// formatea como <c>CAP-PC-REG-###</c>. Pensado para ser invocado desde la creación de
/// capacitaciones en Fase 3.
/// </summary>
public class NumeracionService : INumeracionService
{
    private const string Prefix = "CAP-PC-REG-";

    private readonly AppDbContext _db;

    public NumeracionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string> ClaimNextCodeAsync(CancellationToken ct = default)
    {
        // Si el caller ya inició una transacción (p.ej. CapacitacionRepository.AddAsync),
        // reutilízala en lugar de crear otro ExecutionStrategy — los strategies de SQL Server
        // no se pueden anidar y BeginTransactionAsync falla si ya hay una transacción activa.
        if (_db.Database.CurrentTransaction is not null)
        {
            return await ClaimCoreAsync(ct);
        }

        // Sin transacción externa: envolvemos en IExecutionStrategy + transacción Serializable
        // para reintentos transitorios y bloqueo concurrente.
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);
            var codigo = await ClaimCoreAsync(ct);
            await tx.CommitAsync(ct);
            return codigo;
        });
    }

    private async Task<string> ClaimCoreAsync(CancellationToken ct)
    {
        int current;
        if (_db.Database.IsSqlServer())
        {
            // Hint de bloqueo: evita que dos sesiones reciban el mismo número.
            current = await _db.ConfiguracionNumeracion
                .FromSqlRaw("SELECT * FROM dbo.ConfiguracionNumeracion WITH (UPDLOCK, HOLDLOCK) WHERE Id = 1")
                .Select(c => c.SiguienteNumero)
                .FirstAsync(ct);
        }
        else
        {
            var cfg = await _db.ConfiguracionNumeracion.FirstAsync(c => c.Id == 1, ct);
            current = cfg.SiguienteNumero;
        }

        if (current < 1 || current > 999)
        {
            throw new InvalidOperationException(
                $"Contador de numeración fuera de rango ({current}). Ajústelo vía /api/configuracion/numeracion.");
        }

        var entity = await _db.ConfiguracionNumeracion.FirstAsync(c => c.Id == 1, ct);
        entity.SiguienteNumero = current + 1;
        entity.UltimaActualizacion = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Prefix + current.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
    }
}
