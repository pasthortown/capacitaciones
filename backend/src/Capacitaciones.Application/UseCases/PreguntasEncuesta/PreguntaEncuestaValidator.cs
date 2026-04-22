using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.PreguntasEncuesta;

internal static class PreguntaEncuestaValidator
{
    public const int MaxTextoLength = 500;
    public const int MaxOpcionLength = 200;
    public const int MaxOpciones = 10;

    public static void ValidarTexto(string? texto)
    {
        var t = (texto ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(t))
        {
            throw new PreguntaEncuestaServiceException("TEXTO_VACIO", "El texto de la pregunta es obligatorio.");
        }
        if (t.Length > MaxTextoLength)
        {
            throw new PreguntaEncuestaServiceException(
                "TEXTO_DEMASIADO_LARGO",
                $"El texto de la pregunta no puede exceder {MaxTextoLength} caracteres.");
        }
    }

    public static TipoPregunta ParseTipoPregunta(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo))
        {
            throw new PreguntaEncuestaServiceException(
                "TIPO_PREGUNTA_REQUERIDO",
                "Debe indicar el tipo de pregunta.");
        }
        if (!Enum.TryParse<TipoPregunta>(tipo.Trim(), ignoreCase: true, out var parsed))
        {
            throw new PreguntaEncuestaServiceException(
                "TIPO_PREGUNTA_INVALIDO",
                "Tipo de pregunta inválido. Valores permitidos: SeleccionMultiple, TextoLargo, SiNo.");
        }
        return parsed;
    }

    /// <summary>
    /// Valida las opciones según el tipo y devuelve la lista normalizada
    /// (trim + eliminar vacíos + eliminar duplicados preservando orden).
    /// Para tipos distintos de SeleccionMultiple devuelve lista vacía — las opciones
    /// enviadas se ignoran silenciosamente para no rechazar payloads laxos.
    /// </summary>
    public static IReadOnlyList<string> ValidarYNormalizarOpciones(
        TipoPregunta tipoPregunta,
        IReadOnlyList<string>? opciones)
    {
        if (tipoPregunta != TipoPregunta.SeleccionMultiple)
        {
            return Array.Empty<string>();
        }

        if (opciones is null || opciones.Count == 0)
        {
            throw new PreguntaEncuestaServiceException(
                "OPCIONES_REQUERIDAS",
                "Debe ingresar al menos 2 opciones para una pregunta de selección múltiple.");
        }

        var normalizadas = new List<string>(opciones.Count);
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in opciones)
        {
            var v = (raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(v)) continue;
            if (v.Length > MaxOpcionLength)
            {
                throw new PreguntaEncuestaServiceException(
                    "OPCION_DEMASIADO_LARGA",
                    $"Cada opción no puede exceder {MaxOpcionLength} caracteres.");
            }
            if (vistos.Add(v))
            {
                normalizadas.Add(v);
            }
        }

        if (normalizadas.Count < 2)
        {
            throw new PreguntaEncuestaServiceException(
                "OPCIONES_INSUFICIENTES",
                "Debe ingresar al menos 2 opciones distintas.");
        }
        if (normalizadas.Count > MaxOpciones)
        {
            throw new PreguntaEncuestaServiceException(
                "OPCIONES_EXCESIVAS",
                $"No se permiten más de {MaxOpciones} opciones por pregunta.");
        }

        return normalizadas;
    }
}
