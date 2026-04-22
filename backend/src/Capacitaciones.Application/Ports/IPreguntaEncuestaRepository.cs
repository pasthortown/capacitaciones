using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

public interface IPreguntaEncuestaRepository
{
    Task<IReadOnlyList<PreguntaEncuesta>> ListAsync(
        Guid? tipoActividadId,
        bool includeInactive,
        CancellationToken ct);

    Task<PreguntaEncuesta?> GetByIdAsync(Guid id, CancellationToken ct);

    Task AddAsync(PreguntaEncuesta entity, CancellationToken ct);

    Task UpdateAsync(PreguntaEncuesta entity, CancellationToken ct);

    /// <summary>Soft delete (marca Activo=false).</summary>
    Task SoftDeleteAsync(PreguntaEncuesta entity, CancellationToken ct);
}
