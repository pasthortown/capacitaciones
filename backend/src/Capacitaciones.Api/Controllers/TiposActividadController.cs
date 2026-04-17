using Capacitaciones.Application.Dtos;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Catalogos;
using Capacitaciones.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

// TODO Fase 2: [Authorize(Policy = "Admin")]
[ApiController]
[Route("api/catalogos/tipos-actividad")]
public class TiposActividadController : ControllerBase
{
    private const string Slug = CatalogoSlug.TiposActividad;

    private readonly CatalogoService<TipoActividad> _service;
    private readonly IXlsxTemplateService _xlsx;

    public TiposActividadController(CatalogoService<TipoActividad> service, IXlsxTemplateService xlsx)
    {
        _service = service;
        _xlsx = xlsx;
    }

    [HttpGet]
    public Task<IActionResult> List([FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        CatalogoControllerHelpers.ListAsync(_service, includeInactive, ct);

    [HttpGet("{id:guid}", Name = "TiposActividad_GetById")]
    public Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CatalogoControllerHelpers.GetAsync(_service, id, ct);

    [HttpPost]
    public Task<IActionResult> Create([FromBody] UpsertCatalogoDto input, CancellationToken ct) =>
        CatalogoControllerHelpers.CreateAsync(_service, input, "TiposActividad_GetById", Url, ct);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] UpsertCatalogoDto input, CancellationToken ct) =>
        CatalogoControllerHelpers.UpdateAsync(_service, id, input, ct);

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CatalogoControllerHelpers.DeleteAsync(_service, id, ct);

    [HttpGet("plantilla")]
    public IActionResult Plantilla() =>
        CatalogoControllerHelpers.Plantilla(_xlsx, Slug, "plantilla_tipos_actividad.xlsx");

    [HttpPost("importar")]
    public Task<IActionResult> Importar([FromForm] IFormFile? file, CancellationToken ct) =>
        CatalogoControllerHelpers.ImportarAsync(_service, _xlsx, Slug, file, ct);
}
