namespace Capacitaciones.Application.Dtos.Capacitaciones;

/// <summary>
/// Payload para <c>POST /api/capacitaciones</c>. El <c>Codigo</c> se asigna en el backend.
/// <c>TipoCertificacion</c> se recibe como string para permitir el parseo explícito
/// (<c>Participacion</c> | <c>Aprobacion</c>).
/// </summary>
public class CreateCapacitacionDto
{
    public string Tema { get; set; } = string.Empty;
    public string Capacitador { get; set; } = string.Empty;
    public string? CargoCapacitador { get; set; }
    public string? EmpresaCapacitador { get; set; }

    public Guid ModalidadId { get; set; }
    public Guid TipoActividadId { get; set; }

    public string TipoCertificacion { get; set; } = string.Empty;

    public DateTime FechaHoraInicio { get; set; }
    public int DuracionMinutos { get; set; }

    public string? Descripcion { get; set; }

    public List<CreateResponsableDto> Responsables { get; set; } = new();
}
