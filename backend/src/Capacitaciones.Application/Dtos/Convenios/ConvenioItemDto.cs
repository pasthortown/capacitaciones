namespace Capacitaciones.Application.Dtos.Convenios;

/// <summary>Ítem de costo de un convenio (material, examen, etc.).</summary>
public class ConvenioItemDto
{
    public Guid Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public bool Devengable { get; set; }
    public string? Observacion { get; set; }
}

/// <summary>Ítem de costo en el payload de alta/edición (sin Id: se reemplazan en bloque).</summary>
public class ConvenioItemRequest
{
    public string? Tipo { get; set; }
    public decimal Valor { get; set; }
    public bool Devengable { get; set; } = true;
    public string? Observacion { get; set; }
}

/// <summary>Anexo de un convenio (metadata; el binario se descarga aparte).</summary>
public class ConvenioAnexoDto
{
    public Guid Id { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public string? ContentType { get; set; }
    public DateTime FechaCreacion { get; set; }
}
