using Capacitaciones.Application.Ports;
using QRCoder;

namespace Capacitaciones.Infrastructure.Services;

/// <summary>
/// Adapter de <see cref="IQrGenerator"/> usando <see href="https://github.com/codebude/QRCoder">QRCoder</see>
/// (MIT). Devuelve PNG en base64 listo para incrustar como
/// <c>&lt;img src="data:image/png;base64,..."&gt;</c>.
///
/// Nivel de corrección de errores <c>Q</c> (~25%) — buen compromiso entre
/// capacidad y resistencia ante deformaciones del cliente de correo. Pixel
/// size 6 deja un QR cómodo para escanear en pantalla y al imprimir.
/// </summary>
public class QrCoderQrGenerator : IQrGenerator
{
    public string GeneratePngBase64(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(pixelsPerModule: 6);
        return Convert.ToBase64String(bytes);
    }
}
