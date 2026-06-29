using Capacitaciones.Application.Dtos.Convenios;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Convenios;

/// <summary>Agrega un anexo al convenio (convenio firmado, formulario de cobro firmado, etc.). Múltiples por convenio.</summary>
public class SubirAnexoConvenioUseCase
{
    public const long MaxBytes = 25_000_000; // 25 MB

    private readonly IConvenioRepository _repo;
    private readonly IConvenioAnexoStorage _storage;

    public SubirAnexoConvenioUseCase(IConvenioRepository repo, IConvenioAnexoStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<ConvenioDto> ExecuteAsync(
        Guid convenioId, Stream archivo, long tamano, string nombreOriginal, string? contentType, CancellationToken ct = default)
    {
        if (archivo is null || tamano <= 0)
            throw new ConvenioValidacionException("El archivo está vacío o no se recibió contenido.");
        if (tamano > MaxBytes)
            throw new ConvenioValidacionException("El anexo supera el tamaño máximo permitido (25 MB).");

        var convenio = await _repo.GetByIdAsync(convenioId, ct) ?? throw new ConvenioNotFoundException(convenioId);

        var ext = Path.GetExtension(nombreOriginal ?? string.Empty).TrimStart('.').ToLowerInvariant();
        var stored = string.IsNullOrEmpty(ext) ? $"{Guid.NewGuid()}" : $"{Guid.NewGuid()}.{ext}";

        await _storage.SaveAsync(archivo, stored, ct);

        // Sin Id explícito: EF lo genera. Pre-asignarlo en un padre tracked lo trataría como UPDATE.
        convenio.Anexos.Add(new ConvenioAnexo
        {
            ConvenioId = convenio.Id,
            NombreOriginal = nombreOriginal,
            NombreAlmacenado = stored,
            ContentType = contentType,
            TamanoBytes = tamano,
            FechaCreacion = DateTime.UtcNow,
        });
        convenio.FechaActualizacion = DateTime.UtcNow;
        await _repo.UpdateAsync(convenio, ct);

        return ConvenioMapper.ToDto(convenio);
    }
}

/// <summary>Elimina un anexo concreto del convenio (archivo físico + fila).</summary>
public class EliminarAnexoConvenioUseCase
{
    private readonly IConvenioRepository _repo;
    private readonly IConvenioAnexoStorage _storage;

    public EliminarAnexoConvenioUseCase(IConvenioRepository repo, IConvenioAnexoStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task ExecuteAsync(Guid convenioId, Guid anexoId, CancellationToken ct = default)
    {
        var convenio = await _repo.GetByIdAsync(convenioId, ct) ?? throw new ConvenioNotFoundException(convenioId);
        var anexo = convenio.Anexos.FirstOrDefault(a => a.Id == anexoId)
            ?? throw new ConvenioServiceException("ANEXO_NO_ENCONTRADO", "El anexo no existe en este convenio.");
        var stored = anexo.NombreAlmacenado;
        convenio.Anexos.Remove(anexo);
        convenio.FechaActualizacion = DateTime.UtcNow;
        await _repo.UpdateAsync(convenio, ct);
        if (!string.IsNullOrWhiteSpace(stored)) await _storage.DeleteAsync(stored, ct);
    }
}

/// <summary>Abre un anexo concreto para descarga.</summary>
public class DescargarAnexoConvenioUseCase
{
    private readonly IConvenioRepository _repo;
    private readonly IConvenioAnexoStorage _storage;

    public DescargarAnexoConvenioUseCase(IConvenioRepository repo, IConvenioAnexoStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<(Stream Content, string FileName, string ContentType)> ExecuteAsync(Guid convenioId, Guid anexoId, CancellationToken ct = default)
    {
        var convenio = await _repo.GetByIdAsync(convenioId, ct) ?? throw new ConvenioNotFoundException(convenioId);
        var anexo = convenio.Anexos.FirstOrDefault(a => a.Id == anexoId)
            ?? throw new ConvenioServiceException("ANEXO_NO_ENCONTRADO", "El anexo no existe en este convenio.");
        if (!_storage.Exists(anexo.NombreAlmacenado))
            throw new ConvenioServiceException("ANEXO_AUSENTE", "El archivo físico del anexo no está disponible.");

        var stream = _storage.OpenRead(anexo.NombreAlmacenado);
        var name = string.IsNullOrWhiteSpace(anexo.NombreOriginal) ? $"anexo-{anexoId}" : anexo.NombreOriginal;
        var ctype = string.IsNullOrWhiteSpace(anexo.ContentType) ? "application/octet-stream" : anexo.ContentType!;
        return (stream, name, ctype);
    }
}
