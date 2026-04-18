namespace Capacitaciones.Application.Dtos.Capacitaciones;

/// <summary>
/// Payload para <c>PUT /api/capacitaciones/{id}</c>. Idéntico al create salvo
/// por el campo <c>Codigo</c> (inmutable). La lista <c>ResponsableIds</c> reemplaza
/// por completo la relación N–N existente (estrategia replace-all sobre la pivote).
/// </summary>
public class UpdateCapacitacionDto
{
    public string Tema { get; set; } = string.Empty;
    public string Capacitador { get; set; } = string.Empty;
    public string? CargoCapacitador { get; set; }
    public string? EmpresaCapacitador { get; set; }
    public string? FirmaCapacitador { get; set; }

    public Guid ModalidadId { get; set; }
    public Guid TipoActividadId { get; set; }

    public string TipoCertificacion { get; set; } = string.Empty;

    public DateTime FechaHoraInicio { get; set; }
    public int DuracionMinutos { get; set; }

    /// <summary>
    /// Fase 9: requerido solo cuando <c>TipoCertificacion == Aprobacion</c> (rango 0–10).
    /// Debe venir null si el tipo es <c>Participacion</c>.
    /// </summary>
    public decimal? PuntajeMinimo { get; set; }

    public string? Descripcion { get; set; }

    public List<Guid> ResponsableIds { get; set; } = new();
}
