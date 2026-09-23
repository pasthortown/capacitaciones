using Capacitaciones.Api.Filters;
using Capacitaciones.Application.UseCases.Configuracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>Endpoints para servicios internos de la red Docker. No usa JWT: valida X-Internal-Key.</summary>
[ApiController]
[AllowAnonymous]
[ServiceFilter(typeof(InternalApiKeyFilter))]
[Route("api/internal")]
public class InternalCorreoController : ControllerBase
{
    private readonly ObtenerConfiguracionCorreoInternaUseCase _obtener;

    public InternalCorreoController(ObtenerConfiguracionCorreoInternaUseCase obtener)
    {
        _obtener = obtener;
    }

    [HttpGet("correo-config")]
    public async Task<IActionResult> GetCorreoConfig(CancellationToken ct) =>
        Ok(await _obtener.ExecuteAsync(ct));
}
