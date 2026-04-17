namespace Capacitaciones.Application.Dtos;

/// <summary>
/// Resultado de una importación XLSX de catálogo.
/// </summary>
public class ImportResult
{
    public int TotalFilas { get; set; }
    public int FilasValidas { get; set; }
    public List<ImportRowError> Errores { get; set; } = new();
}

/// <summary>
/// Error de validación a nivel de celda/fila dentro de un XLSX importado.
/// </summary>
public class ImportRowError
{
    public int Fila { get; set; }
    public string Campo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}
