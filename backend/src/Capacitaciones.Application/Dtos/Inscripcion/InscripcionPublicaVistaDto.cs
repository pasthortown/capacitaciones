using Capacitaciones.Application.Dtos.Capacitaciones;

namespace Capacitaciones.Application.Dtos.Inscripcion;

/// <summary>
/// Payload que devuelve <c>GET /api/inscripcion/capacitacion</c>. Incluye los datos
/// mínimos de la capacitación + la lista de áreas activas para llenar el select del formulario.
/// </summary>
public class InscripcionPublicaVistaDto
{
    public InscripcionCapacitacionDto Capacitacion { get; set; } = new();

    public IReadOnlyList<CatalogoRefDto> Areas { get; set; } = Array.Empty<CatalogoRefDto>();
}
