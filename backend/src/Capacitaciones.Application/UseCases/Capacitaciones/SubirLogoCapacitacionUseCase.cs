using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>
/// Fase 9 — Caso de uso admin: carga (o reemplazo) del logo de una capacitación.
/// Valida extensión, content-type y tamaño; si ya hay logo previo lo borra primero del
/// storage físico, luego persiste el nuevo archivo y actualiza <c>LogoPath</c> +
/// <c>LogoContentType</c> en la entidad.
///
/// Contrato de compensación: si tras escribir el archivo el update de BD falla,
/// borramos el archivo recién escrito para no dejar basura huérfana. Si falla el borrado
/// del logo anterior también, propagamos el error (el cliente puede reintentar).
/// </summary>
public class SubirLogoCapacitacionUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly ILogoCapacitacionStorage _storage;

    public SubirLogoCapacitacionUseCase(ICapacitacionRepository repo, ILogoCapacitacionStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<LogoCapacitacionDto> ExecuteAsync(
        Guid id,
        Stream contenido,
        string fileName,
        string contentType,
        long size,
        CancellationToken ct = default)
    {
        if (contenido is null || size <= 0)
            throw new CapacitacionServiceException("LOGO_VACIO", "El archivo está vacío o no se recibió contenido.");

        if (size > LogoCapacitacionPolicy.MaxBytes)
            throw new CapacitacionServiceException(
                "LOGO_DEMASIADO_GRANDE",
                $"El logo supera el tamaño máximo permitido ({LogoCapacitacionPolicy.MaxBytes} bytes / 2 MB).");

        if (string.IsNullOrWhiteSpace(fileName))
            throw new CapacitacionServiceException("LOGO_NOMBRE_REQUERIDO", "El nombre del archivo es requerido.");

        var extension = LogoCapacitacionPolicy.NormalizarExtension(fileName);
        if (!LogoCapacitacionPolicy.EsExtensionPermitida(extension))
            throw new CapacitacionServiceException(
                "LOGO_EXTENSION_INVALIDA",
                "Extensión de logo no permitida. Aceptadas: png, jpg, jpeg, webp, svg.");

        if (!LogoCapacitacionPolicy.EsContentTypePermitido(contentType))
            throw new CapacitacionServiceException(
                "LOGO_CONTENT_TYPE_INVALIDO",
                "MIME del logo no permitido. Aceptados: image/png, image/jpeg, image/webp, image/svg+xml.");

        if (!LogoCapacitacionPolicy.ExtensionYContentTypeCoherentes(extension!, contentType))
            throw new CapacitacionServiceException(
                "LOGO_CONTENT_TYPE_INCOHERENTE",
                "La extensión del archivo no coincide con el tipo MIME declarado.");

        var entity = await _repo.GetByIdWithResponsablesAsync(id, ct)
            ?? throw new CapacitacionNotFoundException(id);

        if (!entity.Activo)
            throw new CapacitacionServiceException(
                "CAPACITACION_INACTIVA",
                "No se puede cargar logo para una capacitación inactiva.");

        var logoAnterior = entity.LogoPath;

        // 1) Escribir el archivo físico nuevo. El guid nuevo garantiza que no colisione con el anterior.
        var nuevoLogoPath = await _storage.GuardarAsync(contenido, extension!, ct);

        try
        {
            entity.LogoPath = nuevoLogoPath;
            entity.LogoContentType = contentType.Trim();
            entity.FechaActualizacion = DateTime.UtcNow;
            await _repo.UpdateAsync(entity, ct);
        }
        catch
        {
            // Compensación best-effort: si el update reventó, eliminamos el archivo nuevo
            // para no dejar basura. El log del adaptador cubre el caso de que el delete falle.
            try { await _storage.EliminarAsync(nuevoLogoPath, CancellationToken.None); }
            catch { /* swallow: propagamos la excepción original del repo */ }
            throw;
        }

        // 2) Borrar el logo anterior (si existía) ahora que el nuevo está commiteado.
        //    Best-effort: un fallo aquí deja un archivo huérfano pero la BD ya apunta al nuevo logo.
        if (!string.IsNullOrWhiteSpace(logoAnterior))
        {
            try { await _storage.EliminarAsync(logoAnterior!, CancellationToken.None); }
            catch { /* swallow: log en adaptador si corresponde */ }
        }

        return new LogoCapacitacionDto
        {
            LogoPath = nuevoLogoPath,
            LogoContentType = entity.LogoContentType!,
            LogoUrl = "/imagenes/" + nuevoLogoPath
        };
    }
}

/// <summary>Resultado compacto de la carga/reemplazo del logo de capacitación.</summary>
public class LogoCapacitacionDto
{
    public string LogoPath { get; set; } = string.Empty;
    public string LogoContentType { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
}
