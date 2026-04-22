using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.PreguntasEncuesta;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Encuesta;

/// <summary>
/// Registra las respuestas de un asistente a la encuesta de una capacitación.
///
/// Reglas:
///  - La capacitación debe existir, estar activa y en estado Finalizada.
///  - El asistente se identifica por (capacitacionId, identificacion); si no existe → 404.
///  - No se permite responder dos veces (409 ENCUESTA_YA_RESPONDIDA).
///  - Todas las preguntas activas del tipo de actividad deben tener respuesta.
///  - Validación por tipo:
///     * SeleccionMultiple → valor debe ser una de las opciones.
///     * SiNo → valor debe ser "Si" | "No" (case-insensitive, normalizado a "Si"/"No").
///     * TextoLargo → se trimea; respuesta no vacía obligatoria (máx 2000 chars).
/// </summary>
public class SubmitEncuestaUseCase
{
    public const int MaxRespuestaTextoLength = 2000;

    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;
    private readonly IPreguntaEncuestaRepository _preguntas;
    private readonly IRespuestaEncuestaRepository _respuestas;

    public SubmitEncuestaUseCase(
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

    public async Task ExecuteAsync(
        Guid capacitacionId,
        SubmitEncuestaDto input,
        CancellationToken ct)
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

        var identificacion = (input.Identificacion ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(identificacion))
        {
            throw new EncuestaServiceException("IDENTIFICACION_REQUERIDA", "Ingresa tu cédula o identificación.");
        }

        var asistente = await _asistentes.GetByCapacitacionAndIdentificacionAsync(
            cap.Id, identificacion, ct);
        if (asistente is null)
        {
            throw new EncuestaServiceException(
                "ASISTENTE_NO_INSCRITO",
                "La identificación ingresada no pertenece a un asistente inscrito a esta capacitación.");
        }

        var yaRespondio = await _respuestas.AnyByAsistenteAsync(asistente.Id, ct);
        if (yaRespondio)
        {
            throw new EncuestaServiceException(
                "ENCUESTA_YA_RESPONDIDA",
                "Ya registramos tus respuestas para esta capacitación. ¡Gracias!");
        }

        var preguntas = await _preguntas.ListAsync(cap.TipoActividadId, includeInactive: false, ct);
        if (preguntas.Count == 0)
        {
            throw new EncuestaServiceException(
                "SIN_PREGUNTAS_CONFIGURADAS",
                "Aún no hay preguntas configuradas para este tipo de actividad.");
        }

        var respuestasIn = (input.Respuestas ?? Array.Empty<RespuestaItemDto>())
            .GroupBy(r => r.PreguntaEncuestaId)
            .ToDictionary(g => g.Key, g => g.First());

        var ahora = DateTime.UtcNow;
        var entidades = new List<RespuestaEncuesta>(preguntas.Count);

        foreach (var p in preguntas)
        {
            if (!respuestasIn.TryGetValue(p.Id, out var r))
            {
                throw new EncuestaServiceException(
                    "RESPUESTA_FALTANTE",
                    "Debes responder todas las preguntas antes de enviar.");
            }

            var respuestaNormalizada = NormalizarRespuesta(p, r.Respuesta);

            entidades.Add(new RespuestaEncuesta
            {
                Id = Guid.NewGuid(),
                AsistenteId = asistente.Id,
                PreguntaEncuestaId = p.Id,
                Respuesta = respuestaNormalizada,
                FechaRespuesta = ahora
            });
        }

        await _respuestas.AddRangeAsync(entidades, ct);
    }

    /// <summary>
    /// Valida y normaliza la respuesta según el tipo de pregunta. Lanza
    /// <see cref="EncuestaServiceException"/> con códigos específicos si el valor
    /// no es válido.
    /// </summary>
    private static string NormalizarRespuesta(PreguntaEncuesta pregunta, string? respuesta)
    {
        var raw = (respuesta ?? string.Empty).Trim();

        switch (pregunta.TipoPregunta)
        {
            case TipoPregunta.SeleccionMultiple:
            {
                if (string.IsNullOrEmpty(raw))
                {
                    throw new EncuestaServiceException(
                        "RESPUESTA_FALTANTE",
                        "Debes seleccionar una opción.");
                }
                var opciones = PreguntaEncuestaMapper.ParseOpciones(pregunta.OpcionesJson);
                var match = opciones.FirstOrDefault(
                    o => string.Equals(o, raw, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    throw new EncuestaServiceException(
                        "OPCION_INVALIDA",
                        "La opción seleccionada no está entre las permitidas.");
                }
                return match;
            }

            case TipoPregunta.SiNo:
            {
                // Aceptamos "Si" / "Sí" / "No" en cualquier capitalización.
                var lower = raw.ToLowerInvariant();
                if (lower is "si" or "sí") return "Si";
                if (lower == "no") return "No";
                throw new EncuestaServiceException(
                    "OPCION_INVALIDA",
                    "La respuesta debe ser 'Si' o 'No'.");
            }

            case TipoPregunta.TextoLargo:
            {
                if (string.IsNullOrEmpty(raw))
                {
                    throw new EncuestaServiceException(
                        "RESPUESTA_FALTANTE",
                        "Este campo es obligatorio.");
                }
                if (raw.Length > MaxRespuestaTextoLength)
                {
                    throw new EncuestaServiceException(
                        "RESPUESTA_DEMASIADO_LARGA",
                        $"El comentario no puede exceder {MaxRespuestaTextoLength} caracteres.");
                }
                return raw;
            }

            default:
                throw new EncuestaServiceException(
                    "TIPO_PREGUNTA_INVALIDO",
                    "Tipo de pregunta no soportado.");
        }
    }
}
