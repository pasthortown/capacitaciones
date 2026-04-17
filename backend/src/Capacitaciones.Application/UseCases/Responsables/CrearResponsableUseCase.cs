using Capacitaciones.Application.Dtos.Responsables;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Responsables;

/// <summary>
/// Caso de uso admin: alta de un responsable en el catálogo global.
/// Firma es opcional — si no viene, el responsable la carga después desde su link firmado.
/// Los campos se trimean; firma opcional se normaliza (whitespace-only → null).
/// </summary>
public class CrearResponsableUseCase
{
    private readonly IResponsableRepository _repo;

    public CrearResponsableUseCase(IResponsableRepository repo)
    {
        _repo = repo;
    }

    public async Task<ResponsableDetailDto> ExecuteAsync(CreateResponsableDto input, CancellationToken ct = default)
    {
        if (input is null)
            throw new ResponsableServiceException("INVALID_INPUT", "Payload requerido.");

        ResponsableValidator.ValidarNombres(input.Nombres);
        ResponsableValidator.ValidarCargo(input.Cargo);
        ResponsableValidator.ValidarEmpresa(input.Empresa);

        var entity = new Domain.Entities.Responsable
        {
            Id = Guid.NewGuid(),
            Nombres = input.Nombres.Trim(),
            Cargo = input.Cargo.Trim(),
            Empresa = input.Empresa.Trim(),
            Firma = ResponsableValidator.TrimToNull(input.Firma),
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = null
        };

        await _repo.AddAsync(entity, ct);

        return ResponsableMapper.ToDetail(entity);
    }
}
