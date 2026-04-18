using Capacitaciones.Application.Dtos.Recursos;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Recursos;

/// <summary>Obtiene un recurso por Id (admin). Devuelve recursos inactivos también.</summary>
public class ObtenerRecursoUseCase
{
    private readonly IRecursoRepository _repo;

    public ObtenerRecursoUseCase(IRecursoRepository repo)
    {
        _repo = repo;
    }

    public async Task<RecursoDetailDto> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new RecursoNotFoundException(id);
        return RecursoMapper.ToDetail(entity);
    }
}
