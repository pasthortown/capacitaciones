using Capacitaciones.Application.UseCases.Recursos;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Endpoint público (sin autenticación) para descargar recursos del repositorio.
/// El admin genera el link con <c>POST /api/recursos/{id}/link</c> y lo comparte.
/// El contenido se sirve vía streaming para no cargar el archivo completo en memoria.
/// </summary>
[ApiController]
[Route("api/publico/recursos")]
public class PublicoRecursosController : ControllerBase
{
    private readonly DescargarRecursoUseCase _descargar;

    public PublicoRecursosController(DescargarRecursoUseCase descargar)
    {
        _descargar = descargar;
    }

    [HttpGet("{id:guid}/descargar")]
    public async Task<IActionResult> Descargar(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _descargar.ExecuteAsync(id, ct);

            // Content-Disposition RFC 5987 para nombres con caracteres no-ASCII.
            // Regla: "filename" ASCII-safe como fallback + "filename*=UTF-8''<pct-encoded>".
            var asciiFallback = BuildAsciiFallback(result.NombreOriginal);
            var utf8Encoded = Uri.EscapeDataString(result.NombreOriginal);
            Response.Headers["Content-Disposition"] =
                $"attachment; filename=\"{asciiFallback}\"; filename*=UTF-8''{utf8Encoded}";

            return File(result.Content, result.ContentType, enableRangeProcessing: false);
        }
        catch (RecursoNotFoundException)
        {
            return NotFound();
        }
        catch (ArchivoFisicoAusenteException ex)
        {
            // 410 Gone: la metadata existe pero el archivo ya no está disponible.
            return new ObjectResult(new { error = ex.Codigo, message = ex.Message })
            {
                StatusCode = StatusCodes.Status410Gone
            };
        }
    }

    /// <summary>
    /// Sustituye caracteres no-ASCII y los que ensucian el header (CR/LF/quote) por un
    /// guion bajo, quedándonos con un fallback legible para clientes que no soporten RFC 5987.
    /// </summary>
    private static string BuildAsciiFallback(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "recurso";
        var chars = nombre.Select(c =>
        {
            if (c < 0x20 || c == '"' || c == '\\' || c > 0x7E) return '_';
            return c;
        }).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "recurso" : result;
    }
}
