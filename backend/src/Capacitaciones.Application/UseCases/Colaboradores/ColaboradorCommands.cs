using Capacitaciones.Application.Dtos.Colaboradores;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Colaboradores;

/// <summary>
/// Alta de un colaborador externo. Valida que la cédula no exista ya como externo y, sobre todo,
/// que <b>no pertenezca a un colaborador de DOS</b> (ControlTareas): los de DOS se administran allá.
/// </summary>
public class CrearColaboradorExternoUseCase
{
    private readonly IColaboradorRepository _repo;
    private readonly IControlTareasColaboradoresClient _controlTareas;

    public CrearColaboradorExternoUseCase(IColaboradorRepository repo, IControlTareasColaboradoresClient controlTareas)
    {
        _repo = repo;
        _controlTareas = controlTareas;
    }

    public async Task<ColaboradorDto> ExecuteAsync(ColaboradorRequest req, CancellationToken ct = default)
    {
        var cedula = (req.Cedula ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cedula))
            throw new ColaboradorValidacionException("La cédula es obligatoria.");
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ColaboradorValidacionException("El nombre es obligatorio.");

        if (await _repo.ExistsByCedulaAsync(cedula, ct))
            throw new CedulaDuplicadaException(cedula);

        // Regla de negocio: si la cédula ya está en DOS (ControlTareas), no se permite como externo.
        if (await _controlTareas.ExisteCedulaAsync(cedula, ct))
            throw new CedulaPerteneceADosException(cedula);

        var entity = new Colaborador
        {
            Id = Guid.NewGuid(),
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
        };
        ColaboradorMapper.Apply(entity, req);
        await _repo.AddAsync(entity, ct);
        return ColaboradorMapper.ToDto(entity);
    }
}

/// <summary>Edición de un colaborador externo. La cédula es inmutable (clave natural).
/// Si el request trae <c>Activo=true</c> y el externo estaba de baja, lo reactiva.</summary>
public class EditarColaboradorExternoUseCase
{
    private readonly IColaboradorRepository _repo;

    public EditarColaboradorExternoUseCase(IColaboradorRepository repo)
    {
        _repo = repo;
    }

    public async Task<ColaboradorDto> ExecuteAsync(Guid id, ColaboradorRequest req, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new ColaboradorNotFoundException(id);

        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ColaboradorValidacionException("El nombre es obligatorio.");

        // La cédula no se cambia en edición: se conserva la de la entidad.
        var cedulaOriginal = entity.Cedula;
        ColaboradorMapper.Apply(entity, req);
        entity.Cedula = cedulaOriginal;

        if (req.Activo == true) entity.Activo = true;
        entity.FechaActualizacion = DateTime.UtcNow;

        await _repo.UpdateAsync(entity, ct);
        return ColaboradorMapper.ToDto(entity);
    }
}

/// <summary>Baja lógica de un colaborador externo (<c>Activo=false</c>).</summary>
public class EliminarColaboradorExternoUseCase
{
    private readonly IColaboradorRepository _repo;

    public EliminarColaboradorExternoUseCase(IColaboradorRepository repo)
    {
        _repo = repo;
    }

    public async Task ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new ColaboradorNotFoundException(id);
        await _repo.DeleteAsync(entity.Id, ct);
    }
}
