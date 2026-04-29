using Capacitaciones.Application.Dtos.Responsables;
using Capacitaciones.Application.UseCases.Notifications;
using Capacitaciones.Application.UseCases.Responsables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// CRUD admin del catálogo global de responsables + endpoint para generar link firmado.
/// Todas las rutas requieren la policy "Admin".
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/responsables")]
public class ResponsablesController : ControllerBase
{
    // Firmas en base64 pueden ser grandes (>100KB). 10 MB es un margen amplio sin exponer a DoS.
    private const int MaxRequestBodyBytes = 10_000_000;

    private readonly ListarResponsablesUseCase _listar;
    private readonly ObtenerResponsableUseCase _obtener;
    private readonly CrearResponsableUseCase _crear;
    private readonly EditarResponsableUseCase _editar;
    private readonly EliminarResponsableUseCase _eliminar;
    private readonly GenerarLinkResponsableUseCase _generarLink;
    private readonly NotificarResponsableFirmaUseCase _notificarFirma;
    private readonly ILogger<ResponsablesController> _logger;

    public ResponsablesController(
        ListarResponsablesUseCase listar,
        ObtenerResponsableUseCase obtener,
        CrearResponsableUseCase crear,
        EditarResponsableUseCase editar,
        EliminarResponsableUseCase eliminar,
        GenerarLinkResponsableUseCase generarLink,
        NotificarResponsableFirmaUseCase notificarFirma,
        ILogger<ResponsablesController> logger)
    {
        _listar = listar;
        _obtener = obtener;
        _crear = crear;
        _editar = editar;
        _eliminar = eliminar;
        _generarLink = generarLink;
        _notificarFirma = notificarFirma;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var items = await _listar.ExecuteAsync(includeInactive, ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}", Name = "Responsables_GetById")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _obtener.ExecuteAsync(id, ct);
            return Ok(dto);
        }
        catch (ResponsableNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<IActionResult> Create([FromBody] CreateResponsableDto input, CancellationToken ct)
    {
        try
        {
            var dto = await _crear.ExecuteAsync(input, ct);
            await NotificarFirmaAsync(dto.Id, ct);
            var location = Url.Action("GetById", new { id = dto.Id }) ?? string.Empty;
            return Created(location, dto);
        }
        catch (ResponsableServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateResponsableDto input, CancellationToken ct)
    {
        try
        {
            var dto = await _editar.ExecuteAsync(id, input, ct);
            await NotificarFirmaAsync(dto.Id, ct);
            return Ok(dto);
        }
        catch (ResponsableNotFoundException)
        {
            return NotFound();
        }
        catch (ResponsableServiceException ex)
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
        catch (ResponsableNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Genera un link firmado (JWT role=Responsable) para entregárselo al responsable.
    /// Cada invocación emite un token nuevo que convive con los anteriores hasta expirar.
    /// </summary>
    [HttpPost("{id:guid}/link")]
    public async Task<IActionResult> GenerarLink(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _generarLink.ExecuteAsync(id, ct);
            return Ok(dto);
        }
        catch (ResponsableNotFoundException)
        {
            return NotFound();
        }
        catch (ResponsableServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Dispara el correo "Carga tus datos y firma" al responsable. No-bloqueante:
    /// captura cualquier error de mail_sender y lo loggea como warning. Si el
    /// responsable no tiene email, sale en silencio sin fallar el endpoint.
    /// </summary>
    private async Task NotificarFirmaAsync(Guid responsableId, CancellationToken ct)
    {
        try
        {
            await _notificarFirma.ExecuteAsync(responsableId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No se pudo enviar el correo de firma al responsable {ResponsableId}.",
                responsableId);
        }
    }

    private static ObjectResult ToProblem(ResponsableServiceException ex)
    {
        var status = ex.Codigo switch
        {
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "RESPONSABLE_INACTIVO" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
