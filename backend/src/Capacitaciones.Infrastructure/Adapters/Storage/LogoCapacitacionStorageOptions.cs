namespace Capacitaciones.Infrastructure.Adapters.Storage;

/// <summary>
/// Opciones del adaptador <see cref="LogoCapacitacionStorage"/>. Se bindean desde
/// <c>appsettings.LogoCapacitacionStorage:*</c> y/o la env var
/// <c>IMAGEN_CAPACITACIONES_DIR</c> (que tiene prioridad en Program.cs por convención
/// operativa del compose).
/// </summary>
public class LogoCapacitacionStorageOptions
{
    public const string SectionName = "LogoCapacitacionStorage";

    /// <summary>Directorio raíz donde se persisten los logos de capacitación.</summary>
    public string Directory { get; set; } = "/imagen_capacitaciones";
}
