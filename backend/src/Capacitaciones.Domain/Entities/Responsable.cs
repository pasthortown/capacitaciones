namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Responsable adicional (firmante secundario) de una capacitación. El capacitador
/// no se modela aquí: figura como primer firmante vía los campos <c>Capacitador</c>
/// y <c>FirmaCapacitador</c> de la propia <see cref="Capacitacion"/>.
/// </summary>
public class Responsable
{
    public Guid Id { get; set; }

    public Guid CapacitacionId { get; set; }
    public Capacitacion? Capacitacion { get; set; }

    public string Nombres { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;

    /// <summary>Firma (base64 del PNG/JPG). Requerida.</summary>
    public string Firma { get; set; } = string.Empty;

    /// <summary>Posición en que aparece en el certificado (0-based, único por capacitación).</summary>
    public int Orden { get; set; }
}
