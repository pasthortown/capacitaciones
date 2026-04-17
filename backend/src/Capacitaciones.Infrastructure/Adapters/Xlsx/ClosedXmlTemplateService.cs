using Capacitaciones.Application.Dtos;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Catalogos;
using ClosedXML.Excel;

namespace Capacitaciones.Infrastructure.Adapters.Xlsx;

/// <summary>
/// Implementación de <see cref="IXlsxTemplateService"/> basada en ClosedXML.
/// Plantilla: hoja única con columnas "Nombre" y "Activo" (default TRUE), encabezados en negrita.
/// Import: lee Nombre y Activo con tolerancia a TRUE/FALSE, Sí/No, 1/0.
/// </summary>
public class ClosedXmlTemplateService : IXlsxTemplateService
{
    private const int NombreMaxLength = 255;
    private const string ColumnaNombre = "Nombre";
    private const string ColumnaActivo = "Activo";

    public byte[] BuildTemplate(string catalogoTipo)
    {
        var slug = NormalizarSlug(catalogoTipo);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(ObtenerTitulo(slug));

        // Encabezados en negrita.
        sheet.Cell(1, 1).Value = ColumnaNombre;
        sheet.Cell(1, 2).Value = ColumnaActivo;
        var header = sheet.Range(1, 1, 1, 2);
        header.Style.Font.Bold = true;

        // Ancho de columnas amigable.
        sheet.Column(1).Width = 40;
        sheet.Column(2).Width = 12;

        // Validación de lista TRUE/FALSE para Activo (2..1000). Si se deja vacío, el import
        // asume TRUE por defecto.
        var activoRange = sheet.Range(2, 2, 1000, 2);
        var validation = activoRange.CreateDataValidation();
        validation.List("\"TRUE,FALSE\"", true);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public ImportResult ImportRows(string catalogoTipo, Stream xlsx)
    {
        _ = NormalizarSlug(catalogoTipo);

        var result = new ImportResult();
        using var workbook = new XLWorkbook(xlsx);
        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null)
        {
            result.Errores.Add(new ImportRowError
            {
                Fila = 0,
                Campo = string.Empty,
                Mensaje = "El archivo XLSX no contiene hojas."
            });
            return result;
        }

        // Mapear encabezados (fila 1) a índices de columna.
        var headerRow = sheet.Row(1);
        int colNombre = -1;
        int colActivo = -1;
        foreach (var cell in headerRow.CellsUsed())
        {
            var h = (cell.GetString() ?? string.Empty).Trim();
            if (string.Equals(h, ColumnaNombre, StringComparison.OrdinalIgnoreCase))
                colNombre = cell.Address.ColumnNumber;
            else if (string.Equals(h, ColumnaActivo, StringComparison.OrdinalIgnoreCase))
                colActivo = cell.Address.ColumnNumber;
        }

        if (colNombre < 0)
        {
            result.Errores.Add(new ImportRowError
            {
                Fila = 1,
                Campo = ColumnaNombre,
                Mensaje = $"No se encontró la columna obligatoria '{ColumnaNombre}' en los encabezados."
            });
            return result;
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        var nombresEnArchivo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int row = 2; row <= lastRow; row++)
        {
            var nombreRaw = sheet.Cell(row, colNombre).GetString()?.Trim() ?? string.Empty;
            var activoRaw = colActivo > 0 ? sheet.Cell(row, colActivo).GetString()?.Trim() ?? string.Empty : string.Empty;

            // Saltar filas totalmente vacías sin contabilizar.
            if (string.IsNullOrWhiteSpace(nombreRaw) && string.IsNullOrWhiteSpace(activoRaw))
            {
                continue;
            }

            result.TotalFilas++;
            var erroresFila = new List<ImportRowError>();

            if (string.IsNullOrWhiteSpace(nombreRaw))
            {
                erroresFila.Add(new ImportRowError
                {
                    Fila = row,
                    Campo = ColumnaNombre,
                    Mensaje = "El nombre es requerido."
                });
            }
            else if (nombreRaw.Length > NombreMaxLength)
            {
                erroresFila.Add(new ImportRowError
                {
                    Fila = row,
                    Campo = ColumnaNombre,
                    Mensaje = $"El nombre excede el máximo de {NombreMaxLength} caracteres."
                });
            }
            else if (!nombresEnArchivo.Add(nombreRaw))
            {
                erroresFila.Add(new ImportRowError
                {
                    Fila = row,
                    Campo = ColumnaNombre,
                    Mensaje = $"Nombre duplicado dentro del archivo: '{nombreRaw}'."
                });
            }

            bool activo = true;
            if (!string.IsNullOrWhiteSpace(activoRaw))
            {
                if (!TryParseBool(activoRaw, out activo))
                {
                    erroresFila.Add(new ImportRowError
                    {
                        Fila = row,
                        Campo = ColumnaActivo,
                        Mensaje = $"Valor no válido para 'Activo': '{activoRaw}'. Use TRUE/FALSE, Sí/No o 1/0."
                    });
                }
            }

            if (erroresFila.Count > 0)
            {
                result.Errores.AddRange(erroresFila);
            }
            else
            {
                result.FilasValidas++;
            }
        }

        return result;
    }

    private static string NormalizarSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("El slug de catálogo es requerido.", nameof(slug));

        var normalized = slug.Trim().ToLowerInvariant();
        if (!CatalogoSlug.IsKnown(normalized))
        {
            throw new ArgumentException(
                $"Slug de catálogo desconocido: '{slug}'. Válidos: {string.Join(", ", CatalogoSlug.All)}.",
                nameof(slug));
        }
        return normalized;
    }

    private static string ObtenerTitulo(string slug) => slug switch
    {
        CatalogoSlug.Modalidades => "Modalidades",
        CatalogoSlug.TiposActividad => "TiposActividad",
        CatalogoSlug.Areas => "Areas",
        _ => "Catalogo"
    };

    private static bool TryParseBool(string raw, out bool value)
    {
        var s = raw.Trim();
        if (bool.TryParse(s, out value)) return true;

        // Aceptar SÍ / NO, 1 / 0 (case-insensitive, sin tildes).
        var normalized = s.ToLowerInvariant()
            .Replace("í", "i")
            .Replace("Í", "i");

        switch (normalized)
        {
            case "1":
            case "si":
            case "s":
            case "yes":
            case "y":
            case "true":
            case "verdadero":
                value = true;
                return true;
            case "0":
            case "no":
            case "n":
            case "false":
            case "falso":
                value = false;
                return true;
        }
        return false;
    }
}
