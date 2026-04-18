namespace Capacitaciones.Application.Dtos.Certificados;

/// <summary>
/// Payload enviado al servicio <c>emisor_documentos</c> en <c>POST /emitir/reporte-asistencia</c>.
/// El shape se serializa en camelCase — el emisor espera nombres idénticos a la plantilla
/// <c>reporte_asistencia.html</c>.
///
/// A diferencia del certificado (uno por asistente), este reporte lleva toda la lista de
/// inscritos a la capacitación. El emisor decide visualmente qué hacer con cada fila
/// según <see cref="ReporteAsistenciaAsistenteDto.EstadoAsistencia"/>:
/// - "Presente": renderiza la firma si existe.
/// - "Ausente": pinta "Ausente" en rojo.
/// - <c>null</c>: celda de firma vacía (pendiente).
/// </summary>
public class ReporteAsistenciaRequest
{
    public ReporteAsistenciaCapacitacionDto Capacitacion { get; set; } = new();
    public List<ReporteAsistenciaAsistenteDto> Asistentes { get; set; } = new();
}

public class ReporteAsistenciaCapacitacionDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;

    /// <summary>Nombre del capacitador (texto libre del dominio).</summary>
    public string Capacitador { get; set; } = string.Empty;

    /// <summary>Firma base64 (data URL) del capacitador; opcional. Si viene null/vacío,
    /// el emisor deja el espacio en blanco en el cuadro "Firma del Capacitador".</summary>
    public string? FirmaCapacitadorBase64 { get; set; }

    /// <summary>ISO-8601 con Z (UTC) — el emisor formatea a es-EC con la TZ del contenedor.</summary>
    public string FechaInicio { get; set; } = string.Empty;

    /// <summary>Duración en horas (decimal, ej. 1.5 para 90 min).</summary>
    public decimal DuracionHoras { get; set; }

    /// <summary>"Departamento Capacitado" — opcional. Null/vacío → "—" en el PDF.</summary>
    public string? Departamento { get; set; }

    /// <summary>Descripción de la capacitación — opcional. Null/vacío → "—".</summary>
    public string? Descripcion { get; set; }
}

public class ReporteAsistenciaAsistenteDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Identificacion { get; set; } = string.Empty;
    public string? Area { get; set; }

    /// <summary>"Presente" | "Ausente" | <c>null</c>.</summary>
    public string? EstadoAsistencia { get; set; }

    /// <summary>
    /// Firma base64 (data URL). El backend la incluye SOLO si el asistente está Presente —
    /// requisito explícito: "la firma solo de los que sí asistieron al evento".
    /// Para Ausente / null esta propiedad viaja vacía aunque el asistente tenga firma
    /// persistida en BD.
    /// </summary>
    public string? FirmaBase64 { get; set; }
}

/// <summary>
/// Respuesta del emisor — idéntica al shape del certificado: <c>{ ruta: "/output/..." }</c>.
/// Reutilizamos <see cref="EmisionResultado"/> en el puerto; este DTO existe solo para
/// documentar la diferencia semántica.
/// </summary>
