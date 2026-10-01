namespace Capacitaciones.Application.Dtos.Inscripcion;

/// <summary>
/// Respuesta de <c>GET /api/inscripcion/capacitacion/persona/{identificacion}</c>.
/// No incluye la firma: solo <see cref="TieneFirma"/>.
/// </summary>
public class PersonaInscritaLookupDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;

    /// <summary>Área guardada si sigue activa; null en otro caso.</summary>
    public Guid? AreaId { get; set; }

    /// <summary>Solo la parte local del correo (sin <c>@dos.com.ec</c>).</summary>
    public string EmailUsuario { get; set; } = string.Empty;

    public bool TieneFirma { get; set; }
}
