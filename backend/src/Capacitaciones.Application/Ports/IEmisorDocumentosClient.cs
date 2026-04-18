using Capacitaciones.Application.Dtos.Certificados;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto hexagonal que abstrae al servicio externo <c>emisor_documentos</c>
/// (Node + Puppeteer, HTTP interno en <c>http://emisor_documentos:3000</c>).
/// La capa Infrastructure provee un adapter basado en <c>HttpClient</c>; los tests
/// inyectan un fake para evitar red real.
/// </summary>
public interface IEmisorDocumentosClient
{
    /// <summary>
    /// Invoca <c>POST /emitir/certificado</c>. Lanza <see cref="HttpRequestException"/>
    /// si el emisor no responde o devuelve un código no exitoso — el caller lo traduce
    /// a 503 <c>SERVICIO_EMISOR_NO_DISPONIBLE</c>.
    /// </summary>
    Task<EmisionResultado> EmitirAsync(EmisionRequest req, CancellationToken ct);

    /// <summary>
    /// Invoca <c>GET /health</c>. Devuelve <c>true</c> si responde <c>200 OK</c>,
    /// <c>false</c> en cualquier otro caso (incluyendo excepciones de red).
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken ct);
}
