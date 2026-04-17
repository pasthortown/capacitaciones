using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

public class TipoActividadRepository : CatalogoRepositoryBase<TipoActividad>, ITipoActividadRepository
{
    public TipoActividadRepository(AppDbContext db) : base(db) { }
}
