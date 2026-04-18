using System.Net.Http.Json;
using System.Text.Json;
using Capacitaciones.Application.Dtos.Certificados;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Infrastructure.Services;

/// <summary>
/// Adapter HTTP tipado para el puerto <see cref="IEmisorDocumentosClient"/>.
/// Se registra como <c>HttpClient</c> con <c>BaseAddress</c> y <c>Timeout</c> tomados de
/// <see cref="EmisorDocumentosOptions"/> desde <c>Program.cs</c>. La serialización usa
/// camelCase para respetar el contrato del servicio Node (ver <c>instrucciones.md §7.6</c>).
/// </summary>
public class EmisorDocumentosHttpClient : IEmisorDocumentosClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public EmisorDocumentosHttpClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<EmisionResultado> EmitirAsync(EmisionRequest req, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("emitir/certificado", req, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var resultado = await response.Content.ReadFromJsonAsync<EmisionResultado>(JsonOptions, ct);
        if (resultado is null || string.IsNullOrWhiteSpace(resultado.Ruta))
        {
            throw new HttpRequestException(
                "El servicio emisor_documentos respondió un body vacío o sin campo 'ruta'.");
        }
        return resultado;
    }

    public async Task<EmisionResultado> EmitirReporteAsistenciaAsync(ReporteAsistenciaRequest req, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("emitir/reporte-asistencia", req, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var resultado = await response.Content.ReadFromJsonAsync<EmisionResultado>(JsonOptions, ct);
        if (resultado is null || string.IsNullOrWhiteSpace(resultado.Ruta))
        {
            throw new HttpRequestException(
                "El servicio emisor_documentos respondió un body vacío o sin campo 'ruta' para el reporte.");
        }
        return resultado;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync("health", ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            // Timeout: el emisor no está respondiendo.
            return false;
        }
    }
}
