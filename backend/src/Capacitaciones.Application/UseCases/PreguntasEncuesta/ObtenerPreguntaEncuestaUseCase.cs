using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.PreguntasEncuesta;

public class ObtenerPreguntaEncuestaUseCase
{
    private readonly IPreguntaEncuestaRepository _repo;

    public ObtenerPreguntaEncuestaUseCase(IPreguntaEncuestaRepository repo)
    {
        _repo = repo;
    }

    public async Task<PreguntaEncuestaDto?> ExecuteAsync(Guid id, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        return entity is null ? null : PreguntaEncuestaMapper.ToDto(entity);
    }
}
