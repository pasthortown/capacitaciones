namespace Capacitaciones.Application.Dtos.Certificados;

/// <summary>
/// Resultado del caso de uso <c>GenerarCertificadoAsistenteUseCase</c>.
/// <c>Ruta</c> es la ruta devuelta por el emisor dentro del volumen <c>/output/</c>;
/// <c>Filename</c> es el último segmento (lo que el backend usará para servir el archivo).
/// </summary>
public class CertificadoEmitidoDto
{
    public string Ruta { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
}
