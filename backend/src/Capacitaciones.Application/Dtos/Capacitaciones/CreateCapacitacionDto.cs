namespace Capacitaciones.Application.Dtos.Capacitaciones;

/// <summary>
/// Payload para <c>POST /api/capacitaciones</c>. El <c>Codigo</c> se asigna en el backend.
/// <c>TipoCertificacion</c> se recibe como string para permitir el parseo explícito
/// (<c>Participacion</c> | <c>Aprobacion</c>).
/// <c>ResponsableIds</c> son ids del catálogo global de responsables en el orden deseado
/// (posición 0-based en la lista = orden del firmante en el certificado).
/// </summary>
public class CreateCapacitacionDto
{
    public string Tema { get; set; } = string.Empty;
    public string Capacitador { get; set; } = string.Empty;
    public string? CargoCapacitador { get; set; }
    public string? EmpresaCapacitador { get; set; }
    public string? EmailCapacitador { get; set; }

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

    /// <summary>
    /// Indica si la capacitación emite certificados. Si el cliente no envía valor,
    /// el backend asume <c>true</c> (default histórico).
    /// </summary>
    public bool EmiteCertificado { get; set; } = true;

    public List<Guid> ResponsableIds { get; set; } = new();
}
