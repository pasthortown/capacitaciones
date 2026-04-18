using Capacitaciones.Application.Dtos.Recursos;
using Capacitaciones.Application.UseCases.Recursos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Endpoints admin del módulo Repositorio. Upload multipart (100 MB máx), CRUD de metadata,
/// baja lógica + borrado físico y generación de link de descarga pública.
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/recursos")]
public class RecursosController : ControllerBase
{
    /// <summary>Límite coherente con <see cref="SubirRecursoUseCase.MaxBytes"/> (100 MB).</summary>
    private const long MaxUploadBytes = 100_000_000;

    private readonly SubirRecursoUseCase _subir;
    private readonly ListarRecursosUseCase _listar;
    private readonly ObtenerRecursoUseCase _obtener;
    private readonly EditarMetadataRecursoUseCase _editar;
    private readonly EliminarRecursoUseCase _eliminar;
    private readonly GenerarLinkDescargaRecursoUseCase _generarLink;

    public RecursosController(
        SubirRecursoUseCase subir,
        ListarRecursosUseCase listar,
        ObtenerRecursoUseCase obtener,
        EditarMetadataRecursoUseCase editar,
        EliminarRecursoUseCase eliminar,
        GenerarLinkDescargaRecursoUseCase generarLink)
    {
        _subir = subir;
        _listar = listar;
        _obtener = obtener;
        _editar = editar;
        _eliminar = eliminar;
        _generarLink = generarLink;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var items = await _listar.ExecuteAsync(includeInactive, ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}", Name = "Recursos_GetById")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _obtener.ExecuteAsync(id, ct);
            return Ok(dto);
        }
        catch (RecursoNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? archivo,
        [FromForm] string? nombre,
        [FromForm] string? descripcion,
        CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return ToProblem(new RecursoServiceException("ARCHIVO_VACIO", "El archivo está vacío o no se recibió."));
        }

        try
        {
            await using var stream = archivo.OpenReadStream();
            var dto = await _subir.ExecuteAsync(
                stream,
                archivo.Length,
                archivo.FileName,
                nombre,
                descripcion ?? string.Empty,
                archivo.ContentType,
                ct);

            var location = Url.Action("GetById", new { id = dto.Id }) ?? string.Empty;
            return Created(location, dto);
        }
        catch (RecursoServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Edita la metadata del recurso y, opcionalmente, reemplaza el archivo físico.
    /// El endpoint es multipart para soportar el upload opcional; cuando <c>archivo</c> es null
    /// o vacío sólo se actualiza la metadata.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromForm] string? nombreOriginal,
        [FromForm] string? descripcion,
        [FromForm] IFormFile? archivo,
        CancellationToken ct)
    {
        var input = new UpdateRecursoMetadataDto
        {
            NombreOriginal = nombreOriginal ?? string.Empty,
            Descripcion = descripcion ?? string.Empty
        };

        try
        {
            RecursoDetailDto dto;
            if (archivo is { Length: > 0 })
            {
                await using var stream = archivo.OpenReadStream();
                dto = await _editar.ExecuteAsync(
                    id,
                    input,
                    stream,
                    archivo.Length,
                    archivo.FileName,
                    archivo.ContentType,
                    ct);
            }
            else
            {
                dto = await _editar.ExecuteAsync(id, input, ct);
            }
            return Ok(dto);
        }
        catch (RecursoNotFoundException)
        {
            return NotFound();
        }
        catch (RecursoServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _eliminar.ExecuteAsync(id, ct);
            return NoContent();
        }
        catch (RecursoNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/link")]
    public async Task<IActionResult> GenerarLink(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _generarLink.ExecuteAsync(id, ct);
            return Ok(dto);
        }
        catch (RecursoNotFoundException)
        {
            return NotFound();
        }
    }

    private static ObjectResult ToProblem(RecursoServiceException ex)
    {
        var status = ex.Codigo switch
        {
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "ARCHIVO_DEMASIADO_GRANDE" => StatusCodes.Status413PayloadTooLarge,
            "EXTENSION_PROHIBIDA" => StatusCodes.Status400BadRequest,
            "DESCRIPCION_REQUERIDA" => StatusCodes.Status400BadRequest,
            "DESCRIPCION_INVALIDA" => StatusCodes.Status400BadRequest,
            "ARCHIVO_REQUERIDO" => StatusCodes.Status400BadRequest,
            "ARCHIVO_VACIO" => StatusCodes.Status400BadRequest,
            "NOMBRE_REQUERIDO" => StatusCodes.Status400BadRequest,
            "NOMBRE_INVALIDO" => StatusCodes.Status400BadRequest,
            "INVALID_INPUT" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };
        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
