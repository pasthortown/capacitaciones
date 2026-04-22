using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.PreguntasEncuesta;

public class ListarPreguntasEncuestaUseCase
{
    private readonly IPreguntaEncuestaRepository _repo;

    public ListarPreguntasEncuestaUseCase(IPreguntaEncuestaRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<PreguntaEncuestaDto>> ExecuteAsync(
        Guid? tipoActividadId,
        bool includeInactive,
        CancellationToken ct)
    {
        var items = await _repo.ListAsync(tipoActividadId, includeInactive, ct);
        return items.Select(PreguntaEncuestaMapper.ToDto).ToArray();
    }
}
