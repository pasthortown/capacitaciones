using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de persistencia de los colaboradores <b>externos a DOS</b> (los internos viven en
/// ControlTareas y se consultan vía <see cref="IControlTareasColaboradoresClient"/>).
/// Baja lógica vía <c>Activo</c>; clave natural <c>Cedula</c>.
/// </summary>
public interface IColaboradorRepository
{
    /// <summary>Listado filtrable. Si <paramref name="includeInactive"/> es false solo activos.
    /// <paramref name="search"/> hace match parcial en cédula/nombre/correo/cargo/área.</summary>
    Task<IReadOnlyList<Colaborador>> ListAsync(string? search, bool includeInactive, CancellationToken ct = default);

    Task<Colaborador?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Colaborador?> GetByCedulaAsync(string cedula, CancellationToken ct = default);

    /// <summary>True si ya existe un externo (activo o inactivo) con esa cédula.</summary>
    Task<bool> ExistsByCedulaAsync(string cedula, CancellationToken ct = default);

    Task AddAsync(Colaborador entity, CancellationToken ct = default);

    Task UpdateAsync(Colaborador entity, CancellationToken ct = default);

    /// <summary>Baja lógica idempotente (<c>Activo=false</c> + <c>FechaActualizacion</c>).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
