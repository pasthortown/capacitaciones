using System.Text.Json;
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
        TipoPregunta = p.TipoPregunta.ToString(),
        Opciones = ParseOpciones(p.OpcionesJson),
        Activo = p.Activo,
        FechaCreacion = p.FechaCreacion,
        FechaActualizacion = p.FechaActualizacion
    };

    /// <summary>
    /// Deserializa el JSON de opciones. Si el JSON es inválido o null, devuelve array vacío —
    /// nunca se lanza excepción desde aquí para no romper el listado por un registro corrupto.
    /// </summary>
    public static IReadOnlyList<string> ParseOpciones(string? opcionesJson)
    {
        if (string.IsNullOrWhiteSpace(opcionesJson)) return Array.Empty<string>();
        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(opcionesJson);
            return arr ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static string? SerializeOpciones(IReadOnlyList<string>? opciones)
    {
        if (opciones is null || opciones.Count == 0) return null;
        return JsonSerializer.Serialize(opciones);
    }
}
