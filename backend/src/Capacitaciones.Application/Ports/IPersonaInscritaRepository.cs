using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de persistencia para <see cref="PersonaInscrita"/>.
///
/// Importante: este repositorio <b>no</b> llama a SaveChanges. <see cref="GetByIdentificacionAsync"/>
/// devuelve la entidad <i>tracked</i> y <see cref="Agregar"/> solo la deja pendiente; ambos cambios se
/// persisten en el mismo SaveChanges de <see cref="IAsistenteRepository.AddAsync"/> (mismo DbContext
/// por scope). Así la inscripción y la persona se guardan de forma atómica.
/// </summary>
public interface IPersonaInscritaRepository
{
    /// <summary>Busca por identificación exacta (ya trimeada por el caller). Entidad tracked.</summary>
    Task<PersonaInscrita?> GetByIdentificacionAsync(string identificacion, CancellationToken ct = default);

    /// <summary>Deja una persona nueva pendiente de guardar (sin SaveChanges).</summary>
    void Agregar(PersonaInscrita entity);
}
