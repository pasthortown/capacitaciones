using Capacitaciones.Application.Dtos.Responsables;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Responsables;

/// <summary>Obtiene un responsable por id (admin). Incluye inactivos.</summary>
public class ObtenerResponsableUseCase
{
    private readonly IResponsableRepository _repo;

    public ObtenerResponsableUseCase(IResponsableRepository repo)
    {
        _repo = repo;
    }

    public async Task<ResponsableDetailDto> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new ResponsableNotFoundException(id);
        return ResponsableMapper.ToDetail(entity);
    }
}
