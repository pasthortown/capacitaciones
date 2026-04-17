namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Asistente inscrito a una capacitación vía link firmado público (Fase 5).
///
/// El <see cref="EmailUsuario"/> se persiste con el dominio corporativo ya concatenado
/// (<c>{usuario}@dos.com.ec</c>) — el formulario público solo captura la parte local.
/// La <see cref="Firma"/> es base64 (data URL o cadena pura) — el SignaturePad del frontend
/// admite dibujar o cargar archivo.
/// </summary>
public class Asistente
{
    public Guid Id { get; set; }

    public Guid CapacitacionId { get; set; }
    public Capacitacion? Capacitacion { get; set; }

    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;

    /// <summary>Identificación (cédula, pasaporte). Único por capacitación (ver índice en EF).</summary>
    public string Identificacion { get; set; } = string.Empty;

    public Guid AreaId { get; set; }
    public Area? Area { get; set; }

    /// <summary>Email completo ya con sufijo <c>@dos.com.ec</c>.</summary>
    public string EmailUsuario { get; set; } = string.Empty;

    /// <summary>Firma base64 (PNG/JPG data URL o cadena pura). Requerida.</summary>
    public string Firma { get; set; } = string.Empty;

    public DateTime FechaInscripcion { get; set; }
}
