using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Admin;

/// <summary>
/// Caso de uso: eliminación lógica de un administrador (<c>Activo = false</c>).
/// Rechaza la auto-eliminación comparando el id solicitado con el id del usuario
/// actualmente autenticado (provisto por el controlador).
/// </summary>
public class EliminarAdminUseCase
{
    private readonly IAdminUserRepository _repo;

    public EliminarAdminUseCase(IAdminUserRepository repo)
    {
        _repo = repo;
    }

    public async Task ExecuteAsync(Guid targetId, Guid currentUserId, CancellationToken ct = default)
    {
        if (targetId == currentUserId)
        {
            throw new AuthServiceException(
                "SELF_DELETE_FORBIDDEN",
                "No se permite que un administrador se elimine a sí mismo.");
        }

        var user = await _repo.GetByIdAsync(targetId, ct)
            ?? throw new AuthServiceException("NOT_FOUND", $"Administrador {targetId} no encontrado.");

        if (!user.Activo)
        {
            // Ya está desactivado: operación idempotente.
            return;
        }

        user.Activo = false;
        user.FechaActualizacion = DateTime.UtcNow;
        await _repo.UpdateAsync(user, ct);
    }
}
