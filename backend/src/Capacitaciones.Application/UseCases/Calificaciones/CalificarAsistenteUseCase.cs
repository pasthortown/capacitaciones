using Capacitaciones.Application.Dtos.Calificaciones;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Application.UseCases.PaseLista;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Calificaciones;

/// <summary>
/// Fase 11: registra (o limpia) la calificación de un asistente.
/// Compartido entre el endpoint público con token Calificaciones y el endpoint admin.
/// El caller valida la policy HTTP; el use case valida:
///   - la capacitación es <c>Aprobacion</c>.
///   - el asistente pertenece a la capacitación indicada.
///   - el asistente está <c>Presente</c> (no se califica a ausentes ni a quien no fue marcado).
///   - la calificación, si no es null, está en [0..10].
/// </summary>
public class CalificarAsistenteUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;

    public CalificarAsistenteUseCase(
        ICapacitacionRepository capacitaciones,
        IAsistenteRepository asistentes)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
    }

    public async Task<CalificacionResponseDto> ExecuteAsync(
        Guid capacitacionId,
        Guid asistenteId,
        decimal? calificacion,
        CancellationToken ct = default)
    {
        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        if (!capacitacion.Activo)
        {
            throw new CapacitadorForbiddenException("La capacitación está inactiva.");
        }

        if (capacitacion.TipoCertificacion != TipoCertificacion.Aprobacion)
        {
            throw new CapacitacionServiceException(
                "CALIFICACIONES_NO_APLICA",
                "Solo las capacitaciones con TipoCertificacion=Aprobacion admiten calificaciones.");
        }

        var asistente = await _asistentes.GetByIdAsync(asistenteId, ct)
            ?? throw new AsistenteNotFoundException(asistenteId);

        // Defensa en profundidad: un token de calificaciones trae la capacitación en su claim.
        // Si el asistente pertenece a otra capacitación, tratarlo como 404 (mismo criterio que
        // MarcarAsistenciaUseCase — el controller público ya aseguró la autenticación).
        if (asistente.CapacitacionId != capacitacion.Id)
        {
            throw new AsistenteNotFoundException(asistenteId);
        }

        if (asistente.EstadoAsistencia != EstadoAsistencia.Presente)
        {
            throw new CapacitacionServiceException(
                "ASISTENTE_NO_PRESENTE",
                "Solo se puede calificar a asistentes marcados como Presente.");
        }

        if (calificacion.HasValue)
        {
            if (calificacion.Value < 0m || calificacion.Value > 10m)
            {
                throw new CapacitacionServiceException(
                    "CALIFICACION_FUERA_DE_RANGO",
                    "La calificación debe estar entre 0 y 10.");
            }
        }

        asistente.Calificacion = calificacion;

        await _asistentes.UpdateAsync(asistente, ct);

        return new CalificacionResponseDto
        {
            Id = asistente.Id,
            Calificacion = asistente.Calificacion
        };
    }
}
