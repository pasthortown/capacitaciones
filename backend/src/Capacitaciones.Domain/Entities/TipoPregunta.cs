namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Tipo de una pregunta de la encuesta de satisfacción. Determina cómo la pinta
/// el frontend y cómo se valida/almacena la respuesta.
/// </summary>
public enum TipoPregunta
{
    /// <summary>Opciones fijas definidas por el admin. Respuesta = texto de la opción elegida.</summary>
    SeleccionMultiple = 0,

    /// <summary>Campo de texto libre. Respuesta = comentario.</summary>
    TextoLargo = 1,

    /// <summary>Dos opciones fijas "Sí" / "No". Respuesta = "Si" | "No".</summary>
    SiNo = 2
}
