namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto que encapsula la generación de códigos QR. El adapter de
/// Infrastructure usa la librería QRCoder. Devuelve el PNG codificado en base64
/// listo para incrustar en plantillas de correo (<c>data:image/png;base64,...</c>)
/// o en endpoints públicos.
/// </summary>
public interface IQrGenerator
{
    /// <summary>Genera un PNG QR del contenido dado y lo devuelve en base64 (sin prefijo data URI).</summary>
    string GeneratePngBase64(string content);
}
