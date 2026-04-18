using Capacitaciones.Application.Dtos.Recursos;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Recursos;

/// <summary>
/// Edita la metadata visible (<c>NombreOriginal</c>, <c>Descripcion</c>) y, opcionalmente,
/// <b>reemplaza el archivo físico</b> de un recurso.
///
/// Cuando <c>archivoNuevo</c> es <c>null</c> o <c>tamanoNuevo == 0</c>, sólo se actualiza la
/// metadata (comportamiento original). Cuando se recibe un archivo nuevo:
///   1. Se validan tamaño (≤ 100 MB) y extensión (no bloqueada) igual que en el alta.
///   2. Se genera un nuevo <c>NombreAlmacenado</c> (UUID), se guarda vía storage.
///   3. Se actualizan <c>NombreAlmacenado</c>, <c>Extension</c>, <c>ContentType</c> y
///      <c>TamanoBytes</c> en la entidad.
///   4. Se intenta borrar el archivo anterior (best-effort: si falla, se loguea pero no
///      revierte — el registro en BD ya apunta al nuevo).
///
/// El flujo deja el archivo antiguo sólo si el UPDATE en BD falla; en ese caso compensamos
/// borrando el nuevo para no dejar basura huérfana.
/// </summary>
public class EditarMetadataRecursoUseCase
{
    public const long MaxBytes = SubirRecursoUseCase.MaxBytes;

    private readonly IRecursoRepository _repo;
    private readonly IResourceStorage _storage;

    public EditarMetadataRecursoUseCase(IRecursoRepository repo, IResourceStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    /// <summary>
    /// Edición sólo de metadata (sin reemplazo de archivo).
    /// </summary>
    public Task<RecursoDetailDto> ExecuteAsync(Guid id, UpdateRecursoMetadataDto input, CancellationToken ct = default)
        => ExecuteAsync(id, input, archivoNuevo: null, tamanoNuevo: 0, nombreArchivoNuevo: null, contentTypeNuevo: null, ct);

    /// <summary>
    /// Edición de metadata con posibilidad de reemplazar el archivo físico.
    /// </summary>
    public async Task<RecursoDetailDto> ExecuteAsync(
        Guid id,
        UpdateRecursoMetadataDto input,
        Stream? archivoNuevo,
        long tamanoNuevo,
        string? nombreArchivoNuevo,
        string? contentTypeNuevo,
        CancellationToken ct = default)
    {
        ValidarMetadata(input);

        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new RecursoNotFoundException(id);

        // Flujo de reemplazo del binario si el caller envió un nuevo archivo.
        var reemplazoInfo = archivoNuevo is not null && tamanoNuevo > 0
            ? await AplicarReemplazoArchivoAsync(entity, archivoNuevo, tamanoNuevo, nombreArchivoNuevo, contentTypeNuevo, ct)
            : null;

        entity.NombreOriginal = input.NombreOriginal.Trim();
        entity.Descripcion = input.Descripcion.Trim();
        entity.FechaActualizacion = DateTime.UtcNow;

        await PersistirConCompensacionAsync(entity, reemplazoInfo, ct);

        if (reemplazoInfo is { OldStored: { } oldStored } && oldStored != reemplazoInfo.NewStored)
        {
            // Best-effort: borramos el archivo anterior. Si falla, queda huérfano pero el recurso
            // ya apunta al nuevo. No revertimos porque la fuente de verdad ya es el nuevo.
            try { await _storage.DeleteAsync(oldStored, CancellationToken.None); }
            catch { /* swallow: el adaptador loguea si corresponde */ }
        }

        return RecursoMapper.ToDetail(entity);
    }

    private static void ValidarMetadata(UpdateRecursoMetadataDto? input)
    {
        if (input is null)
            throw new RecursoServiceException("INVALID_INPUT", "Payload requerido.");

        if (string.IsNullOrWhiteSpace(input.NombreOriginal))
            throw new RecursoServiceException("NOMBRE_REQUERIDO", "El nombre del recurso es requerido.");
        if (input.NombreOriginal.Length > 500)
            throw new RecursoServiceException("NOMBRE_INVALIDO", "El nombre excede el máximo de 500 caracteres.");

        if (string.IsNullOrWhiteSpace(input.Descripcion))
            throw new RecursoServiceException("DESCRIPCION_REQUERIDA", "La descripción del recurso es requerida.");
        if (input.Descripcion.Length > 2000)
            throw new RecursoServiceException("DESCRIPCION_INVALIDA", "La descripción excede el máximo de 2000 caracteres.");
    }

    /// <summary>
    /// Valida tamaño/extensión, guarda el nuevo archivo y muta la entidad con los nuevos
    /// datos binarios. Devuelve la info necesaria para compensar/limpiar según el resultado
    /// del UPDATE en BD.
    /// </summary>
    private async Task<ReemplazoInfo> AplicarReemplazoArchivoAsync(
        Recurso entity,
        Stream archivoNuevo,
        long tamanoNuevo,
        string? nombreArchivoNuevo,
        string? contentTypeNuevo,
        CancellationToken ct)
    {
        if (tamanoNuevo > MaxBytes)
            throw new RecursoServiceException(
                "ARCHIVO_DEMASIADO_GRANDE",
                $"El archivo supera el tamaño máximo permitido ({MaxBytes} bytes).");

        if (string.IsNullOrWhiteSpace(nombreArchivoNuevo))
            throw new RecursoServiceException("ARCHIVO_REQUERIDO", "El nombre del archivo es requerido.");

        var ext = ExtensionPolicy.Normalize(nombreArchivoNuevo!);
        if (!ExtensionPolicy.IsAllowed(ext))
            throw new RecursoServiceException(
                "EXTENSION_PROHIBIDA",
                $"La extensión '.{ext}' está prohibida por la política del repositorio.");

        var nuevoStored = ext is null
            ? Guid.NewGuid().ToString("N")
            : $"{Guid.NewGuid():N}.{ext}";

        // Guardamos el nuevo archivo primero (si falla, no tocamos el recurso).
        await _storage.SaveAsync(archivoNuevo, nuevoStored, ct);

        var oldStored = entity.NombreAlmacenado;
        entity.NombreAlmacenado = nuevoStored;
        entity.Extension = ext;
        entity.ContentType = string.IsNullOrWhiteSpace(contentTypeNuevo) ? null : contentTypeNuevo!.Trim();
        entity.TamanoBytes = tamanoNuevo;

        return new ReemplazoInfo(nuevoStored, oldStored);
    }

    /// <summary>
    /// Persiste el recurso. Si el UPDATE revienta y hubo un archivo nuevo subido, lo
    /// borra del storage para no dejar basura huérfana antes de re-lanzar la excepción.
    /// </summary>
    private async Task PersistirConCompensacionAsync(Recurso entity, ReemplazoInfo? reemplazoInfo, CancellationToken ct)
    {
        try
        {
            await _repo.UpdateAsync(entity, ct);
        }
        catch
        {
            if (reemplazoInfo is not null)
            {
                try { await _storage.DeleteAsync(reemplazoInfo.NewStored, CancellationToken.None); }
                catch { /* swallow: propagamos la excepción original del repo */ }
            }
            throw;
        }
    }

    private sealed record ReemplazoInfo(string NewStored, string OldStored);
}
