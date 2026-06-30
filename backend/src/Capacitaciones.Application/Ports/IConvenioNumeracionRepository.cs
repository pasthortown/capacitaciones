using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de repositorio para <see cref="ConvenioNumeracion"/>. Siempre trabaja con la fila
/// única de Id = 1 (el seed inicial lo garantiza).
/// </summary>
public interface IConvenioNumeracionRepository
{
    Task<ConvenioNumeracion> GetAsync(CancellationToken ct = default);
    Task UpdateAsync(ConvenioNumeracion entity, CancellationToken ct = default);
}
