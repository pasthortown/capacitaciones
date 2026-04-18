using Capacitaciones.Application.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Capacitaciones.Infrastructure.Adapters.Storage;

/// <summary>
/// Adaptador por filesystem del puerto <see cref="ILogoCapacitacionStorage"/> (Fase 9).
/// Directorio raíz configurable vía <see cref="LogoCapacitacionStorageOptions.Directory"/>
/// (Program.cs resuelve prioridad: env <c>IMAGEN_CAPACITACIONES_DIR</c> → appsettings →
/// default <c>/imagen_capacitaciones</c>).
///
/// Seguridad: <c>logoPath</c> se valida como nombre plano (sin <c>/</c>, <c>\</c>, ni <c>..</c>)
/// para impedir path traversal. El nombre físico siempre es <c>{guid}.{ext}</c> generado aquí,
/// nunca viene del cliente.
/// </summary>
public class LogoCapacitacionStorage : ILogoCapacitacionStorage
{
    private readonly string _rootDir;
    private readonly ILogger<LogoCapacitacionStorage> _logger;

    public LogoCapacitacionStorage(
        IOptions<LogoCapacitacionStorageOptions> options,
        ILogger<LogoCapacitacionStorage> logger)
    {
        _logger = logger;

        var configured = options.Value.Directory;
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = "/imagen_capacitaciones";
        }

        _rootDir = Path.GetFullPath(configured);

        if (!Directory.Exists(_rootDir))
        {
            Directory.CreateDirectory(_rootDir);
            _logger.LogInformation("Directorio de logos de capacitación creado: {Dir}", _rootDir);
        }
        else
        {
            _logger.LogInformation("Directorio de logos de capacitación: {Dir}", _rootDir);
        }
    }

    public async Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct)
    {
        if (contenido is null) throw new ArgumentNullException(nameof(contenido));
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("La extensión es requerida para nombrar el logo.", nameof(extension));

        var normalized = extension.Trim().TrimStart('.').ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}.{normalized}";

        var path = BuildSafePath(storedName);

        await using var fs = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await contenido.CopyToAsync(fs, ct);

        return storedName;
    }

    public Task EliminarAsync(string logoPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(logoPath)) return Task.CompletedTask;

        var path = BuildSafePath(logoPath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Compone la ruta absoluta validando que <paramref name="storedName"/> sea un nombre plano.
    /// Rechaza separadores de directorio, <c>..</c> y nombres vacíos.
    /// </summary>
    private string BuildSafePath(string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName))
            throw new InvalidOperationException("logoPath no puede estar vacío.");

        if (storedName.Contains('/') || storedName.Contains('\\') ||
            storedName.Contains("..") || Path.IsPathRooted(storedName))
        {
            throw new InvalidOperationException($"logoPath inválido (path traversal): '{storedName}'.");
        }

        var combined = Path.GetFullPath(Path.Combine(_rootDir, storedName));
        if (!combined.StartsWith(_rootDir, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"logoPath fuera del directorio raíz: '{storedName}'.");
        }
        return combined;
    }
}
