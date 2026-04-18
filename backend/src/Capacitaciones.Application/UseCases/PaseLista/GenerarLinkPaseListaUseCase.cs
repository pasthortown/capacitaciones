using Capacitaciones.Application.Dtos.PaseLista;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.PaseLista;

/// <summary>
/// Fase 10: genera un link firmado (JWT role=PaseLista) para que el admin lo entregue al
/// capacitador y este pueda marcar asistencia sin login. Espejo de
/// <see cref="Capacitador.GenerarLinkCapacitadorUseCase"/>, pero emite token con rol distinto
/// para que un link filtrado solo habilite el flujo de pase de lista (no descripción/firma).
///
/// Como en los otros links firmados, no hay lista negra: cada invocación emite un token nuevo
/// que convive con los previos hasta expirar.
/// </summary>
public class GenerarLinkPaseListaUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly IJwtTokenGenerator _jwt;

    public GenerarLinkPaseListaUseCase(ICapacitacionRepository repo, IJwtTokenGenerator jwt)
    {
        _repo = repo;
        _jwt = jwt;
    }

    public async Task<LinkPaseListaResponseDto> ExecuteAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        if (!entity.Activo)
        {
            throw new CapacitacionServiceException(
                "CAPACITACION_INACTIVA",
                "No se puede generar un link para una capacitación inactiva.");
        }

        var result = _jwt.GeneratePaseListaToken(entity.Id);

        // URL relativa: el Frontend la resuelve contra window.location.origin al copiarla.
        var url = $"/capacitador/pase-lista?token={Uri.EscapeDataString(result.Token)}";

        return new LinkPaseListaResponseDto
        {
            Url = url,
            Token = result.Token,
            ExpiresAt = result.ExpiresAt
        };
    }
}
