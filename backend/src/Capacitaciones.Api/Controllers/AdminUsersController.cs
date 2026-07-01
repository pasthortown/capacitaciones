using System.Security.Claims;
using Capacitaciones.Application.Dtos.Admin;
using Capacitaciones.Application.UseCases;
using Capacitaciones.Application.UseCases.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly CrearAdminUseCase _crear;
    private readonly ListarAdminsUseCase _listar;
    private readonly EliminarAdminUseCase _eliminar;

    public AdminUsersController(
        CrearAdminUseCase crear,
        ListarAdminsUseCase listar,
        EliminarAdminUseCase eliminar)
    {
        _crear = crear;
        _listar = listar;
        _eliminar = eliminar;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _listar.ExecuteAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAdminUserDto input, CancellationToken ct)
    {
        try
        {
            var dto = await _crear.ExecuteAsync(input, ct);
            return CreatedAtAction(nameof(List), new { id = dto.Id }, dto);
        }
        catch (AuthServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentId = CurrentUserId();
        if (currentId is null)
        {
            return Unauthorized();
        }

        try
        {
            await _eliminar.ExecuteAsync(id, currentId.Value, ct);
            return NoContent();
        }
        catch (AuthServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    private Guid? CurrentUserId()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private IActionResult ToProblem(AuthServiceException ex)
    {
        var status = ex.Codigo switch
        {
            "DUPLICATE_EMAIL" => StatusCodes.Status409Conflict,
            "DUPLICATE_USUARIO" => StatusCodes.Status409Conflict,
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "SELF_DELETE_FORBIDDEN" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
