using Capacitaciones.Application.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Capacitaciones.Infrastructure.Adapters.Storage;

/// <summary>
/// Adaptador por filesystem del puerto <see cref="IResourceStorage"/>.
/// Directorio raíz configurable vía <see cref="ResourceStorageOptions.Directory"/>
/// (Program.cs resuelve prioridad: env <c>REPOSITORIO_DIR</c> → appsettings → default <c>/repository</c>).
///
/// Seguridad: <c>storedName</c> se valida como nombre plano (sin <c>/</c>, <c>\</c>, ni <c>..</c>)
/// para impedir path traversal. Cualquier nombre que viole la convención lanza
/// <see cref="InvalidOperationException"/> — esto es un invariante del sistema, no un error de
/// usuario (el caller es el UseCase, no el request).
/// </summary>
public class FileSystemResourceStorage : IResourceStorage
{
    private readonly string _rootDir;
    private readonly ILogger<FileSystemResourceStorage> _logger;

    public FileSystemResourceStorage(
        IOptions<ResourceStorageOptions> options,
        ILogger<FileSystemResourceStorage> logger)
    {
        _logger = logger;

        var configured = options.Value.Directory;
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = "/repository";
        }

        _rootDir = Path.GetFullPath(configured);

        if (!Directory.Exists(_rootDir))
        {
            Directory.CreateDirectory(_rootDir);
            _logger.LogInformation("Directorio del repositorio creado: {Dir}", _rootDir);
        }
        else
        {
            _logger.LogInformation("Directorio del repositorio: {Dir}", _rootDir);
        }
    }

    public async Task SaveAsync(Stream content, string storedName, CancellationToken ct)
    {
        if (content is null) throw new ArgumentNullException(nameof(content));
        var path = BuildSafePath(storedName);

        await using var fs = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await content.CopyToAsync(fs, ct);
    }

    public bool Exists(string storedName)
    {
        var path = BuildSafePath(storedName);
        return File.Exists(path);
    }

    public Task DeleteAsync(string storedName, CancellationToken ct)
    {
        var path = BuildSafePath(storedName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    public Stream OpenRead(string storedName)
    {
        var path = BuildSafePath(storedName);
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
    }

    public string GetAbsolutePath(string storedName) => BuildSafePath(storedName);

    /// <summary>
    /// Compone la ruta absoluta validando que <paramref name="storedName"/> sea un nombre plano.
    /// Rechaza separadores de directorio, <c>..</c> y nombres vacíos.
    /// </summary>
    private string BuildSafePath(string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName))
            throw new InvalidOperationException("storedName no puede estar vacío.");

        // Nombre plano: ningún separador, ningún salto al padre.
        if (storedName.Contains('/') || storedName.Contains('\\') ||
            storedName.Contains("..") || Path.IsPathRooted(storedName))
        {
            throw new InvalidOperationException($"storedName inválido (path traversal): '{storedName}'.");
        }

        // Path.Combine aquí es seguro porque ya filtramos separadores. Re-validamos con
        // GetFullPath para que la ruta resultante siga dentro del root.
        var combined = Path.GetFullPath(Path.Combine(_rootDir, storedName));
        if (!combined.StartsWith(_rootDir, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"storedName fuera del directorio raíz: '{storedName}'.");
        }
        return combined;
    }
}
