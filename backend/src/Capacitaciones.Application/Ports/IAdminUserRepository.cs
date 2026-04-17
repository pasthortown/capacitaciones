using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de repositorio para <see cref="AdminUser"/>. Las operaciones de búsqueda por
/// email usan comparación case-insensitive (la collation por defecto de SQL Server es CI).
/// </summary>
public interface IAdminUserRepository
{
    Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AdminUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<AdminUser>> ListAsync(CancellationToken ct = default);
    Task<bool> AnyActivoAsync(CancellationToken ct = default);
    Task AddAsync(AdminUser user, CancellationToken ct = default);
    Task UpdateAsync(AdminUser user, CancellationToken ct = default);
}
