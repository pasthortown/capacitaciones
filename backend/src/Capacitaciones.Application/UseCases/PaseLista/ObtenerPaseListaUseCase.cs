using Capacitaciones.Application.Dtos.PaseLista;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;

namespace Capacitaciones.Application.UseCases.PaseLista;

/// <summary>
/// Fase 10: devuelve la información necesaria para renderizar la pantalla de pase de lista
/// (admin o capacitador público) — resumen de la capacitación + lista de asistentes ordenada
/// alfabéticamente por <c>Apellidos</c> y luego <c>Nombres</c> (case-insensitive, cultura invariante).
///
/// Validaciones:
///   - Capacitación inexistente → <see cref="CapacitacionNotFoundException"/> (404).
///   - Capacitación inactiva → <see cref="CapacitadorForbiddenException"/> (403).
/// </summary>
public class ObtenerPaseListaUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;

    public ObtenerPaseListaUseCase(
        ICapacitacionRepository capacitaciones,
        IAsistenteRepository asistentes)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
    }

    public async Task<PaseListaDto> ExecuteAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        if (!capacitacion.Activo)
        {
            throw new CapacitadorForbiddenException("La capacitación está inactiva.");
        }

        var asistentes = await _asistentes.ListByCapacitacionAsync(capacitacion.Id, ct);

        // Orden alfabético por Apellidos → Nombres, case-insensitive. OrdinalIgnoreCase
        // evita sorpresas regionales (tilde vs no-tilde) y coincide con el comportamiento
        // que espera la pantalla pública: recorrer asistente por asistente en orden natural.
        var ordenados = asistentes
            .OrderBy(a => a.Apellidos ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Nombres ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(a => new PaseListaAsistenteDto
            {
                Id = a.Id,
                Nombres = a.Nombres,
                Apellidos = a.Apellidos,
                Identificacion = a.Identificacion,
                EstadoAsistencia = a.EstadoAsistencia?.ToString(),
                FechaMarcacionAsistencia = a.FechaMarcacionAsistencia
            })
            .ToList();

        return new PaseListaDto
        {
            Capacitacion = new PaseListaCapacitacionDto
            {
                Id = capacitacion.Id,
                Codigo = capacitacion.Codigo,
                Tema = capacitacion.Tema,
                FechaHoraInicio = capacitacion.FechaHoraInicio,
                DuracionMinutos = capacitacion.DuracionMinutos,
                Estado = CapacitacionEstadoCalculator.Calcular(capacitacion)
            },
            Asistentes = ordenados
        };
    }
}
