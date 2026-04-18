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

        // Garantiza que el archivo descargado conserve la extensión original aunque
        // el admin haya editado el `NombreOriginal` quitándola (ej. renombró a
        // "Guía del taller" un archivo que venía como "material.pdf"). Sin esto el
        // navegador guarda el archivo sin extensión y queda inusable.
        var nombreDescarga = ComposeFilenameWithExtension(entity.NombreOriginal, entity.Extension);

        return new DescargaRecurso(stream, contentType, nombreDescarga, entity.TamanoBytes);
    }

    private static string ComposeFilenameWithExtension(string? nombreOriginal, string? extension)
    {
        var nombre = string.IsNullOrWhiteSpace(nombreOriginal) ? "recurso" : nombreOriginal!.Trim();
        if (string.IsNullOrWhiteSpace(extension)) return nombre;
        var sufijo = "." + extension!.TrimStart('.').ToLowerInvariant();
        return nombre.EndsWith(sufijo, StringComparison.OrdinalIgnoreCase)
            ? nombre
            : nombre + sufijo;
    }
}

/// <summary>Tupla inmutable con todo lo necesario para emitir la descarga.</summary>
public record DescargaRecurso(Stream Content, string ContentType, string NombreOriginal, long TamanoBytes);
