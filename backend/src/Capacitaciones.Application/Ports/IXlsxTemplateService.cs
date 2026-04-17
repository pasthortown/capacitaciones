using Capacitaciones.Application.Dtos;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto para la generación y lectura de plantillas XLSX de catálogos.
/// </summary>
public interface IXlsxTemplateService
{
    /// <summary>
    /// Genera una plantilla XLSX vacía con los encabezados del catálogo indicado.
    /// </summary>
    byte[] BuildTemplate(string catalogoTipo);

    /// <summary>
    /// Lee un XLSX con filas para un catálogo, valida y retorna el resultado con errores y filas válidas.
    /// </summary>
    ImportResult ImportRows(string catalogoTipo, Stream xlsx);
}
