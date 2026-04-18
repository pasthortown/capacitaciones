namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de almacenamiento físico de recursos. El adaptador por defecto
/// (<c>FileSystemResourceStorage</c>) escribe en el directorio configurado por
/// la env var <c>REPOSITORIO_DIR</c> (default <c>/repository</c>).
///
/// Convención de <c>storedName</c>: nombre plano <c>{guid}.{ext}</c> (sin subdirectorios,
/// sin <c>..</c>). Cualquier implementación DEBE validar path traversal para proteger
/// contra nombres maliciosos.
/// </summary>
public interface IResourceStorage
{
    /// <summary>Copia <paramref name="content"/> al storage bajo <paramref name="storedName"/>.</summary>
    Task SaveAsync(Stream content, string storedName, CancellationToken ct);

    /// <summary>True si el archivo existe en el storage.</summary>
    bool Exists(string storedName);

    /// <summary>Borra el archivo físico. No falla si no existe (no-op).</summary>
    Task DeleteAsync(string storedName, CancellationToken ct);

    /// <summary>Abre el archivo en modo lectura. Falla si no existe.</summary>
    Stream OpenRead(string storedName);

    /// <summary>Devuelve la ruta absoluta en el filesystem (útil para logs/diagnóstico).</summary>
    string GetAbsolutePath(string storedName);
}
