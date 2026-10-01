using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador EF Core de <see cref="IPersonaInscritaRepository"/>. No llama a SaveChanges: los cambios
/// se persisten junto con el asistente en <see cref="AsistenteRepository.AddAsync"/> (mismo DbContext).
/// </summary>
public class PersonaInscritaRepository : IPersonaInscritaRepository
{
    private readonly AppDbContext _db;

    public PersonaInscritaRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<PersonaInscrita?> GetByIdentificacionAsync(string identificacion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(identificacion)) return Task.FromResult<PersonaInscrita?>(null);
        var normalized = identificacion.Trim();
        return _db.PersonasInscritas.FirstOrDefaultAsync(p => p.Identificacion == normalized, ct);
    }

    public void Agregar(PersonaInscrita entity)
    {
        _db.PersonasInscritas.Add(entity);
    }
}
