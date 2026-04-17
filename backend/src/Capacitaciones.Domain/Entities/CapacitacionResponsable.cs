namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Pivote N–N entre <see cref="Capacitacion"/> y <see cref="Responsable"/>.
/// Contiene el <c>Orden</c> con el que aparece el responsable en el certificado (0-based),
/// único por capacitación (impuesto por índice único en BD).
/// </summary>
public class CapacitacionResponsable
{
    public Guid CapacitacionId { get; set; }
    public Capacitacion? Capacitacion { get; set; }

    public Guid ResponsableId { get; set; }
    public Responsable? Responsable { get; set; }

    /// <summary>Posición en que aparece en el certificado (0-based, único por capacitación).</summary>
    public int Orden { get; set; }
}
