namespace Capacitaciones.Application.Dtos.PaseLista;

/// <summary>
/// Respuesta compacta tras marcar asistencia (Fase 10). Permite al front actualizar
/// únicamente el asistente tocado sin volver a pedir toda la lista.
/// </summary>
public class MarcacionAsistenciaResponseDto
{
    public Guid Id { get; set; }

    /// <summary>"Presente" | "Ausente" | null si se limpió la marcación.</summary>
    public string? EstadoAsistencia { get; set; }

    public DateTime? FechaMarcacionAsistencia { get; set; }
}
