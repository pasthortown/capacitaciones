using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Recursos;

/// <summary>
/// Descarga pública de un recurso activo. El controlador usa el <c>Stream</c> devuelto
/// para construir un <c>FileStreamResult</c>; el stream se cierra al terminar la respuesta.
/// </summary>
public class DescargarRecursoUseCase
{
    private readonly IRecursoRepository _repo;
    private readonly IResourceStorage _storage;

    public DescargarRecursoUseCase(IRecursoRepository repo, IResourceStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<DescargaRecurso> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null || !entity.Activo)
            throw new RecursoNotFoundException(id);

        if (!_storage.Exists(entity.NombreAlmacenado))
            throw new ArchivoFisicoAusenteException(entity.Id, entity.NombreAlmacenado);

        var stream = _storage.OpenRead(entity.NombreAlmacenado);
        var contentType = string.IsNullOrWhiteSpace(entity.ContentType)
            ? "application/octet-stream"
            : entity.ContentType!;

        return new DescargaRecurso(stream, contentType, entity.NombreOriginal, entity.TamanoBytes);
    }
}

/// <summary>Tupla inmutable con todo lo necesario para emitir la descarga.</summary>
public record DescargaRecurso(Stream Content, string ContentType, string NombreOriginal, long TamanoBytes);
