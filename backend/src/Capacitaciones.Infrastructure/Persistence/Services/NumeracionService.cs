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
        // IExecutionStrategy maneja reintentos transitorios de SQL Server sin romper la transacción.
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);

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
                // Fallback para proveedores no SQL Server (tests InMemory, etc.).
                var cfg = await _db.ConfiguracionNumeracion.FirstAsync(c => c.Id == 1, ct);
                current = cfg.SiguienteNumero;
            }

            if (current < 1 || current > 999)
            {
                throw new InvalidOperationException(
                    $"Contador de numeración fuera de rango ({current}). Ajústelo vía /api/configuracion/numeracion.");
            }

            var siguiente = current + 1;

            // Update por Id (evita cargar y trackear la entidad si ya fue leída vía SQL crudo).
            var entity = await _db.ConfiguracionNumeracion.FirstAsync(c => c.Id == 1, ct);
            entity.SiguienteNumero = siguiente;
            entity.UltimaActualizacion = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            return Prefix + current.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
        });
    }
}
