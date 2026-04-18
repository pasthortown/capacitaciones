namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de almacenamiento físico del logo de capacitación (Fase 9). El adaptador por
/// defecto (<c>LogoCapacitacionStorage</c>) escribe en el directorio configurado por
/// la env var <c>IMAGEN_CAPACITACIONES_DIR</c> (default <c>/imagen_capacitaciones</c>).
///
/// Convención del nombre físico: <c>{guid}.{ext}</c> (sin subdirectorios, sin <c>..</c>).
/// La implementación DEBE validar path traversal.
/// </summary>
public interface ILogoCapacitacionStorage
{
    /// <summary>
    /// Copia <paramref name="contenido"/> al storage y devuelve el nombre físico asignado
    /// (<c>{guid}.{extension}</c>). <paramref name="extension"/> llega normalizada (minúsculas,
    /// sin el punto inicial).
    /// </summary>
    Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct);

    /// <summary>Borra el archivo físico. No falla si no existe (no-op).</summary>
    Task EliminarAsync(string logoPath, CancellationToken ct);
}
