using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

public class AreaRepository : CatalogoRepositoryBase<Area>, IAreaRepository
{
    public AreaRepository(AppDbContext db) : base(db) { }
}
