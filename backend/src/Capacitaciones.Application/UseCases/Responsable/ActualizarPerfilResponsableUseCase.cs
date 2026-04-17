using Capacitaciones.Application.Dtos.Responsables;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Responsables;

namespace Capacitaciones.Application.UseCases.Responsable;

/// <summary>
/// Caso de uso público: el responsable autenticado vía link firmado actualiza su perfil.
/// La firma es OBLIGATORIA en este flujo — el propósito del link es que el responsable
/// cargue/actualice su firma. Campos se trimean; firma se trimea pero no se normaliza más
/// (es base64/dataURL).
///
/// Reglas:
///   - Responsable inexistente → <see cref="ResponsableNotFoundException"/> (404).
///   - Responsable inactivo → <see cref="ResponsableForbiddenException"/> (403).
///   - Firma vacía/null/whitespace → <see cref="ResponsableServiceException"/> con código
///     <c>INVALID_FIRMA</c> (400).
/// </summary>
public class ActualizarPerfilResponsableUseCase
{
    private readonly IResponsableRepository _repo;

    public ActualizarPerfilResponsableUseCase(IResponsableRepository repo)
    {
        _repo = repo;
    }

    public async Task<ResponsablePerfilDto> ExecuteAsync(
        Guid responsableId,
        UpdateResponsablePerfilDto input,
        CancellationToken ct = default)
    {
        if (input is null)
            throw new ResponsableServiceException("INVALID_INPUT", "Payload requerido.");

        var entity = await _repo.GetByIdAsync(responsableId, ct)
            ?? throw new ResponsableNotFoundException(responsableId);

        if (!entity.Activo)
        {
            throw new ResponsableForbiddenException("El responsable está inactivo.");
        }

        ResponsableValidator.ValidarNombres(input.Nombres);
        ResponsableValidator.ValidarCargo(input.Cargo);
        ResponsableValidator.ValidarEmpresa(input.Empresa);

        // Firma obligatoria acá: whitespace-only NO es válido.
        if (string.IsNullOrWhiteSpace(input.Firma))
            throw new ResponsableServiceException("INVALID_FIRMA", "'firma' es requerida.");

        entity.Nombres = input.Nombres.Trim();
        entity.Cargo = input.Cargo.Trim();
        entity.Empresa = input.Empresa.Trim();
        entity.Firma = input.Firma.Trim();
        entity.FechaActualizacion = DateTime.UtcNow;

        await _repo.UpdateAsync(entity, ct);

        return ResponsableMapper.ToPerfil(entity);
    }
}
