namespace Capacitaciones.Application.Dtos.PaseLista;

/// <summary>
/// Respuesta del endpoint público <c>GET /api/capacitador/pase-lista</c> (Fase 10).
/// Incluye los datos básicos de la capacitación y la lista de asistentes ordenada
/// alfabéticamente por Apellidos/Nombres.
/// </summary>
public class PaseListaDto
{
    public PaseListaCapacitacionDto Capacitacion { get; set; } = new();

    public IReadOnlyList<PaseListaAsistenteDto> Asistentes { get; set; } = new List<PaseListaAsistenteDto>();
}
