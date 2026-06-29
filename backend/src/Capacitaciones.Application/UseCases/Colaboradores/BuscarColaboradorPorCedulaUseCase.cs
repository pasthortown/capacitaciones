using Capacitaciones.Application.Dtos.Colaboradores;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Colaboradores;

/// <summary>
/// Resuelve un colaborador por cédula buscando primero entre los externos locales y luego en DOS
/// (ControlTareas). Devuelve null si no existe en ninguno. Lo usa el modal de convenios para
/// mostrar el nombre del colaborador al ingresar la cédula.
/// </summary>
public class BuscarColaboradorPorCedulaUseCase
{
    private readonly IColaboradorRepository _externos;
    private readonly IControlTareasColaboradoresClient _controlTareas;

    public BuscarColaboradorPorCedulaUseCase(IColaboradorRepository externos, IControlTareasColaboradoresClient controlTareas)
    {
        _externos = externos;
        _controlTareas = controlTareas;
    }

    public async Task<ColaboradorLookupDto?> ExecuteAsync(string cedula, CancellationToken ct = default)
    {
        var c = (cedula ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(c)) return null;

        var ext = await _externos.GetByCedulaAsync(c, ct);
        if (ext is not null)
            return new ColaboradorLookupDto { Cedula = ext.Cedula, Name = ext.Name, Origen = "Externo" };

        var dos = await _controlTareas.ObtenerPorCedulaAsync(c, ct);
        if (dos is not null)
            return new ColaboradorLookupDto { Cedula = c, Name = dos.Name, Origen = "DOS" };

        return null;
    }
}
