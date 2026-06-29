using Capacitaciones.Application.Dtos.Colaboradores;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Colaboradores;

/// <summary>Lista los colaboradores internos de DOS desde el API de ControlTareas (solo lectura).</summary>
public class ListarColaboradoresDosUseCase
{
    private readonly IControlTareasColaboradoresClient _client;

    public ListarColaboradoresDosUseCase(IControlTareasColaboradoresClient client)
    {
        _client = client;
    }

    public Task<IReadOnlyList<EmpleadoDosDto>> ExecuteAsync(string? buscar, bool incluirInactivos, CancellationToken ct = default)
        => _client.ListarAsync(buscar, incluirInactivos, ct);

    /// <summary>¿Está disponible la integración con ControlTareas? (para que la UI avise si no).</summary>
    public bool IntegracionDisponible => _client.Enabled;
}

/// <summary>Lista los colaboradores externos administrados localmente.</summary>
public class ListarColaboradoresExternosUseCase
{
    private readonly IColaboradorRepository _repo;

    public ListarColaboradoresExternosUseCase(IColaboradorRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<ColaboradorDto>> ExecuteAsync(string? buscar, bool incluirInactivos, CancellationToken ct = default)
    {
        var items = await _repo.ListAsync(buscar, incluirInactivos, ct);
        return items.Select(ColaboradorMapper.ToDto).ToList();
    }
}

/// <summary>Detalle de un colaborador externo por Id.</summary>
public class ObtenerColaboradorExternoUseCase
{
    private readonly IColaboradorRepository _repo;

    public ObtenerColaboradorExternoUseCase(IColaboradorRepository repo)
    {
        _repo = repo;
    }

    public async Task<ColaboradorDto> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new ColaboradorNotFoundException(id);
        return ColaboradorMapper.ToDto(entity);
    }
}
