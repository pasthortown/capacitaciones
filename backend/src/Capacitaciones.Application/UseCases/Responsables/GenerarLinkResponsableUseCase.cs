using Capacitaciones.Application.Dtos.Responsables;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Responsables;

/// <summary>
/// Genera un link firmado (JWT role=Responsable) para que el admin lo entregue al responsable
/// y éste cargue/actualice su perfil (incluida la firma).
///
/// Revocación: igual que los tokens de capacitador/inscripción, cada invocación emite un token
/// nuevo que convive con los anteriores hasta expirar. No hay lista negra.
/// </summary>
public class GenerarLinkResponsableUseCase
{
    private readonly IResponsableRepository _repo;
    private readonly IJwtTokenGenerator _jwt;

    public GenerarLinkResponsableUseCase(IResponsableRepository repo, IJwtTokenGenerator jwt)
    {
        _repo = repo;
        _jwt = jwt;
    }

    public async Task<LinkResponsableResponseDto> ExecuteAsync(Guid responsableId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(responsableId, ct)
            ?? throw new ResponsableNotFoundException(responsableId);

        if (!entity.Activo)
        {
            throw new ResponsableServiceException(
                "RESPONSABLE_INACTIVO",
                "No se puede generar un link para un responsable inactivo.");
        }

        var result = _jwt.GenerateResponsableToken(entity.Id);

        // URL relativa: el Frontend la resuelve contra window.location.origin al copiarla.
        var url = $"/responsable?token={Uri.EscapeDataString(result.Token)}";

        return new LinkResponsableResponseDto
        {
            Url = url,
            Token = result.Token,
            ExpiresAt = result.ExpiresAt
        };
    }
}
