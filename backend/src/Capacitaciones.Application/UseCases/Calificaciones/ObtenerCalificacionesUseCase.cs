using Capacitaciones.Application.Dtos.Calificaciones;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Calificaciones;

/// <summary>
/// Fase 11: devuelve la información necesaria para renderizar la pantalla de calificaciones
/// (admin o capacitador público) — resumen de la capacitación + lista de asistentes
/// <c>Presentes</c> ordenada alfabéticamente por <c>Apellidos</c> y luego <c>Nombres</c>
/// (case-insensitive, cultura invariante).
///
/// Validaciones:
///   - Capacitación inexistente → <see cref="CapacitacionNotFoundException"/> (404).
///   - Capacitación inactiva → <see cref="CapacitadorForbiddenException"/> (403).
///   - <c>TipoCertificacion != Aprobacion</c> → <c>CALIFICACIONES_NO_APLICA</c> (409).
/// </summary>
public class ObtenerCalificacionesUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;

    public ObtenerCalificacionesUseCase(
        ICapacitacionRepository capacitaciones,
        IAsistenteRepository asistentes)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
    }

    public async Task<CalificacionesDto> ExecuteAsync(Guid capacitacionId, CancellationToken ct = default)
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

        var asistentes = await _asistentes.ListByCapacitacionAsync(capacitacion.Id, ct);

        // Filtro Fase 11: solo Presentes. No tiene sentido calificar a quien faltó ni a quien
        // nunca fue marcado — es responsabilidad del capacitador cerrar el pase de lista antes.
        var ordenados = asistentes
            .Where(a => a.EstadoAsistencia == EstadoAsistencia.Presente)
            .OrderBy(a => a.Apellidos ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Nombres ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(a => new CalificacionesAsistenteDto
            {
                Id = a.Id,
                Nombres = a.Nombres,
                Apellidos = a.Apellidos,
                Identificacion = a.Identificacion,
                EstadoAsistencia = a.EstadoAsistencia?.ToString(),
                Calificacion = a.Calificacion
            })
            .ToList();

        return new CalificacionesDto
        {
            Capacitacion = new CalificacionesCapacitacionDto
            {
                Id = capacitacion.Id,
                Codigo = capacitacion.Codigo,
                Tema = capacitacion.Tema,
                FechaHoraInicio = capacitacion.FechaHoraInicio,
                DuracionMinutos = capacitacion.DuracionMinutos,
                Estado = CapacitacionEstadoCalculator.Calcular(capacitacion),
                TipoCertificacion = capacitacion.TipoCertificacion.ToString(),
                PuntajeMinimo = capacitacion.PuntajeMinimo
            },
            Asistentes = ordenados
        };
    }
}
