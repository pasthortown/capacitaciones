using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.PreguntasEncuesta;

internal static class PreguntaEncuestaMapper
{
    public static PreguntaEncuestaDto ToDto(PreguntaEncuesta p) => new()
    {
        Id = p.Id,
        TipoActividadId = p.TipoActividadId,
        TipoActividadNombre = p.TipoActividad?.Nombre ?? string.Empty,
        Texto = p.Texto,
        Activo = p.Activo,
        FechaCreacion = p.FechaCreacion,
        FechaActualizacion = p.FechaActualizacion
    };
}
