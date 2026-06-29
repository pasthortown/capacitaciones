using Capacitaciones.Application.Dtos.Convenios;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Convenios;

/// <summary>Listado general de convenios (pestaña Convenios).</summary>
public class ListarConveniosUseCase
{
    private readonly IConvenioRepository _repo;
    public ListarConveniosUseCase(IConvenioRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ConvenioDto>> ExecuteAsync(string? buscar, bool incluirInactivos, CancellationToken ct = default)
    {
        var items = await _repo.ListAsync(buscar, incluirInactivos, ct);
        return items.Select(ConvenioMapper.ToDto).ToList();
    }
}

/// <summary>Detalle de un convenio por Id.</summary>
public class ObtenerConvenioUseCase
{
    private readonly IConvenioRepository _repo;
    public ObtenerConvenioUseCase(IConvenioRepository repo) => _repo = repo;

    public async Task<ConvenioDto> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _repo.GetByIdAsync(id, ct) ?? throw new ConvenioNotFoundException(id);
        return ConvenioMapper.ToDto(c);
    }
}

/// <summary>
/// Historial por colaborador: convenios <b>vigentes</b> (activos y con saldo por devengar; los
/// "no aplica" también se incluyen, con saldo 0) de la cédula indicada.
/// </summary>
public class ListarConveniosPorColaboradorUseCase
{
    private readonly IConvenioRepository _repo;
    public ListarConveniosPorColaboradorUseCase(IConvenioRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ConvenioDto>> ExecuteAsync(string cedula, bool soloVigentes, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cedula))
            throw new ConvenioValidacionException("Debe indicar la cédula del colaborador.");
        var items = await _repo.ListByCedulaAsync(cedula.Trim(), includeInactive: false, ct);
        var dtos = items.Select(ConvenioMapper.ToDto);
        if (soloVigentes) dtos = dtos.Where(d => d.TieneSaldoPendiente);
        return dtos.ToList();
    }
}
