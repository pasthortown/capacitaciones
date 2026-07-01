using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

public class AdminUserRepository : IAdminUserRepository
{
    private readonly AppDbContext _db;

    public AdminUserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<AdminUser?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        // Collation CI por defecto: la igualdad ya es case-insensitive en SQL Server.
        _db.AdminUsers.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<AdminUser?> GetByUsuarioRedAsync(string usuarioRed, CancellationToken ct = default) =>
        _db.AdminUsers.FirstOrDefaultAsync(u => u.UsuarioRed == usuarioRed, ct);

    public async Task<IReadOnlyList<AdminUser>> ListAsync(CancellationToken ct = default) =>
        await _db.AdminUsers
            .AsNoTracking()
            .Where(u => u.UsuarioRed != "")
            .OrderBy(u => u.UsuarioRed)
            .ToListAsync(ct);

    public Task<bool> AnyActivoAsync(CancellationToken ct = default) =>
        _db.AdminUsers.AnyAsync(u => u.Activo, ct);

    public async Task AddAsync(AdminUser user, CancellationToken ct = default)
    {
        await _db.AdminUsers.AddAsync(user, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AdminUser user, CancellationToken ct = default)
    {
        _db.AdminUsers.Update(user);
        await _db.SaveChangesAsync(ct);
    }
}
