namespace Capacitaciones.Application.Dtos.Certificados;

/// <summary>
/// Respuesta del emisor tras emitir un certificado (<c>201 Created</c>).
/// <c>Ruta</c> es la ruta absoluta del PDF dentro del volumen compartido <c>/output/</c>
/// (ej. <c>/output/CAP-PC-REG-001_1712345678.pdf</c>).
/// </summary>
public class EmisionResultado
{
    public string Ruta { get; set; } = string.Empty;
}
