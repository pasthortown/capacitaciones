using Capacitaciones.Application.Dtos.Recursos;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Recursos;

/// <summary>
/// Caso de uso admin: recibe un stream + metadata, valida, persiste el archivo físico vía
/// <see cref="IResourceStorage"/> y registra la entidad en BD. Si el insert falla después
/// de persistir el archivo, compensa borrando el archivo para no dejar basura huérfana.
/// </summary>
public class SubirRecursoUseCase
{
    /// <summary>Límite en bytes (100 MB) — alineado con <c>RequestSizeLimit</c> del controller.</summary>
    public const long MaxBytes = 100_000_000;

    private readonly IRecursoRepository _repo;
    private readonly IResourceStorage _storage;

    public SubirRecursoUseCase(IRecursoRepository repo, IResourceStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<RecursoDetailDto> ExecuteAsync(
        Stream archivo,
        long tamano,
        string nombreArchivoOriginal,
        string? nombreUsuario,
        string descripcion,
        string? contentType,
        CancellationToken ct = default)
    {
        // Validaciones: hay un orden intencional (archivo/tamaño → descripción → extensión) para
        // que los mensajes sean orientadores aunque varios campos vengan mal al mismo tiempo.
        if (archivo is null || tamano <= 0)
            throw new RecursoServiceException("ARCHIVO_VACIO", "El archivo está vacío o no se recibió contenido.");

        if (tamano > MaxBytes)
            throw new RecursoServiceException(
                "ARCHIVO_DEMASIADO_GRANDE",
                $"El archivo supera el tamaño máximo permitido ({MaxBytes} bytes).");

        if (string.IsNullOrWhiteSpace(nombreArchivoOriginal))
            throw new RecursoServiceException("ARCHIVO_REQUERIDO", "El nombre del archivo es requerido.");

        if (string.IsNullOrWhiteSpace(descripcion))
            throw new RecursoServiceException("DESCRIPCION_REQUERIDA", "La descripción del recurso es requerida.");

        var nombreOriginalTrimmed = nombreArchivoOriginal.Trim();
        var nombreMostrable = string.IsNullOrWhiteSpace(nombreUsuario)
            ? nombreOriginalTrimmed
            : nombreUsuario!.Trim();

        if (nombreMostrable.Length > 500)
            throw new RecursoServiceException(
                "NOMBRE_INVALIDO",
                "El nombre excede el máximo de 500 caracteres.");

        var ext = ExtensionPolicy.Normalize(nombreOriginalTrimmed);
        if (!ExtensionPolicy.IsAllowed(ext))
            throw new RecursoServiceException(
                "EXTENSION_PROHIBIDA",
                $"La extensión '.{ext}' está prohibida por la política del repositorio.");

        // Generamos un storedName único. Guid colisionando es estadísticamente imposible,
        // pero aun así la BD tiene un índice único como respaldo final.
        var storedName = ext is null
            ? Guid.NewGuid().ToString("N")
            : $"{Guid.NewGuid():N}.{ext}";

        // 1) Persistir archivo físico primero. Si falla, no hay basura en BD.
        await _storage.SaveAsync(archivo, storedName, ct);

        // 2) Intentar insertar la metadata. Si revienta, compensamos borrando el archivo.
        var entity = new Recurso
        {
            Id = Guid.NewGuid(),
            NombreOriginal = nombreMostrable,
            NombreAlmacenado = storedName,
            Extension = ext,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? null : contentType!.Trim(),
            TamanoBytes = tamano,
            Descripcion = descripcion.Trim(),
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = null
        };

        try
        {
            await _repo.AddAsync(entity, ct);
        }
        catch
        {
            // Compensación best-effort: si el borrado del archivo falla también, preferimos
            // propagar la excepción original del repo para no enmascarar la causa real.
            try { await _storage.DeleteAsync(storedName, CancellationToken.None); }
            catch { /* swallow: el log lo hace el adaptador si corresponde */ }
            throw;
        }

        return RecursoMapper.ToDetail(entity);
    }
}
