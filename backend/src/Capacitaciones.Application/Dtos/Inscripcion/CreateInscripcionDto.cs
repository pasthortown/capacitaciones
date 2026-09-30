namespace Capacitaciones.Application.Dtos.Inscripcion;

/// <summary>
/// Payload que envía el formulario público para inscribir un asistente.
///
/// <see cref="EmailUsuario"/> contiene SOLO la parte local (el dominio <c>@dos.com.ec</c>
/// lo concatena el backend — ver decisión UX §7.3). La <see cref="Firma"/> es base64
/// (data URL o cadena pura); es requerida salvo que se use la firma registrada.
/// </summary>
public class CreateInscripcionDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Identificacion { get; set; } = string.Empty;
    public Guid AreaId { get; set; }

    /// <summary>Parte local del email corporativo (sin <c>@</c>).</summary>
    public string EmailUsuario { get; set; } = string.Empty;

    /// <summary>
    /// Firma base64 (data URL aceptada). Requerida salvo que <see cref="UsarFirmaRegistrada"/> sea true;
    /// en ese caso se ignora.
    /// </summary>
    public string? Firma { get; set; }

    /// <summary>
    /// true = usar la firma guardada en <c>PersonaInscrita</c> para esta identificación.
    /// </summary>
    public bool UsarFirmaRegistrada { get; set; }
}
