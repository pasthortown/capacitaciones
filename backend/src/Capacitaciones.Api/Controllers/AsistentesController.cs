using Capacitaciones.Application.Dtos.Calificaciones;
using Capacitaciones.Application.Dtos.PaseLista;
using Capacitaciones.Application.UseCases.Asistentes;
using Capacitaciones.Application.UseCases.Calificaciones;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Application.UseCases.Certificados;
using Capacitaciones.Application.UseCases.PaseLista;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Admin — listado de asistentes de una capacitación, descarga del certificado (Fase 6)
/// y corrección de asistencia por fila (Fase 10).
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/capacitaciones/{capacitacionId:guid}/asistentes")]
public class AsistentesController : ControllerBase
{
    private readonly ListarAsistentesUseCase _listar;
    private readonly DescargarCertificadoUseCase _descargarCertificado;
    private readonly MarcarAsistenciaUseCase _marcarAsistencia;
    private readonly CalificarAsistenteUseCase _calificar;

    public AsistentesController(
        ListarAsistentesUseCase listar,
        DescargarCertificadoUseCase descargarCertificado,
        MarcarAsistenciaUseCase marcarAsistencia,
        CalificarAsistenteUseCase calificar)
    {
        _listar = listar;
        _descargarCertificado = descargarCertificado;
        _marcarAsistencia = marcarAsistencia;
        _calificar = calificar;
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
        catch (CertificadoAsistenteNoElegibleException ex)
        {
            // Fase 12: el asistente está ausente o sin marcar; 409 con motivo para el UI.
            return new ObjectResult(new
            {
                error = ex.Codigo,
                message = ex.Message,
                motivo = ex.Motivo
            })
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

    /// <summary>
    /// Fase 10: corrección admin de la asistencia de un asistente desde la tabla de listado.
    /// Acepta <c>"Presente"</c>, <c>"Ausente"</c> o <c>null</c> (limpiar marcación).
    /// </summary>
    [HttpPut("{asistenteId:guid}/asistencia")]
    public async Task<IActionResult> MarcarAsistencia(
        Guid capacitacionId,
        Guid asistenteId,
        [FromBody] MarcarAsistenciaDto? input,
        CancellationToken ct)
    {
        try
        {
            var estado = MarcarAsistenciaUseCase.ParseEstado(input?.EstadoAsistencia);
            var dto = await _marcarAsistencia.ExecuteAsync(capacitacionId, asistenteId, estado, ct);
            return Ok(dto);
        }
        catch (AsistenteNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitadorForbiddenException ex)
        {
            return new ObjectResult(new { error = "CAPACITACION_INACTIVA", message = ex.Message })
            {
                StatusCode = StatusCodes.Status409Conflict
            };
        }
        catch (CapacitacionServiceException ex)
        {
            var status = ex.Codigo switch
            {
                "ESTADO_ASISTENCIA_INVALIDO" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest
            };
            return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
        }
    }

    /// <summary>
    /// Fase 11: edición admin de la calificación de un asistente desde la tabla de listado.
    /// Acepta <c>null</c> (limpiar) o un decimal en [0..10].
    /// </summary>
    [HttpPut("{asistenteId:guid}/calificacion")]
    public async Task<IActionResult> Calificar(
        Guid capacitacionId,
        Guid asistenteId,
        [FromBody] CalificarAsistenteDto? input,
        CancellationToken ct)
    {
        try
        {
            var dto = await _calificar.ExecuteAsync(capacitacionId, asistenteId, input?.Calificacion, ct);
            return Ok(dto);
        }
        catch (AsistenteNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitadorForbiddenException ex)
        {
            return new ObjectResult(new { error = "CAPACITACION_INACTIVA", message = ex.Message })
            {
                StatusCode = StatusCodes.Status409Conflict
            };
        }
        catch (CapacitacionServiceException ex)
        {
            var status = ex.Codigo switch
            {
                "CALIFICACIONES_NO_APLICA" => StatusCodes.Status409Conflict,
                "ASISTENTE_NO_PRESENTE" => StatusCodes.Status409Conflict,
                "CALIFICACION_FUERA_DE_RANGO" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest
            };
            return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
        }
    }
}
