namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Clase base abstracta para catálogos administrables.
/// Los 3 catálogos de Fase 1 (Modalidad, TipoActividad, Area) comparten la misma forma.
/// </summary>
public abstract class CatalogoBase
{
    public Guid Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaActualizacion { get; set; }
}
