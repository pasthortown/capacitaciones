namespace Capacitaciones.Application.Dtos.Inscripcion;

/// <summary>
/// Payload que envía el formulario público para inscribir un asistente.
///
/// <see cref="EmailUsuario"/> contiene SOLO la parte local (el dominio <c>@dos.com.ec</c>
/// lo concatena el backend — ver decisión UX §7.3). La <see cref="Firma"/> es base64
/// (data URL o cadena pura) y es requerida.
/// </summary>
public class CreateInscripcionDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Identificacion { get; set; } = string.Empty;
    public Guid AreaId { get; set; }

    /// <summary>Parte local del email corporativo (sin <c>@</c>).</summary>
    public string EmailUsuario { get; set; } = string.Empty;

    /// <summary>Firma base64 (data URL aceptada).</summary>
    public string Firma { get; set; } = string.Empty;
}
