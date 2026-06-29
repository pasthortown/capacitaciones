using Capacitaciones.Application.Dtos.Convenios;
using Capacitaciones.Application.UseCases.Convenios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// Módulo Convenios (Entrenamiento). CRUD de convenios con ítems de costo y devengo proporcional,
/// anexo (convenio firmado) e historial por colaborador. Policy Admin.
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/convenios")]
public class ConveniosController : ControllerBase
{
    private const long MaxAnexoBytes = 25_000_000;

    private readonly ListarConveniosUseCase _listar;
    private readonly ObtenerConvenioUseCase _obtener;
    private readonly CrearConvenioUseCase _crear;
    private readonly EditarConvenioUseCase _editar;
    private readonly EliminarConvenioUseCase _eliminar;
    private readonly ListarConveniosPorColaboradorUseCase _historial;
    private readonly SubirAnexoConvenioUseCase _subirAnexo;
    private readonly EliminarAnexoConvenioUseCase _eliminarAnexo;
    private readonly DescargarAnexoConvenioUseCase _descargarAnexo;

    public ConveniosController(
        ListarConveniosUseCase listar,
        ObtenerConvenioUseCase obtener,
        CrearConvenioUseCase crear,
        EditarConvenioUseCase editar,
        EliminarConvenioUseCase eliminar,
        ListarConveniosPorColaboradorUseCase historial,
        SubirAnexoConvenioUseCase subirAnexo,
        EliminarAnexoConvenioUseCase eliminarAnexo,
        DescargarAnexoConvenioUseCase descargarAnexo)
    {
        _listar = listar;
        _obtener = obtener;
        _crear = crear;
        _editar = editar;
        _eliminar = eliminar;
        _historial = historial;
        _subirAnexo = subirAnexo;
        _eliminarAnexo = eliminarAnexo;
        _descargarAnexo = descargarAnexo;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? buscar, [FromQuery] bool incluirInactivos = false, CancellationToken ct = default)
        => Ok(await _listar.ExecuteAsync(buscar, incluirInactivos, ct));

    [HttpGet("{id:guid}", Name = "Convenios_GetById")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
    {
        try { return Ok(await _obtener.ExecuteAsync(id, ct)); }
        catch (ConvenioNotFoundException) { return NotFound(); }
    }

    [HttpGet("colaborador/{cedula}")]
    public async Task<IActionResult> Historial(string cedula, [FromQuery] bool soloVigentes = true, CancellationToken ct = default)
    {
        try { return Ok(await _historial.ExecuteAsync(cedula, soloVigentes, ct)); }
        catch (ConvenioServiceException ex) { return MapError(ex); }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ConvenioRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _crear.ExecuteAsync(req, ct);
            return CreatedAtRoute("Convenios_GetById", new { id = dto.Id }, dto);
        }
        catch (ConvenioServiceException ex) { return MapError(ex); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] ConvenioRequest req, CancellationToken ct)
    {
        try { return Ok(await _editar.ExecuteAsync(id, req, ct)); }
        catch (ConvenioServiceException ex) { return MapError(ex); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        try { await _eliminar.ExecuteAsync(id, ct); return NoContent(); }
        catch (ConvenioNotFoundException) { return NotFound(); }
    }

    // --- Anexo (convenio firmado) ---

    [HttpPost("{id:guid}/anexos")]
    [RequestSizeLimit(MaxAnexoBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxAnexoBytes)]
    public async Task<IActionResult> AgregarAnexo(Guid id, [FromForm] IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { error = "ARCHIVO_VACIO", message = "Debe adjuntar un archivo." });
        try
        {
            await using var stream = archivo.OpenReadStream();
            var dto = await _subirAnexo.ExecuteAsync(id, stream, archivo.Length, archivo.FileName, archivo.ContentType, ct);
            return Ok(dto);
        }
        catch (ConvenioServiceException ex) { return MapError(ex); }
    }

    [HttpDelete("{id:guid}/anexos/{anexoId:guid}")]
    public async Task<IActionResult> EliminarAnexo(Guid id, Guid anexoId, CancellationToken ct)
    {
        try { await _eliminarAnexo.ExecuteAsync(id, anexoId, ct); return NoContent(); }
        catch (ConvenioNotFoundException) { return NotFound(); }
        catch (ConvenioServiceException ex) { return MapError(ex); }
    }

    [HttpGet("{id:guid}/anexos/{anexoId:guid}/descargar")]
    public async Task<IActionResult> DescargarAnexo(Guid id, Guid anexoId, CancellationToken ct)
    {
        try
        {
            var (content, fileName, contentType) = await _descargarAnexo.ExecuteAsync(id, anexoId, ct);
            return File(content, contentType, fileName);
        }
        catch (ConvenioNotFoundException) { return NotFound(); }
        catch (ConvenioServiceException ex) when (ex.Codigo is "ANEXO_AUSENTE" or "ANEXO_NO_ENCONTRADO") { return NotFound(new { error = ex.Codigo, message = ex.Message }); }
    }

    private IActionResult MapError(ConvenioServiceException ex) => ex switch
    {
        ConvenioNotFoundException => NotFound(),
        ConvenioValidacionException => BadRequest(new { error = ex.Codigo, message = ex.Message }),
        _ => new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = StatusCodes.Status409Conflict },
    };
}
