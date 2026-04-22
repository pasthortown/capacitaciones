namespace Capacitaciones.Application.Dtos.Capacitaciones;

/// <summary>
/// Proyección completa de una capacitación, incluyendo datos del capacitador
/// y la lista de responsables adicionales ordenada por <c>Orden</c>.
/// </summary>
public class CapacitacionDetailDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;

    public string Capacitador { get; set; } = string.Empty;
    public string? CargoCapacitador { get; set; }
    public string? EmpresaCapacitador { get; set; }
    public string? EmailCapacitador { get; set; }
    public string? FirmaCapacitador { get; set; }

    public string? Descripcion { get; set; }

    public CatalogoRefDto Modalidad { get; set; } = new();
    public CatalogoRefDto TipoActividad { get; set; } = new();

    public string TipoCertificacion { get; set; } = string.Empty;

    public DateTime FechaHoraInicio { get; set; }
    public int DuracionMinutos { get; set; }

    /// <summary>Fase 9: puntaje mínimo de aprobación (null si el tipo es Participacion).</summary>
    public decimal? PuntajeMinimo { get; set; }

    /// <summary>
    /// Fase 9: URL relativa del logo (ej. <c>/imagenes/&lt;Guid&gt;.png</c>).
    /// Null cuando la capacitación no tiene logo cargado.
    /// </summary>
    public string? LogoUrl { get; set; }

    public string Estado { get; set; } = string.Empty;
    public int TotalAsistentes { get; set; }

    public bool Activo { get; set; }

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }

    public List<ResponsableDto> Responsables { get; set; } = new();
}
