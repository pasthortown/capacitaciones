using Capacitaciones.Application.UseCases.Asistentes;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Certificados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Admin — listado de asistentes de una capacitación y descarga del certificado (Fase 6).
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/capacitaciones/{capacitacionId:guid}/asistentes")]
public class AsistentesController : ControllerBase
{
    private readonly ListarAsistentesUseCase _listar;
    private readonly DescargarCertificadoUseCase _descargarCertificado;

    public AsistentesController(
        ListarAsistentesUseCase listar,
        DescargarCertificadoUseCase descargarCertificado)
    {
        _listar = listar;
        _descargarCertificado = descargarCertificado;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid capacitacionId, CancellationToken ct)
    {
        try
        {
            var items = await _listar.ExecuteAsync(capacitacionId, ct);
            return Ok(items);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Fase 6: descarga el certificado del asistente. Si el PDF aún no fue emitido, se llama
    /// al servicio <c>emisor_documentos</c> de forma implícita y luego se sirve el archivo.
    /// </summary>
    [HttpGet("{asistenteId:guid}/certificado")]
    public async Task<IActionResult> DescargarCertificado(
        Guid capacitacionId,
        Guid asistenteId,
        CancellationToken ct)
    {
        try
        {
            var descarga = await _descargarCertificado.ExecuteAsync(capacitacionId, asistenteId, ct);

            // FileStreamResult se encarga de cerrar el stream tras copiar al response.
            return File(descarga.FileStream, descarga.ContentType, descarga.Filename);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CertificadoFirmasFaltantesException ex)
        {
            return new ObjectResult(new
            {
                error = ex.Codigo,
                message = ex.Message,
                faltantes = ex.Faltantes
            })
            {
                StatusCode = StatusCodes.Status409Conflict
            };
        }
        catch (CertificadoNoDisponibleException ex)
        {
            return new ObjectResult(new { error = ex.Codigo, message = ex.Message })
            {
                StatusCode = StatusCodes.Status409Conflict
            };
        }
        catch (CapacitacionServiceException ex)
        {
            var status = ex.Codigo switch
            {
                "CAPACITACION_NO_FINALIZADA" => StatusCodes.Status409Conflict,
                "ASISTENTE_NOT_FOUND" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest
            };
            return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
        }
        catch (HttpRequestException ex)
        {
            // El emisor está caído o respondió con un status no exitoso.
            return new ObjectResult(new
            {
                error = "SERVICIO_EMISOR_NO_DISPONIBLE",
                message = $"No se pudo contactar al servicio emisor_documentos: {ex.Message}"
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
        }
    }
}
