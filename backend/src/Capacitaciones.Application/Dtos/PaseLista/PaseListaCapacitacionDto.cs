namespace Capacitaciones.Application.Dtos.PaseLista;

/// <summary>
/// Resumen read-only de la capacitación para la pantalla de pase de lista (Fase 10).
/// </summary>
public class PaseListaCapacitacionDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;
    public DateTime FechaHoraInicio { get; set; }
    public int DuracionMinutos { get; set; }
    public string Estado { get; set; } = string.Empty;
}
