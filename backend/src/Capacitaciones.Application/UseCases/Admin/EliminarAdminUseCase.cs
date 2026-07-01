using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Admin;

/// <summary>
/// Caso de uso: quitar un usuario de red de la lista de permitidos (borrado físico).
/// Rechaza la auto-eliminación comparando el id solicitado con el del usuario autenticado.
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
                "No puedes quitarte a ti mismo de la lista de permitidos.");
        }

        var user = await _repo.GetByIdAsync(targetId, ct);
        if (user is null) return; // idempotente: ya no existe

        await _repo.DeleteAsync(targetId, ct);
    }
}
