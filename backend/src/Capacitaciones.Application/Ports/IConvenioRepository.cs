using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>Puerto de persistencia de Convenios. Baja lógica vía <c>Activo</c>.</summary>
public interface IConvenioRepository
{
    /// <summary>Listado filtrable. <paramref name="search"/> hace match parcial en título/tipo/cédula/nombre del colaborador.</summary>
    Task<IReadOnlyList<Convenio>> ListAsync(string? search, bool includeInactive, CancellationToken ct = default);

    Task<Convenio?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Convenios de un colaborador por cédula (la vigencia/devengo se calcula en aplicación).</summary>
    Task<IReadOnlyList<Convenio>> ListByCedulaAsync(string cedula, bool includeInactive, CancellationToken ct = default);

    Task AddAsync(Convenio entity, CancellationToken ct = default);
    Task UpdateAsync(Convenio entity, CancellationToken ct = default);

    /// <summary>Baja lógica idempotente.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
