using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

public interface IRespuestaEncuestaRepository
{
    Task<bool> AnyByAsistenteAsync(Guid asistenteId, CancellationToken ct);

    Task<IReadOnlyList<RespuestaEncuesta>> ListByAsistenteAsync(
        Guid asistenteId,
        CancellationToken ct);

    Task<IReadOnlyList<RespuestaEncuesta>> ListByCapacitacionAsync(
        Guid capacitacionId,
        CancellationToken ct);

    Task AddRangeAsync(IEnumerable<RespuestaEncuesta> entities, CancellationToken ct);
}
