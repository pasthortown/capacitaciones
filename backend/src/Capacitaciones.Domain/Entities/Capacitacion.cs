namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Capacitación registrada por el administrador. El <see cref="Codigo"/> se asigna
/// atómicamente vía <c>INumeracionService.ClaimNextCodeAsync</c> al crear la entidad
/// y es inmutable después de la creación.
/// El <c>Estado</c> (Inscripciones Abiertas / Iniciada / Finalizada) se calcula en runtime
/// a partir de <see cref="FechaHoraInicio"/> y <see cref="DuracionMinutos"/> — no se persiste.
/// </summary>
public class Capacitacion
{
    public Guid Id { get; set; }

    /// <summary>Código único con formato <c>CAP-PC-REG-###</c>.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Tema { get; set; } = string.Empty;

    public string Capacitador { get; set; } = string.Empty;

    public string? CargoCapacitador { get; set; }

    public string? EmpresaCapacitador { get; set; }

    /// <summary>Firma del capacitador (base64 del PNG/JPG). Capturada vía link firmado en Fase 4.</summary>
    public string? FirmaCapacitador { get; set; }

    /// <summary>Descripción libre — también capturada por el capacitador en Fase 4.</summary>
    public string? Descripcion { get; set; }

    public Guid ModalidadId { get; set; }
    public Modalidad? Modalidad { get; set; }

    public Guid TipoActividadId { get; set; }
    public TipoActividad? TipoActividad { get; set; }

    public TipoCertificacion TipoCertificacion { get; set; }

    public DateTime FechaHoraInicio { get; set; }

    /// <summary>Duración en minutos. Debe ser múltiplo de 30 y mayor a 0.</summary>
    public int DuracionMinutos { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }

    /// <summary>
    /// Relación N–N con el catálogo de responsables. Cada entrada trae su propio <c>Orden</c>
    /// (0-based, único por capacitación). Acceder al responsable real vía
    /// <c>CapacitacionResponsables[i].Responsable</c> (cargado con <c>ThenInclude</c>).
    /// </summary>
    public List<CapacitacionResponsable> CapacitacionResponsables { get; set; } = new();
}
