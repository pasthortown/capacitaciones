using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.Encuesta;

/// <summary>
/// Carga los datos que necesita la página pública de encuesta:
///  - header de la capacitación (solo si ya está Finalizada y activa),
///  - preguntas activas del tipo de actividad de la capacitación.
/// </summary>
public class ObtenerEncuestaPublicaUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IPreguntaEncuestaRepository _preguntas;

    public ObtenerEncuestaPublicaUseCase(
        ICapacitacionRepository capacitaciones,
        IPreguntaEncuestaRepository preguntas)
    {
        _capacitaciones = capacitaciones;
        _preguntas = preguntas;
    }

    public async Task<EncuestaPublicaDto> ExecuteAsync(Guid capacitacionId, CancellationToken ct)
    {
        var cap = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new EncuestaServiceException("CAPACITACION_NOT_FOUND", "La capacitación no existe.");

        if (!cap.Activo)
        {
            throw new EncuestaServiceException("CAPACITACION_NOT_FOUND", "La capacitación no existe.");
        }

        var estado = CapacitacionEstadoCalculator.Calcular(cap);
        if (estado != "Finalizada")
        {
            throw new EncuestaServiceException(
                "CAPACITACION_NO_FINALIZADA",
                "La encuesta solo está disponible una vez finalizada la capacitación.");
        }

        var preguntas = await _preguntas.ListAsync(cap.TipoActividadId, includeInactive: false, ct);

        return new EncuestaPublicaDto
        {
            CapacitacionId = cap.Id,
            Codigo = cap.Codigo,
            Tema = cap.Tema,
            Capacitador = cap.Capacitador,
            FechaHoraInicio = cap.FechaHoraInicio,
            DuracionMinutos = cap.DuracionMinutos,
            TipoActividadNombre = cap.TipoActividad?.Nombre ?? string.Empty,
            Preguntas = preguntas
                .Select(p => new EncuestaPreguntaDto { Id = p.Id, Texto = p.Texto })
                .ToArray()
        };
    }
}
