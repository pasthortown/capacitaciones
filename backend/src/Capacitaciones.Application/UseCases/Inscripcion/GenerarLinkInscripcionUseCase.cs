using Capacitaciones.Application.Dtos.Inscripcion;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.Inscripcion;

/// <summary>
/// Genera un link firmado (JWT role=Inscripcion) para que el admin lo distribuya a los
/// asistentes potenciales (Fase 5).
///
/// Decisión: el admin puede generar el link aún si la capacitación está <c>Finalizada</c>
/// porque el back-office podría querer previsualizar el formulario o reutilizar el link;
/// la pantalla pública es la que bloquea con 409 cuando el estado es Finalizada al
/// momento de cargarla. Solo se valida que la capacitación esté <b>activa</b>.
///
/// Revocación: no hay lista negra — cada invocación emite un token nuevo que convive con
/// los anteriores hasta expirar. Mismo compromiso que el token de capacitador (Fase 4).
/// </summary>
public class GenerarLinkInscripcionUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly IJwtTokenGenerator _jwt;

    public GenerarLinkInscripcionUseCase(ICapacitacionRepository repo, IJwtTokenGenerator jwt)
    {
        _repo = repo;
        _jwt = jwt;
    }

    public async Task<LinkInscripcionResponseDto> ExecuteAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        if (!entity.Activo)
        {
            throw new CapacitacionServiceException(
                "CAPACITACION_INACTIVA",
                "No se puede generar un link de inscripción para una capacitación inactiva.");
        }

        var result = _jwt.GenerateInscripcionToken(entity.Id);

        // URL relativa — el frontend la resuelve contra window.location.origin al copiarla.
        var url = $"/inscripcion?token={Uri.EscapeDataString(result.Token)}";

        return new LinkInscripcionResponseDto
        {
            Url = url,
            Token = result.Token,
            ExpiresAt = result.ExpiresAt
        };
    }
}
