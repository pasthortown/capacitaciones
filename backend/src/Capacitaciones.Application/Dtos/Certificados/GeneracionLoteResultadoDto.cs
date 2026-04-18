namespace Capacitaciones.Application.Dtos.Certificados;

/// <summary>
/// Resumen devuelto por <c>GenerarCertificadosCapacitacionUseCase</c> al admin.
/// Se devuelve <c>200 OK</c> con este payload incluso si algunos (o todos) los
/// asistentes fallaron — el UI decide cómo presentar los errores.
/// </summary>
public class GeneracionLoteResultadoDto
{
    public int Total { get; set; }
    public int Emitidos { get; set; }
    public List<GeneracionLoteErrorDto> Errores { get; set; } = new();
}

public class GeneracionLoteErrorDto
{
    public Guid AsistenteId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}
