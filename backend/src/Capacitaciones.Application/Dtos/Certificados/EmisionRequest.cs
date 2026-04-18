namespace Capacitaciones.Application.Dtos.Certificados;

/// <summary>
/// Payload enviado al servicio <c>emisor_documentos</c> (Node + Puppeteer) en el endpoint
/// <c>POST /emitir/certificado</c>. El shape está definido por el contrato fijado en la Fase 6
/// (ver <c>instrucciones.md §7.6</c>) y debe mantenerse en camelCase al serializar.
/// </summary>
public class EmisionRequest
{
    public EmisionCapacitacionDto Capacitacion { get; set; } = new();
    public EmisionAsistenteDto Asistente { get; set; } = new();
    public List<EmisionFirmanteDto> Firmantes { get; set; } = new();

    /// <summary>
    /// Fase 12 — etiqueta efectiva del certificado: "Aprobacion" | "Asistencia" | "Participacion".
    /// Puede diferir de <c>Capacitacion.TipoCertificacion</c>: una capacitación Aprobacion con
    /// calificación &lt; puntaje mínimo emite un certificado "Asistencia" sin cambiar el tipo base.
    /// </summary>
    public string CertificadoEfectivo { get; set; } = string.Empty;
}

/// <summary>Datos de la capacitación necesarios para renderizar el certificado.</summary>
public class EmisionCapacitacionDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;

    /// <summary>Nombre del catálogo (ej. "Curso", "Taller"). Se usa para armar el texto del certificado.</summary>
    public string TipoActividad { get; set; } = string.Empty;

    /// <summary>"Participacion" | "Aprobacion" (ToString del enum).</summary>
    public string TipoCertificacion { get; set; } = string.Empty;

    /// <summary>ISO-8601 con Z (UTC) — el emisor hace el formateo visual.</summary>
    public string FechaInicio { get; set; } = string.Empty;

    /// <summary>Duración en horas (permite decimales, ej. 1.5).</summary>
    public decimal DuracionHoras { get; set; }

    /// <summary>Fase 12 — puntaje mínimo de aprobación (0–10). Null cuando TipoCertificacion != Aprobacion.</summary>
    public decimal? PuntajeMinimo { get; set; }

    /// <summary>
    /// Fase 12 — ruta local del logo dentro del volumen compartido <c>/imagen_capacitaciones</c>
    /// (ej. <c>/imagen_capacitaciones/&lt;guid&gt;.png</c>). El emisor lo lee del disco y lo embebe
    /// como data URL en el HTML. Null si la capacitación no tiene logo.
    /// </summary>
    public string? LogoPathLocal { get; set; }
}

public class EmisionAsistenteDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Identificacion { get; set; } = string.Empty;

    /// <summary>Fase 12 — calificación 0–10. Solo se incluye si la capacitación es Aprobacion y el asistente está Presente.</summary>
    public decimal? Calificacion { get; set; }
}

/// <summary>
/// Firmante del certificado: el capacitador siempre es el primero, luego los responsables
/// en el <c>Orden</c> configurado en la capacitación.
/// </summary>
public class EmisionFirmanteDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string FirmaBase64 { get; set; } = string.Empty;
}
