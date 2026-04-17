using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de persistencia para el catálogo de Áreas.
/// </summary>
public interface IAreaRepository : ICatalogoRepository<Area>
{
}
