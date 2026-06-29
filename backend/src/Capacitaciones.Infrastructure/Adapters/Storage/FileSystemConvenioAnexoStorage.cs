using Capacitaciones.Application.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Capacitaciones.Infrastructure.Adapters.Storage;

/// <summary>Opciones del storage de anexos de convenios (dir raíz; env <c>CONVENIOS_DIR</c>).</summary>
public sealed class ConvenioAnexoStorageOptions
{
    public string? Directory { get; set; }
}

/// <summary>
/// Adaptador por filesystem de <see cref="IConvenioAnexoStorage"/>. Mismo patrón de seguridad
/// (nombre plano, sin path traversal) que el storage de recursos.
/// </summary>
public class FileSystemConvenioAnexoStorage : IConvenioAnexoStorage
{
    private readonly string _rootDir;

    public FileSystemConvenioAnexoStorage(
        IOptions<ConvenioAnexoStorageOptions> options,
        ILogger<FileSystemConvenioAnexoStorage> logger)
    {
        var configured = options.Value.Directory;
        if (string.IsNullOrWhiteSpace(configured)) configured = "/convenios_anexos";
        _rootDir = Path.GetFullPath(configured);
        if (!Directory.Exists(_rootDir))
        {
            Directory.CreateDirectory(_rootDir);
            logger.LogInformation("Directorio de anexos de convenios creado: {Dir}", _rootDir);
        }
    }

    public async Task SaveAsync(Stream content, string storedName, CancellationToken ct)
    {
        if (content is null) throw new ArgumentNullException(nameof(content));
        var path = BuildSafePath(storedName);
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await content.CopyToAsync(fs, ct);
    }

    public bool Exists(string storedName) => File.Exists(BuildSafePath(storedName));

    public Task DeleteAsync(string storedName, CancellationToken ct)
    {
        var path = BuildSafePath(storedName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Stream OpenRead(string storedName)
        => new FileStream(BuildSafePath(storedName), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);

    private string BuildSafePath(string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName))
            throw new InvalidOperationException("storedName no puede estar vacío.");
        if (storedName.Contains('/') || storedName.Contains('\\') ||
            storedName.Contains("..") || Path.IsPathRooted(storedName))
            throw new InvalidOperationException($"storedName inválido (path traversal): '{storedName}'.");
        var combined = Path.GetFullPath(Path.Combine(_rootDir, storedName));
        if (!combined.StartsWith(_rootDir, StringComparison.Ordinal))
            throw new InvalidOperationException($"storedName fuera del directorio raíz: '{storedName}'.");
        return combined;
    }
}
