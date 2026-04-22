using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.PreguntasEncuesta;

public class EliminarPreguntaEncuestaUseCase
{
    private readonly IPreguntaEncuestaRepository _repo;

    public EliminarPreguntaEncuestaUseCase(IPreguntaEncuestaRepository repo)
    {
        _repo = repo;
    }

    public async Task ExecuteAsync(Guid id, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(id, ct)
            ?? throw new PreguntaEncuestaNotFoundException(id);
        await _repo.SoftDeleteAsync(entity, ct);
    }
}
