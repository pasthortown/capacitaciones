using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.PreguntasEncuesta;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Encuesta;

/// <summary>
/// Admin — agrega las respuestas de la encuesta de una capacitación para mostrar
/// en el dashboard y pasarle al generador de PDF.
/// </summary>
public class ObtenerResultadosEncuestaUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;
    private readonly IPreguntaEncuestaRepository _preguntas;
    private readonly IRespuestaEncuestaRepository _respuestas;

    public ObtenerResultadosEncuestaUseCase(
        ICapacitacionRepository capacitaciones,
        IAsistenteRepository asistentes,
        IPreguntaEncuestaRepository preguntas,
        IRespuestaEncuestaRepository respuestas)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
        _preguntas = preguntas;
        _respuestas = respuestas;
    }

    public async Task<ResultadoEncuestaDto> ExecuteAsync(
        Guid capacitacionId,
        CancellationToken ct)
    {
        var cap = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new EncuestaServiceException(
                "CAPACITACION_NOT_FOUND", "La capacitación no existe.");

        // Preguntas activas del tipo de actividad de la capacitación.
        var preguntas = await _preguntas.ListAsync(cap.TipoActividadId, includeInactive: false, ct);

        // Respuestas de todos los asistentes de la capacitación.
        var respuestas = await _respuestas.ListByCapacitacionAsync(capacitacionId, ct);

        var totalAsistentes = await _asistentes.CountByCapacitacionAsync(capacitacionId, ct);

        // Respondieron = asistentes distintos con al menos una respuesta.
        var totalRespondieron = respuestas
            .Select(r => r.AsistenteId)
            .Distinct()
            .Count();

        // Agrupar respuestas por pregunta para procesar eficientemente.
        var respuestasPorPregunta = respuestas
            .GroupBy(r => r.PreguntaEncuestaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var preguntasResumen = preguntas.Select(p =>
            BuildResumen(p, respuestasPorPregunta.TryGetValue(p.Id, out var lst) ? lst : new List<RespuestaEncuesta>())
        ).ToArray();

        return new ResultadoEncuestaDto
        {
            CapacitacionId = cap.Id,
            Codigo = cap.Codigo,
            Tema = cap.Tema,
            Capacitador = cap.Capacitador,
            FechaHoraInicio = cap.FechaHoraInicio,
            DuracionMinutos = cap.DuracionMinutos,
            TipoActividadNombre = cap.TipoActividad?.Nombre ?? string.Empty,
            TotalAsistentes = totalAsistentes,
            TotalRespondieron = totalRespondieron,
            Preguntas = preguntasResumen
        };
    }

    private static ResultadoPreguntaDto BuildResumen(
        PreguntaEncuesta pregunta,
        List<RespuestaEncuesta> respuestas)
    {
        var dto = new ResultadoPreguntaDto
        {
            Id = pregunta.Id,
            Texto = pregunta.Texto,
            TipoPregunta = pregunta.TipoPregunta.ToString(),
            TotalRespuestas = respuestas.Count
        };

        switch (pregunta.TipoPregunta)
        {
            case TipoPregunta.SeleccionMultiple:
            {
                var opciones = PreguntaEncuestaMapper.ParseOpciones(pregunta.OpcionesJson);
                dto.Opciones = opciones;
                dto.ConteoOpciones = BuildConteoOpciones(opciones, respuestas);
                break;
            }
            case TipoPregunta.SiNo:
            {
                var opciones = new[] { "Si", "No" };
                dto.Opciones = opciones;
                dto.ConteoOpciones = BuildConteoOpciones(opciones, respuestas);
                break;
            }
            case TipoPregunta.TextoLargo:
            {
                dto.RespuestasTexto = respuestas
                    .OrderBy(r => r.FechaRespuesta)
                    .Select(r => new RespuestaTextoDto
                    {
                        Asistente = BuildAsistenteNombre(r.Asistente),
                        Texto = r.Respuesta ?? string.Empty,
                        FechaRespuesta = r.FechaRespuesta
                    })
                    .ToArray();
                break;
            }
        }

        return dto;
    }

    private static IReadOnlyList<ConteoOpcionDto> BuildConteoOpciones(
        IReadOnlyList<string> opciones,
        List<RespuestaEncuesta> respuestas)
    {
        // Garantiza que todas las opciones declaradas aparezcan con su conteo (aunque sea 0).
        // Match case-insensitive para tolerar variaciones menores en capitalización.
        var mapCount = opciones.ToDictionary(o => o, _ => 0, StringComparer.OrdinalIgnoreCase);
        int otras = 0;
        foreach (var r in respuestas)
        {
            var val = (r.Respuesta ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(val)) continue;
            if (mapCount.ContainsKey(val))
            {
                mapCount[val]++;
            }
            else
            {
                otras++;
            }
        }

        var resultado = opciones
            .Select(o => new ConteoOpcionDto { Opcion = o, Conteo = mapCount[o] })
            .ToList();
        if (otras > 0)
        {
            resultado.Add(new ConteoOpcionDto { Opcion = "Otras", Conteo = otras });
        }
        return resultado;
    }

    private static string BuildAsistenteNombre(Asistente? a)
    {
        if (a is null) return "Asistente";
        var nombres = a.Nombres ?? string.Empty;
        var apellidos = a.Apellidos ?? string.Empty;
        var n = $"{apellidos} {nombres}".Trim();
        return string.IsNullOrWhiteSpace(n) ? (a.Identificacion ?? "Asistente") : n;
    }
}
