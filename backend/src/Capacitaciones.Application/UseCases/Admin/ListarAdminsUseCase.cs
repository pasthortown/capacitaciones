using Capacitaciones.Application.Dtos.Admin;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Admin;

/// <summary>Caso de uso: listar administradores (activos e inactivos).</summary>
public class ListarAdminsUseCase
{
    private readonly IAdminUserRepository _repo;

    public ListarAdminsUseCase(IAdminUserRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<AdminUserDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var items = await _repo.ListAsync(ct);
        return items.Select(CrearAdminUseCase.ToDto).ToList();
    }
}
