using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>Puerto de la fila única <see cref="ConfiguracionCorreo"/> (Id = 1). No hay seed: puede no existir.</summary>
public interface IConfiguracionCorreoRepository
{
    Task<ConfiguracionCorreo?> GetAsync(CancellationToken ct = default);

    /// <summary>Inserta la fila si no existe; si existe, guarda los cambios.</summary>
    Task UpsertAsync(ConfiguracionCorreo entity, CancellationToken ct = default);
}
