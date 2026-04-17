using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de repositorio para <see cref="ConfiguracionNumeracion"/>. Siempre trabaja
/// con la fila única de Id = 1 (el seed inicial lo garantiza).
/// </summary>
public interface IConfiguracionNumeracionRepository
{
    Task<ConfiguracionNumeracion> GetAsync(CancellationToken ct = default);
    Task UpdateAsync(ConfiguracionNumeracion entity, CancellationToken ct = default);
}
