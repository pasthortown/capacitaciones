namespace Capacitaciones.Infrastructure.Services;

/// <summary>
/// Opciones bindeadas desde la sección <c>EmisorDocumentos</c> de <c>appsettings</c>.
/// Se alimentan también desde env vars con prefijo <c>EmisorDocumentos__</c> (ej.
/// <c>EmisorDocumentos__BaseUrl</c>).
/// </summary>
public class EmisorDocumentosOptions
{
    public const string SectionName = "EmisorDocumentos";

    /// <summary>
    /// URL base del servicio. En Docker Compose el contenedor backend llega al emisor
    /// por el nombre del servicio en la red interna <c>capacitaciones-net</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "http://emisor_documentos:3000";

    /// <summary>
    /// Timeout del <c>HttpClient</c> (en segundos). Puppeteer puede tardar varios segundos
    /// en cargar fuentes + renderizar + imprimir; 120s cubre cómodamente un render normal
    /// y deja margen si el contenedor acaba de arrancar.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}
