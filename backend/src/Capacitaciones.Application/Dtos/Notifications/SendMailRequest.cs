using System.Text.Json.Serialization;

namespace Capacitaciones.Application.Dtos.Notifications;

/// <summary>
/// Payload que el backend envía al servicio <c>mail_sender</c>. La forma del
/// JSON debe coincidir 1-a-1 con <c>SendMailRequest</c> definido en
/// <c>mail_sender/app.py</c> (campos en camelCase via <see cref="System.Text.Json"/>
/// con <c>JsonSerializerDefaults.Web</c>).
/// </summary>
public class SendMailRequest
{
    public string Template { get; set; } = string.Empty;
    public Dictionary<string, object?> Parameters { get; set; } = new();
    public List<string> Recipients { get; set; } = new();
    public string Subject { get; set; } = string.Empty;
    public List<string>? Cc { get; set; }
    public List<string>? Bcc { get; set; }
    public MailAttachment? Attachment { get; set; }
}

/// <summary>
/// Adjunto opcional (mapea al sub-objeto <c>attachment</c> del servicio Python).
/// El Pydantic del servicio expone los campos como snake_case
/// (<c>content_base64</c>, <c>mime_type</c>), así que se anotan para que la
/// serialización camelCase del backend no rompa el contrato.
/// </summary>
public class MailAttachment
{
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("content_base64")]
    public string ContentBase64 { get; set; } = string.Empty;

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }
}
