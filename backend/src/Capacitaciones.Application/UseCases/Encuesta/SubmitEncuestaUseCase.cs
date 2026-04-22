using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Encuesta;

/// <summary>
/// Registra las respuestas de un asistente a la encuesta de una capacitación.
///
/// Reglas:
///  - La capacitación debe existir, estar activa y en estado Finalizada.
///  - El asistente se identifica por (capacitacionId, identificacion); si no existe → 404.
///  - No se permite responder dos veces (409 ENCUESTA_YA_RESPONDIDA).
///  - Cada respuesta debe ser entero 1..5.
///  - Todas las preguntas activas del tipo de actividad deben tener respuesta (no se acepta parcial).
/// </summary>
public class SubmitEncuestaUseCase
{
    public const int ValorMinimo = 1;
    public const int ValorMaximo = 5;

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

        var preguntasValidasIds = preguntas.Select(p => p.Id).ToHashSet();
        var respuestasById = (input.Respuestas ?? Array.Empty<RespuestaItemDto>())
            .GroupBy(r => r.PreguntaEncuestaId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var p in preguntas)
        {
            if (!respuestasById.TryGetValue(p.Id, out var r))
            {
                throw new EncuestaServiceException(
                    "RESPUESTA_FALTANTE",
                    "Debes responder todas las preguntas antes de enviar.");
            }
            if (r.Valor < ValorMinimo || r.Valor > ValorMaximo)
            {
                throw new EncuestaServiceException(
                    "VALOR_FUERA_DE_RANGO",
                    $"Las respuestas deben ser enteros entre {ValorMinimo} y {ValorMaximo}.");
            }
        }

        // Si el cliente envía respuestas para preguntas que no aplican al tipo, las ignoramos.
        var ahora = DateTime.UtcNow;
        var entidades = respuestasById.Values
            .Where(r => preguntasValidasIds.Contains(r.PreguntaEncuestaId))
            .Select(r => new RespuestaEncuesta
            {
                Id = Guid.NewGuid(),
                AsistenteId = asistente.Id,
                PreguntaEncuestaId = r.PreguntaEncuestaId,
                Valor = r.Valor,
                FechaRespuesta = ahora
            })
            .ToList();

        await _respuestas.AddRangeAsync(entidades, ct);
    }
}
