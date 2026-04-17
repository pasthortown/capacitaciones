using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Contrato genérico de repositorio para catálogos administrables que heredan de <see cref="CatalogoBase"/>.
/// </summary>
public interface ICatalogoRepository<T> where T : CatalogoBase
{
    Task<IEnumerable<T>> ListAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<T?> GetByNombreAsync(string nombre, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);

    /// <summary>
    /// Eliminación lógica: marca <see cref="CatalogoBase.Activo"/> = false.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
