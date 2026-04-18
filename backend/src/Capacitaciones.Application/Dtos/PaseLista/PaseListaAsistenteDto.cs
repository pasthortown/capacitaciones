namespace Capacitaciones.Application.Dtos.PaseLista;

/// <summary>
/// Proyección compacta de un asistente para la pantalla de pase de lista (Fase 10).
/// <c>EstadoAsistencia</c> se serializa como string ("Presente" | "Ausente" | <c>null</c>)
/// para que el front pueda mapear al componente <c>AttendanceToggle</c> sin exponer el enum
/// numérico al cliente.
/// </summary>
public class PaseListaAsistenteDto
{
    public Guid Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Identificacion { get; set; } = string.Empty;

    /// <summary>"Presente" | "Ausente" | null si aún no se marcó.</summary>
    public string? EstadoAsistencia { get; set; }

    public DateTime? FechaMarcacionAsistencia { get; set; }
}
