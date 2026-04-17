namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Catálogo global de responsables (firmantes secundarios) utilizable en múltiples
/// capacitaciones vía la pivote <see cref="CapacitacionResponsable"/>. La firma es
/// opcional acá (se carga por el propio responsable desde el link firmado).
/// El capacitador no se modela aquí: figura como primer firmante vía los campos
/// <c>Capacitador</c> y <c>FirmaCapacitador</c> de la propia <see cref="Capacitacion"/>.
/// </summary>
public class Responsable
{
    public Guid Id { get; set; }

    public string Nombres { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;

    /// <summary>Firma (base64 del PNG/JPG). Opcional: el responsable la completa desde su link firmado.</summary>
    public string? Firma { get; set; }

    /// <summary>Baja lógica. Responsable inactivo no puede seleccionarse al crear/editar una capacitación.</summary>
    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
