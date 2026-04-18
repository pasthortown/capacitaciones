using Capacitaciones.Application.Dtos.Calificaciones;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Calificaciones;

/// <summary>
/// Fase 11: genera un link firmado (JWT role=Calificaciones) para que el admin lo entregue al
/// capacitador y este pueda registrar notas sin login. Espejo de
/// <see cref="PaseLista.GenerarLinkPaseListaUseCase"/>, pero exige además que la capacitación sea
/// <c>TipoCertificacion == Aprobacion</c>: no tiene sentido habilitar calificaciones en una
/// capacitación de Participación.
///
/// Como en los otros links firmados, no hay lista negra: cada invocación emite un token nuevo
/// que convive con los previos hasta expirar.
/// </summary>
public class GenerarLinkCalificacionesUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly IJwtTokenGenerator _jwt;

    public GenerarLinkCalificacionesUseCase(ICapacitacionRepository repo, IJwtTokenGenerator jwt)
    {
        _repo = repo;
        _jwt = jwt;
    }

    public async Task<LinkCalificacionesResponseDto> ExecuteAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        if (!entity.Activo)
        {
            throw new CapacitacionServiceException(
                "CAPACITACION_INACTIVA",
                "No se puede generar un link para una capacitación inactiva.");
        }

        if (entity.TipoCertificacion != TipoCertificacion.Aprobacion)
        {
            throw new CapacitacionServiceException(
                "CALIFICACIONES_NO_APLICA",
                "Solo las capacitaciones con TipoCertificacion=Aprobacion admiten calificaciones.");
        }

        var result = _jwt.GenerateCalificacionesToken(entity.Id);

        // URL relativa: el Frontend la resuelve contra window.location.origin al copiarla.
        var url = $"/capacitador/calificaciones?token={Uri.EscapeDataString(result.Token)}";

        return new LinkCalificacionesResponseDto
        {
            Url = url,
            Token = result.Token,
            ExpiresAt = result.ExpiresAt
        };
    }
}
