using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de persistencia para el catálogo de Modalidades.
/// </summary>
public interface IModalidadRepository : ICatalogoRepository<Modalidad>
{
}
