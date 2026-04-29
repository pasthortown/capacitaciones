using System.Net.Http.Json;
using System.Text.Json;
using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Infrastructure.Services;

/// <summary>
/// Adapter HTTP tipado para <see cref="IMailSenderClient"/>. Se registra como
/// <c>HttpClient</c> con <c>BaseAddress</c> y <c>Timeout</c> tomados de
/// <see cref="MailSenderOptions"/> en <c>Program.cs</c>. Serializa con
/// camelCase salvo donde la DTO declara explícitamente otro nombre vía
/// <c>[JsonPropertyName]</c> (ej. <c>content_base64</c> del adjunto).
/// </summary>
public class MailSenderHttpClient : IMailSenderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;

    public MailSenderHttpClient(HttpClient http)
    {
        _http = http;
    }

    public async Task SendMailAsync(SendMailRequest request, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("send-mail", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }
}
