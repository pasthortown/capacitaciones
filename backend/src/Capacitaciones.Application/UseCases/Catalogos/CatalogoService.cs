using Capacitaciones.Application.Dtos;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Catalogos;

/// <summary>
/// Servicio genérico que concentra los casos de uso de los 3 catálogos administrables.
/// Los 5 casos de uso (Listar, Obtener, Crear, Editar, EliminarLogico) se exponen como
/// métodos dentro de una misma clase para mantener el código simple y legible.
/// </summary>
public class CatalogoService<T> where T : CatalogoBase, new()
{
    private const int NombreMaxLength = 255;
    private readonly ICatalogoRepository<T> _repo;

    public CatalogoService(ICatalogoRepository<T> repo)
    {
        _repo = repo;
    }

    /// <summary>Listar catálogo (Caso de uso: Listar).</summary>
    public async Task<IReadOnlyList<CatalogoDto>> ListarAsync(bool includeInactive, CancellationToken ct = default)
    {
        var items = await _repo.ListAsync(includeInactive, ct);
        return items.Select(ToDto).ToList();
    }

    /// <summary>Obtener por Id (Caso de uso: Obtener).</summary>
    public async Task<CatalogoDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <summary>Crear (Caso de uso: Crear).</summary>
    public async Task<CatalogoDto> CrearAsync(UpsertCatalogoDto input, CancellationToken ct = default)
    {
        var nombre = NormalizarNombre(input.Nombre);

        var existente = await _repo.GetByNombreAsync(nombre, ct);
        if (existente is not null)
        {
            throw new CatalogoServiceException(
                "DUPLICATE_NAME",
                $"Ya existe un ítem con el nombre '{nombre}'.");
        }

        var entity = new T
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Activo = input.Activo,
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = null
        };
        await _repo.AddAsync(entity, ct);
        return ToDto(entity);
    }

    /// <summary>Editar (Caso de uso: Editar).</summary>
    public async Task<CatalogoDto> EditarAsync(Guid id, UpsertCatalogoDto input, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct)
            ?? throw new CatalogoNotFoundException(id);

        var nuevoNombre = NormalizarNombre(input.Nombre);

        if (!string.Equals(entity.Nombre, nuevoNombre, StringComparison.OrdinalIgnoreCase))
        {
            var duplicado = await _repo.GetByNombreAsync(nuevoNombre, ct);
            if (duplicado is not null && duplicado.Id != id)
            {
                throw new CatalogoServiceException(
                    "DUPLICATE_NAME",
                    $"Ya existe un ítem con el nombre '{nuevoNombre}'.");
            }
        }

        entity.Nombre = nuevoNombre;
        entity.Activo = input.Activo;
        entity.FechaActualizacion = DateTime.UtcNow;

        await _repo.UpdateAsync(entity, ct);
        return ToDto(entity);
    }

    /// <summary>Eliminación lógica (Caso de uso: EliminarLogico).</summary>
    public async Task EliminarLogicoAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct)
            ?? throw new CatalogoNotFoundException(id);

        await _repo.DeleteAsync(id, ct);
    }

    /// <summary>Alta masiva de filas (usado tras una importación XLSX válida).</summary>
    public async Task ImportarFilasAsync(IEnumerable<UpsertCatalogoDto> filas, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var entidades = filas.Select(f => new T
        {
            Id = Guid.NewGuid(),
            Nombre = NormalizarNombre(f.Nombre),
            Activo = f.Activo,
            FechaCreacion = now,
            FechaActualizacion = null
        }).ToList();

        if (entidades.Count > 0)
        {
            await _repo.AddRangeAsync(entidades, ct);
        }
    }

    private static string NormalizarNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new CatalogoServiceException("EMPTY_NAME", "El nombre es requerido.");
        }

        var trimmed = nombre.Trim();
        if (trimmed.Length > NombreMaxLength)
        {
            throw new CatalogoServiceException(
                "NAME_TOO_LONG",
                $"El nombre excede el máximo de {NombreMaxLength} caracteres.");
        }
        return trimmed;
    }

    private static CatalogoDto ToDto(T entity) => new()
    {
        Id = entity.Id,
        Nombre = entity.Nombre,
        Activo = entity.Activo,
        FechaCreacion = entity.FechaCreacion,
        FechaActualizacion = entity.FechaActualizacion
    };
}
