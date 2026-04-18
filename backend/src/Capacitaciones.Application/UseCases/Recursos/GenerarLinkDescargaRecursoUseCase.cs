using Capacitaciones.Application.Dtos.Recursos;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Recursos;

/// <summary>
/// Devuelve un link de descarga relativo para que el admin lo copie/comparta. No emite
/// token: la ruta pública es accesible por cualquiera que conozca el Id del recurso
/// (v1 — para v2 se evaluará firmar los links y/o expiración).
/// </summary>
public class GenerarLinkDescargaRecursoUseCase
{
    private readonly IRecursoRepository _repo;

    public GenerarLinkDescargaRecursoUseCase(IRecursoRepository repo)
    {
        _repo = repo;
    }

    public async Task<LinkDescargaRecursoDto> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null || !entity.Activo)
            throw new RecursoNotFoundException(id);

        return new LinkDescargaRecursoDto
        {
            Url = $"/api/publico/recursos/{entity.Id}/descargar",
            RecursoId = entity.Id,
            NombreOriginal = entity.NombreOriginal,
            TamanoBytes = entity.TamanoBytes,
            ContentType = entity.ContentType
        };
    }
}
