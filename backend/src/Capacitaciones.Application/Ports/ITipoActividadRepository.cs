using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de persistencia para el catálogo de Tipos de Actividad.
/// </summary>
public interface ITipoActividadRepository : ICatalogoRepository<TipoActividad>
{
}
