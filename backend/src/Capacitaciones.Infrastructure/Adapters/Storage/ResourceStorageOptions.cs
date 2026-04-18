namespace Capacitaciones.Infrastructure.Adapters.Storage;

/// <summary>
/// Opciones del adaptador <see cref="FileSystemResourceStorage"/>. Se bindean desde
/// <c>appsettings.ResourceStorage:*</c> y/o la env var <c>REPOSITORIO_DIR</c> (que tiene
/// prioridad en Program.cs por convención operativa del compose).
/// </summary>
public class ResourceStorageOptions
{
    public const string SectionName = "ResourceStorage";

    /// <summary>Directorio raíz donde se persisten los archivos del repositorio.</summary>
    public string Directory { get; set; } = "/repository";
}
