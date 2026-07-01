using System.IO.Compression;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Convenios;

/// <summary>Utilidades para empaquetar archivos de convenios en un ZIP.</summary>
internal static class ConvenioZipHelpers
{
    /// <summary>Limpia un nombre para usarlo como entrada de ZIP (sin separadores de ruta).</summary>
    public static string SafeEntry(string? name)
        => string.IsNullOrWhiteSpace(name)
            ? "archivo"
            : name.Replace('/', '_').Replace('\\', '_').Trim();

    public static async Task AddStreamAsync(ZipArchive zip, string entryName, Stream src, CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var es = entry.Open();
        await using (src)
        {
            await src.CopyToAsync(es, ct);
        }
    }
}

/// <summary>Descarga en un ZIP todos los anexos de un convenio (para el botón por fila "Descargar").</summary>
public class DescargarAnexosConvenioZipUseCase
{
    private readonly IConvenioRepository _repo;
    private readonly IConvenioAnexoStorage _storage;

    public DescargarAnexosConvenioZipUseCase(IConvenioRepository repo, IConvenioAnexoStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<(MemoryStream Content, string FileName)> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _repo.GetByIdAsync(id, ct) ?? throw new ConvenioNotFoundException(id);
        var codigo = c.NumeroRegistro is int n ? IConvenioNumeracionService.Format(n) : c.Id.ToString();

        var mem = new MemoryStream();
        using (var zip = new ZipArchive(mem, ZipArchiveMode.Create, leaveOpen: true))
        {
            var i = 0;
            foreach (var a in c.Anexos)
            {
                if (!_storage.Exists(a.NombreAlmacenado)) continue;
                i++;
                var name = ConvenioZipHelpers.SafeEntry(a.NombreOriginal ?? a.NombreAlmacenado);
                await ConvenioZipHelpers.AddStreamAsync(zip, $"{i:00}_{name}", _storage.OpenRead(a.NombreAlmacenado), ct);
            }
        }
        mem.Position = 0;
        return (mem, $"Anexos_{codigo}.zip");
    }
}

/// <summary>
/// Paquete de desvinculación: un ZIP con el reporte de liquidación + el PDF de cada convenio del
/// colaborador + todos los anexos cargados en esos convenios. Para el botón "Descargar todo".
/// </summary>
public class PaqueteDesvinculacionUseCase
{
    private readonly IConvenioRepository _repo;
    private readonly ImprimirConvenioUseCase _imprimir;
    private readonly DescargarReporteLiquidacionUseCase _reporteLiquidacion;
    private readonly IConvenioAnexoStorage _storage;

    public PaqueteDesvinculacionUseCase(
        IConvenioRepository repo,
        ImprimirConvenioUseCase imprimir,
        DescargarReporteLiquidacionUseCase reporteLiquidacion,
        IConvenioAnexoStorage storage)
    {
        _repo = repo;
        _imprimir = imprimir;
        _reporteLiquidacion = reporteLiquidacion;
        _storage = storage;
    }

    public async Task<(MemoryStream Content, string FileName)> ExecuteAsync(string cedula, DateTime fechaSalida, CancellationToken ct = default)
    {
        var ced = (cedula ?? string.Empty).Trim();

        var mem = new MemoryStream();
        using (var zip = new ZipArchive(mem, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 1) Reporte de liquidación por desvinculación.
            try
            {
                var rep = await _reporteLiquidacion.ExecuteAsync(ced, fechaSalida, ct);
                await ConvenioZipHelpers.AddStreamAsync(zip, $"Liquidacion_{ConvenioZipHelpers.SafeEntry(ced)}.pdf", rep.FileStream, ct);
            }
            catch (HttpRequestException) { /* emisor no disponible: se continúa con lo demás */ }

            // 2) Por cada convenio del colaborador: su PDF + sus anexos.
            var convenios = await _repo.ListByCedulaAsync(ced, includeInactive: false, ct);
            foreach (var c in convenios)
            {
                var codigo = c.NumeroRegistro is int n ? IConvenioNumeracionService.Format(n) : c.Id.ToString();

                try
                {
                    var pdf = await _imprimir.ExecuteAsync(c.Id, ct);
                    await ConvenioZipHelpers.AddStreamAsync(zip, $"{codigo}/Convenio_{codigo}.pdf", pdf.FileStream, ct);
                }
                catch (HttpRequestException) { /* si falla el emisor para uno, se continúa */ }

                var i = 0;
                foreach (var a in c.Anexos)
                {
                    if (!_storage.Exists(a.NombreAlmacenado)) continue;
                    i++;
                    var name = ConvenioZipHelpers.SafeEntry(a.NombreOriginal ?? a.NombreAlmacenado);
                    await ConvenioZipHelpers.AddStreamAsync(zip, $"{codigo}/anexos/{i:00}_{name}", _storage.OpenRead(a.NombreAlmacenado), ct);
                }
            }
        }
        mem.Position = 0;
        return (mem, $"Desvinculacion_{ConvenioZipHelpers.SafeEntry(ced)}.zip");
    }
}
