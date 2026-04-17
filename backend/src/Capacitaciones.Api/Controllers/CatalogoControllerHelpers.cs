using Capacitaciones.Application.Dtos;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Catalogos;
using Capacitaciones.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Lógica compartida para los 3 controladores de catálogos. Evita duplicación
/// sin introducir un controlador genérico (que complica el routing en MVC).
/// </summary>
internal static class CatalogoControllerHelpers
{
    public static async Task<IActionResult> ListAsync<T>(
        CatalogoService<T> service,
        bool includeInactive,
        CancellationToken ct) where T : CatalogoBase, new()
    {
        var items = await service.ListarAsync(includeInactive, ct);
        return new OkObjectResult(items);
    }

    public static async Task<IActionResult> GetAsync<T>(
        CatalogoService<T> service,
        Guid id,
        CancellationToken ct) where T : CatalogoBase, new()
    {
        var dto = await service.ObtenerAsync(id, ct);
        return dto is null ? new NotFoundResult() : new OkObjectResult(dto);
    }

    public static async Task<IActionResult> CreateAsync<T>(
        CatalogoService<T> service,
        UpsertCatalogoDto input,
        string routeName,
        IUrlHelper url,
        CancellationToken ct) where T : CatalogoBase, new()
    {
        try
        {
            var dto = await service.CrearAsync(input, ct);
            var location = url.Action(
                action: "GetById",
                values: new { id = dto.Id }) ?? string.Empty;
            return new CreatedResult(location, dto);
        }
        catch (CatalogoServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    public static async Task<IActionResult> UpdateAsync<T>(
        CatalogoService<T> service,
        Guid id,
        UpsertCatalogoDto input,
        CancellationToken ct) where T : CatalogoBase, new()
    {
        try
        {
            var dto = await service.EditarAsync(id, input, ct);
            return new OkObjectResult(dto);
        }
        catch (CatalogoNotFoundException)
        {
            return new NotFoundResult();
        }
        catch (CatalogoServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    public static async Task<IActionResult> DeleteAsync<T>(
        CatalogoService<T> service,
        Guid id,
        CancellationToken ct) where T : CatalogoBase, new()
    {
        try
        {
            await service.EliminarLogicoAsync(id, ct);
            return new NoContentResult();
        }
        catch (CatalogoNotFoundException)
        {
            return new NotFoundResult();
        }
        catch (CatalogoServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    public static IActionResult Plantilla(
        IXlsxTemplateService xlsx,
        string slug,
        string fileName)
    {
        var bytes = xlsx.BuildTemplate(slug);
        return new FileContentResult(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        {
            FileDownloadName = fileName
        };
    }

    public static async Task<IActionResult> ImportarAsync<T>(
        CatalogoService<T> service,
        IXlsxTemplateService xlsx,
        string slug,
        IFormFile? file,
        CancellationToken ct) where T : CatalogoBase, new()
    {
        if (file is null || file.Length == 0)
        {
            return new BadRequestObjectResult(new { error = "MISSING_FILE", message = "Debe adjuntar un archivo XLSX en el campo 'file'." });
        }

        ImportResult result;
        await using (var stream = file.OpenReadStream())
        {
            result = xlsx.ImportRows(slug, stream);
        }

        // Si no hay errores de validación, persistir las filas válidas.
        // Nota: no se persiste nada si hay errores — el usuario debe corregir y reintentar.
        if (result.Errores.Count == 0 && result.FilasValidas > 0)
        {
            // Releer para obtener los valores concretos (el primer pase sólo valida).
            await using var stream = file.OpenReadStream();
            var rows = LeerFilasValidas(xlsx, slug, stream);
            await service.ImportarFilasAsync(rows, ct);
        }

        return new OkObjectResult(result);
    }

    private static IEnumerable<UpsertCatalogoDto> LeerFilasValidas(
        IXlsxTemplateService xlsx,
        string slug,
        Stream stream)
    {
        // Reutilizamos ImportRows para filtrar filas válidas pero necesitamos los datos:
        // ClosedXmlTemplateService sólo retorna el resumen. Para mantener la lógica en un
        // único lugar, releemos usando un importador inline (hermano del validador).
        // Implementación simple: cargar con ClosedXML de nuevo.
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        int colNombre = -1, colActivo = -1;
        foreach (var cell in sheet.Row(1).CellsUsed())
        {
            var h = (cell.GetString() ?? string.Empty).Trim();
            if (string.Equals(h, "Nombre", StringComparison.OrdinalIgnoreCase)) colNombre = cell.Address.ColumnNumber;
            else if (string.Equals(h, "Activo", StringComparison.OrdinalIgnoreCase)) colActivo = cell.Address.ColumnNumber;
        }

        var list = new List<UpsertCatalogoDto>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int row = 2; row <= lastRow; row++)
        {
            var nombre = colNombre > 0 ? (sheet.Cell(row, colNombre).GetString() ?? string.Empty).Trim() : string.Empty;
            var activoRaw = colActivo > 0 ? (sheet.Cell(row, colActivo).GetString() ?? string.Empty).Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(nombre)) continue;
            if (nombre.Length > 255) continue;
            if (!vistos.Add(nombre)) continue;

            bool activo = true;
            if (!string.IsNullOrWhiteSpace(activoRaw))
            {
                var n = activoRaw.ToLowerInvariant().Replace("í", "i");
                activo = n is "1" or "si" or "s" or "yes" or "y" or "true" or "verdadero";
                if (!activo && n is not ("0" or "no" or "n" or "false" or "falso"))
                {
                    // valor desconocido: fue marcado error por el validador, esta fila no debería llegar acá.
                    continue;
                }
            }

            list.Add(new UpsertCatalogoDto { Nombre = nombre, Activo = activo });
        }
        return list;
    }

    private static ObjectResult ToProblem(CatalogoServiceException ex)
    {
        var status = ex.Codigo switch
        {
            "DUPLICATE_NAME" => StatusCodes.Status409Conflict,
            "EMPTY_NAME" => StatusCodes.Status400BadRequest,
            "NAME_TOO_LONG" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
