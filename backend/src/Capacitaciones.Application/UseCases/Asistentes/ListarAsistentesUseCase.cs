using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.Dtos.Inscripcion;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.Asistentes;

/// <summary>
/// Caso de uso admin: lista los asistentes inscritos a una capacitación. La vista del grid
/// consume esta lista y habilita la acción "descargar certificado" según el estado derivado.
///
/// Si la capacitación no existe → 404 vía <see cref="CapacitacionNotFoundException"/>.
/// Si está inactiva se devuelve la lista igual (el admin podría necesitar consultar historial).
/// </summary>
public class ListarAsistentesUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;

    public ListarAsistentesUseCase(ICapacitacionRepository capacitaciones, IAsistenteRepository asistentes)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
    }

    public async Task<IReadOnlyList<AsistenteSummaryDto>> ExecuteAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        // Validar que la capacitación exista — devolver 404 explícitamente si no.
        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        var items = await _asistentes.ListByCapacitacionAsync(capacitacion.Id, ct);

        return items
            .Select(a => new AsistenteSummaryDto
            {
                Id = a.Id,
                Nombres = a.Nombres,
                Apellidos = a.Apellidos,
                Identificacion = a.Identificacion,
                Email = a.EmailUsuario,
                Area = new CatalogoRefDto { Id = a.AreaId, Nombre = a.Area?.Nombre ?? string.Empty },
                FechaInscripcion = a.FechaInscripcion,
                // Fase 10 — el front usa estos dos campos para pintar el toggle de asistencia
                // en la tabla de listado (admin) sin hacer otro round-trip.
                EstadoAsistencia = a.EstadoAsistencia?.ToString(),
                FechaMarcacionAsistencia = a.FechaMarcacionAsistencia,
                // Fase 11 — calificación editable inline en la tabla admin. El front decide si
                // renderizarla según el TipoCertificacion de la capacitación (Aprobacion).
                Calificacion = a.Calificacion,
                // Envío de certificados — estado por asistente para la columna "Certificado".
                EstadoEnvioCertificado = a.EstadoEnvioCertificado?.ToString(),
                FechaEnvioCertificado = a.FechaEnvioCertificado,
                MensajeErrorEnvio = a.MensajeErrorEnvio
            })
            .ToList();
    }
}
