using Capacitaciones.Application.Dtos.Capacitaciones;

namespace Capacitaciones.Application.Dtos.Capacitador;

/// <summary>
/// Vista de una capacitación expuesta al capacitador autenticado con link firmado (Fase 4).
/// NO incluye responsables ni lista de asistentes: el capacitador solo ve lo necesario
/// para contextualizar la sesión y los campos que puede editar sobre su propia identidad.
/// </summary>
public class CapacitadorCapacitacionDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;

    public string Capacitador { get; set; } = string.Empty;

    public DateTime FechaHoraInicio { get; set; }
    public int DuracionMinutos { get; set; }

    public CatalogoRefDto Modalidad { get; set; } = new();
    public CatalogoRefDto TipoActividad { get; set; } = new();

    public string TipoCertificacion { get; set; } = string.Empty;

    /// <summary>Estado derivado: "Inscripciones Abiertas" | "Iniciada" | "Finalizada".</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>Campos editables por el capacitador.</summary>
    public string? Descripcion { get; set; }
    public string? FirmaCapacitador { get; set; }
    public string? CargoCapacitador { get; set; }
    public string? EmpresaCapacitador { get; set; }
    public string? EmailCapacitador { get; set; }
}
