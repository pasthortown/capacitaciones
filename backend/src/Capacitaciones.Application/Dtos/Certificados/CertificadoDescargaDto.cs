namespace Capacitaciones.Application.Dtos.Certificados;

/// <summary>
/// Resultado del caso de uso <c>DescargarCertificadoUseCase</c>. El controller toma el
/// <see cref="FileStream"/>, lo envuelve en un <c>FileStreamResult</c> con
/// <c>Content-Type: application/pdf</c> y <c>Content-Disposition: attachment; filename=...</c>.
/// El stream viene abierto en modo lectura; el caller es responsable de liberarlo tras enviarlo.
/// </summary>
public class CertificadoDescargaDto
{
    public CertificadoDescargaDto(Stream fileStream, string filename)
    {
        FileStream = fileStream;
        Filename = filename;
    }

    public Stream FileStream { get; }
    public string Filename { get; }
    public string ContentType => "application/pdf";
}
