using Capacitaciones.Application.Dtos.Responsables;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Responsables;

/// <summary>
/// Caso de uso admin: edición de un responsable del catálogo global.
/// Regla de firma: si <paramref name="input.Firma"/> es null se conserva la firma actual;
/// si es "" / whitespace se limpia (guarda null); si trae contenido se aplica tal cual (trim).
/// Esto permite al admin actualizar datos sin tocar la firma ya cargada por el responsable.
/// </summary>
public class EditarResponsableUseCase
{
    private readonly IResponsableRepository _repo;

    public EditarResponsableUseCase(IResponsableRepository repo)
    {
        _repo = repo;
    }

    public async Task<ResponsableDetailDto> ExecuteAsync(Guid id, UpdateResponsableDto input, CancellationToken ct = default)
    {
        if (input is null)
            throw new ResponsableServiceException("INVALID_INPUT", "Payload requerido.");

        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new ResponsableNotFoundException(id);

        ResponsableValidator.ValidarNombres(input.Nombres);
        ResponsableValidator.ValidarCargo(input.Cargo);
        ResponsableValidator.ValidarEmpresa(input.Empresa);

        entity.Nombres = input.Nombres.Trim();
        entity.Cargo = input.Cargo.Trim();
        entity.Empresa = input.Empresa.Trim();

        // Firma: null => no tocar; "" / whitespace => limpiar; valor => trim y aplicar.
        if (input.Firma is not null)
        {
            entity.Firma = ResponsableValidator.TrimToNull(input.Firma);
        }

        // Activo: null => no tocar. Permite reactivar desde la UI admin.
        if (input.Activo.HasValue)
        {
            entity.Activo = input.Activo.Value;
        }

        entity.FechaActualizacion = DateTime.UtcNow;

        await _repo.UpdateAsync(entity, ct);

        return ResponsableMapper.ToDetail(entity);
    }
}
