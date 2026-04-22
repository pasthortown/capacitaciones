using System.Net.Http.Json;
using System.Text.Json;
using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Infrastructure.Services;

/// <summary>
/// Opciones del servicio externo <c>emisor_reportes</c> (Python).
/// Configurable vía <c>EmisorReportes:BaseUrl</c> + <c>EmisorReportes:TimeoutSeconds</c>.
/// </summary>
public class EmisorReportesOptions
{
    public const string SectionName = "EmisorReportes";
    public string BaseUrl { get; set; } = "http://emisor_reportes:5000";
    public int TimeoutSeconds { get; set; } = 120;
}

public class EmisorReportesHttpClient : IEmisorReportesClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public EmisorReportesHttpClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> EmitirReporteEncuestaAsync(
        ResultadoEncuestaDto payload,
        CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync("emitir/reporte-encuesta", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var resultado = await response.Content.ReadFromJsonAsync<EmisionRespuesta>(JsonOptions, ct);
        if (resultado is null || string.IsNullOrWhiteSpace(resultado.Ruta))
        {
            throw new HttpRequestException(
                "El servicio emisor_reportes respondió body vacío o sin campo 'ruta'.");
        }
        return resultado.Ruta;
    }

    private sealed record EmisionRespuesta(string Ruta);
}
