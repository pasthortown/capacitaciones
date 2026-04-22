using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

public class RespuestaEncuestaRepository : IRespuestaEncuestaRepository
{
    private readonly AppDbContext _db;

    public RespuestaEncuestaRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> AnyByAsistenteAsync(Guid asistenteId, CancellationToken ct)
    {
        return _db.RespuestasEncuesta
            .AsNoTracking()
            .AnyAsync(r => r.AsistenteId == asistenteId, ct);
    }

    public async Task<IReadOnlyList<RespuestaEncuesta>> ListByAsistenteAsync(
        Guid asistenteId,
        CancellationToken ct)
    {
        return await _db.RespuestasEncuesta
            .AsNoTracking()
            .Include(r => r.PreguntaEncuesta)
            .Where(r => r.AsistenteId == asistenteId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RespuestaEncuesta>> ListByCapacitacionAsync(
        Guid capacitacionId,
        CancellationToken ct)
    {
        return await _db.RespuestasEncuesta
            .AsNoTracking()
            .Include(r => r.PreguntaEncuesta)
            .Include(r => r.Asistente)
            .Where(r => r.Asistente!.CapacitacionId == capacitacionId)
            .ToListAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<RespuestaEncuesta> entities, CancellationToken ct)
    {
        await _db.RespuestasEncuesta.AddRangeAsync(entities, ct);
        await _db.SaveChangesAsync(ct);
    }
}
