namespace Capacitaciones.Application.Dtos.Capacitaciones;

/// <summary>
/// Payload para <c>PUT /api/capacitaciones/{id}</c>. Idéntico al create salvo
/// por el campo <c>Codigo</c> (inmutable). La lista <c>Responsables</c> reemplaza
/// por completo a la existente (estrategia replace-all).
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

    public string? Descripcion { get; set; }

    public List<CreateResponsableDto> Responsables { get; set; } = new();
}
