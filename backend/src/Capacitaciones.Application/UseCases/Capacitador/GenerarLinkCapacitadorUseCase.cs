using Capacitaciones.Application.Dtos.Capacitador;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.Capacitador;

/// <summary>
/// Genera un link firmado (JWT role=Capacitador) para que el admin lo entregue al capacitador.
///
/// Revocación: no existe lista negra. Cada invocación emite un token NUEVO que convive con
/// cualquiera anterior hasta que todos expiren. Si un admin regenera el link porque sospecha
/// filtración, los tokens viejos seguirán siendo válidos hasta vencer (90 días por defecto).
/// TODO Fase futura: persistir un contador por capacitación para invalidar versiones anteriores.
/// </summary>
public class GenerarLinkCapacitadorUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly IJwtTokenGenerator _jwt;

    public GenerarLinkCapacitadorUseCase(ICapacitacionRepository repo, IJwtTokenGenerator jwt)
    {
        _repo = repo;
        _jwt = jwt;
    }

    public async Task<LinkCapacitadorResponseDto> ExecuteAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        if (!entity.Activo)
        {
            throw new CapacitacionServiceException(
                "CAPACITACION_INACTIVA",
                "No se puede generar un link para una capacitación inactiva.");
        }

        var result = _jwt.GenerateCapacitadorToken(entity.Id);

        // URL relativa: el Frontend la resuelve contra window.location.origin al copiarla.
        var url = $"/capacitador?token={Uri.EscapeDataString(result.Token)}";

        return new LinkCapacitadorResponseDto
        {
            Url = url,
            Token = result.Token,
            ExpiresAt = result.ExpiresAt
        };
    }
}
