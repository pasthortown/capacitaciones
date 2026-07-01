using Capacitaciones.Application.Dtos.Convenios;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Convenios;

/// <summary>Resuelve un colaborador por cédula: primero entre los externos locales, luego en DOS
/// (ControlTareas). Devuelve nombre + origen, o null si no existe en ninguno.</summary>
public readonly record struct ColaboradorResuelto(string Nombre, string Origen, string? Cargo, string? Area, string? Empresa, string? Genero);

public static class ColaboradorResolver
{
    /// <summary>Prefiere el valor capturado manualmente en el formulario (editable) y, si viene
    /// vacío, usa el de la fuente. Permite complementar datos que la fuente no tiene.</summary>
    public static string? PreferirManual(string? manual, string? fuente)
        => string.IsNullOrWhiteSpace(manual) ? fuente : manual.Trim();

    public static async Task<ColaboradorResuelto?> ResolveAsync(
        IColaboradorRepository externos,
        IControlTareasColaboradoresClient controlTareas,
        string cedula,
        CancellationToken ct)
    {
        var ext = await externos.GetByCedulaAsync(cedula, ct);
        if (ext is not null) return new ColaboradorResuelto(ext.Name, "Externo", ext.JobPosition, ext.WorkArea, ext.Society, ext.Sex);

        var dos = await controlTareas.ObtenerPorCedulaAsync(cedula, ct);
        if (dos is not null) return new ColaboradorResuelto(dos.Name, "DOS", dos.JobPosition, dos.WorkArea, dos.Society, dos.Sex);

        return null;
    }
}

/// <summary>Alta de un convenio. Resuelve y "fija" el colaborador (cédula + nombre snapshot).</summary>
public class CrearConvenioUseCase
{
    private readonly IConvenioRepository _repo;
    private readonly IColaboradorRepository _externos;
    private readonly IControlTareasColaboradoresClient _controlTareas;
    private readonly IConvenioNumeracionService _numeracion;

    public CrearConvenioUseCase(IConvenioRepository repo, IColaboradorRepository externos, IControlTareasColaboradoresClient controlTareas, IConvenioNumeracionService numeracion)
    {
        _repo = repo;
        _externos = externos;
        _controlTareas = controlTareas;
        _numeracion = numeracion;
    }

    public async Task<ConvenioDto> ExecuteAsync(ConvenioRequest req, CancellationToken ct = default)
    {
        var cedula = (req.Cedula ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cedula))
            throw new ConvenioValidacionException("Debe seleccionar el colaborador (cédula).");

        var resuelto = await ColaboradorResolver.ResolveAsync(_externos, _controlTareas, cedula, ct)
            ?? throw new ColaboradorNoEncontradoException(cedula);

        var (numero, _) = await _numeracion.ClaimNextAsync(ct);

        var entity = new Convenio
        {
            Id = Guid.NewGuid(),
            NumeroRegistro = numero,
            CedulaColaborador = cedula,
            NombreColaborador = resuelto.Nombre,
            OrigenColaborador = resuelto.Origen,
            // Cargo/Área/Empresa son editables: el valor del formulario manda; si viene vacío, la fuente.
            CargoColaborador = ColaboradorResolver.PreferirManual(req.CargoColaborador, resuelto.Cargo),
            AreaColaborador = ColaboradorResolver.PreferirManual(req.AreaColaborador, resuelto.Area),
            EmpresaColaborador = ColaboradorResolver.PreferirManual(req.EmpresaColaborador, resuelto.Empresa),
            GeneroColaborador = resuelto.Genero,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
        };
        // La fecha de ingreso ya no se captura: es automáticamente la fecha de creación del
        // convenio en el sistema (ancla del cálculo de devengo).
        entity.FechaIngreso = entity.FechaCreacion;
        ConvenioMapper.Apply(entity, req);
        await _repo.AddAsync(entity, ct);
        return ConvenioMapper.ToDto(entity);
    }
}

/// <summary>Edición de un convenio. El colaborador (cédula) no cambia; se refresca el snapshot
/// del nombre. <c>Activo=true</c> reactiva.</summary>
public class EditarConvenioUseCase
{
    private readonly IConvenioRepository _repo;
    private readonly IColaboradorRepository _externos;
    private readonly IControlTareasColaboradoresClient _controlTareas;

    public EditarConvenioUseCase(IConvenioRepository repo, IColaboradorRepository externos, IControlTareasColaboradoresClient controlTareas)
    {
        _repo = repo;
        _externos = externos;
        _controlTareas = controlTareas;
    }

    public async Task<ConvenioDto> ExecuteAsync(Guid id, ConvenioRequest req, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new ConvenioNotFoundException(id);

        ConvenioMapper.Apply(entity, req);

        // Refresca el snapshot del nombre por si cambió en la fuente (no cambia la cédula).
        var resuelto = await ColaboradorResolver.ResolveAsync(_externos, _controlTareas, entity.CedulaColaborador, ct);
        if (resuelto is not null)
        {
            entity.NombreColaborador = resuelto.Value.Nombre;
            entity.OrigenColaborador = resuelto.Value.Origen;
            entity.GeneroColaborador = resuelto.Value.Genero;
        }
        // Cargo/Área/Empresa son editables: el valor del formulario manda; si viene vacío, se usa
        // el de la fuente (o el snapshot existente si la fuente no está disponible). No se sobrescriben.
        entity.CargoColaborador = ColaboradorResolver.PreferirManual(req.CargoColaborador, resuelto?.Cargo ?? entity.CargoColaborador);
        entity.AreaColaborador = ColaboradorResolver.PreferirManual(req.AreaColaborador, resuelto?.Area ?? entity.AreaColaborador);
        entity.EmpresaColaborador = ColaboradorResolver.PreferirManual(req.EmpresaColaborador, resuelto?.Empresa ?? entity.EmpresaColaborador);

        if (req.Activo == true) entity.Activo = true;
        entity.FechaActualizacion = DateTime.UtcNow;

        await _repo.UpdateAsync(entity, ct);
        return ConvenioMapper.ToDto(entity);
    }
}

/// <summary>Baja lógica de un convenio.</summary>
public class EliminarConvenioUseCase
{
    private readonly IConvenioRepository _repo;
    public EliminarConvenioUseCase(IConvenioRepository repo) => _repo = repo;

    public async Task ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new ConvenioNotFoundException(id);
        await _repo.DeleteAsync(entity.Id, ct);
    }
}
