namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Registro de una persona que se inscribió alguna vez por la página pública de inscripción,
/// independiente de cada capacitación. Clave natural: <see cref="Identificacion"/> (única).
///
/// Se crea o actualiza en cada inscripción (<c>InscribirAsistenteUseCase</c>) y alimenta el
/// autocompletado por cédula del formulario público. La <see cref="Firma"/> se reusa en nuevas
/// inscripciones pero <b>nunca</b> se devuelve por la API pública.
/// </summary>
public class PersonaInscrita
{
    public Guid Id { get; set; }

    /// <summary>Cédula o pasaporte, con Trim y sin normalizar mayúsculas (mismo criterio que <see cref="Asistente"/>).</summary>
    public string Identificacion { get; set; } = string.Empty;

    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;

    /// <summary>Última área usada. Nullable por si el área deja de existir en el catálogo.</summary>
    public Guid? AreaId { get; set; }
    public Area? Area { get; set; }

    /// <summary>Email completo ya con sufijo <c>@dos.com.ec</c>.</summary>
    public string EmailUsuario { get; set; } = string.Empty;

    /// <summary>Firma base64 (data URL). Null = sin firma guardada.</summary>
    public string? Firma { get; set; }

    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}
