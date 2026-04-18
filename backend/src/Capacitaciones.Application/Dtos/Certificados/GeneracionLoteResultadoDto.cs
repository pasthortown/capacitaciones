namespace Capacitaciones.Application.Dtos.Certificados;

/// <summary>
/// Resumen devuelto por <c>GenerarCertificadosCapacitacionUseCase</c> al admin.
/// Se devuelve <c>200 OK</c> con este payload incluso si algunos (o todos) los
/// asistentes fallaron — el UI decide cómo presentar los errores.
/// </summary>
public class GeneracionLoteResultadoDto
{
    public int Total { get; set; }
    public int Emitidos { get; set; }

    /// <summary>
    /// Fase 12 — cantidad de asistentes omitidos por no ser elegibles para certificado
    /// (ausentes o sin marcación de asistencia). No cuentan como error.
    /// </summary>
    public int NoElegibles { get; set; }

    /// <summary>Fase 12 — detalle de los no-elegibles (asistenteId + motivo).</summary>
    public List<GeneracionLoteNoElegibleDto> NoElegiblesDetalle { get; set; } = new();

    public List<GeneracionLoteErrorDto> Errores { get; set; } = new();
}

public class GeneracionLoteErrorDto
{
    public Guid AsistenteId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}

/// <summary>
/// Fase 12 — información de un asistente omitido por no ser elegible. El UI lo muestra
/// separado de los errores (son estado esperado, no fallo de emisión).
/// </summary>
public class GeneracionLoteNoElegibleDto
{
    public Guid AsistenteId { get; set; }
    /// <summary>"AUSENTE" | "SIN_MARCAR"</summary>
    public string Motivo { get; set; } = string.Empty;
}
