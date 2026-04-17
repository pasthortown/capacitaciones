using Capacitaciones.Application.Dtos.Responsables;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Responsables;

namespace Capacitaciones.Application.UseCases.Responsable;

/// <summary>
/// Caso de uso público: el responsable autenticado vía link firmado (claim <c>rid</c>) consulta
/// su propio perfil.
///
/// Reglas:
///   - Responsable inexistente → <see cref="ResponsableNotFoundException"/> (404 en el controller).
///   - Responsable inactivo → <see cref="ResponsableForbiddenException"/> (403).
/// </summary>
public class ObtenerPerfilResponsableUseCase
{
    private readonly IResponsableRepository _repo;

    public ObtenerPerfilResponsableUseCase(IResponsableRepository repo)
    {
        _repo = repo;
    }

    public async Task<ResponsablePerfilDto> ExecuteAsync(Guid responsableId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(responsableId, ct)
            ?? throw new ResponsableNotFoundException(responsableId);

        if (!entity.Activo)
        {
            throw new ResponsableForbiddenException("El responsable está inactivo.");
        }

        return ResponsableMapper.ToPerfil(entity);
    }
}

/// <summary>
/// Se lanza cuando el token del responsable es válido (policy pasa) pero el responsable no
/// puede ser operado: inactivo. El controlador la traduce a 403.
/// </summary>
public class ResponsableForbiddenException : Exception
{
    public ResponsableForbiddenException(string message) : base(message) { }
}
