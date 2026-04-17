using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

public class ModalidadRepository : CatalogoRepositoryBase<Modalidad>, IModalidadRepository
{
    public ModalidadRepository(AppDbContext db) : base(db) { }
}
