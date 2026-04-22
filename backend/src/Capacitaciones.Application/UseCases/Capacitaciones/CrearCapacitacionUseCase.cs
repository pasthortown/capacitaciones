using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>
/// Caso de uso: crear capacitación. El código se asigna atómicamente vía
/// <see cref="INumeracionService.ClaimNextCodeAsync"/> y la inserción de la capacitación
/// + entradas pivote a responsables se hace dentro de la misma transacción (manejada por el
/// repositorio con <c>IExecutionStrategy</c> para retry de SQL Server).
/// </summary>
public class CrearCapacitacionUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly IModalidadRepository _modalidades;
    private readonly ITipoActividadRepository _tiposActividad;
    private readonly IResponsableRepository _responsables;
    private readonly INumeracionService _numeracion;

    public CrearCapacitacionUseCase(
        ICapacitacionRepository repo,
        IModalidadRepository modalidades,
        ITipoActividadRepository tiposActividad,
        IResponsableRepository responsables,
        INumeracionService numeracion)
    {
        _repo = repo;
        _modalidades = modalidades;
        _tiposActividad = tiposActividad;
        _responsables = responsables;
        _numeracion = numeracion;
    }

    public async Task<CapacitacionDetailDto> ExecuteAsync(CreateCapacitacionDto input, CancellationToken ct = default)
    {
        if (input is null)
            throw new CapacitacionServiceException("INVALID_INPUT", "Payload requerido.");

        CapacitacionValidator.ValidarTema(input.Tema);
        CapacitacionValidator.ValidarCapacitador(input.Capacitador);
        CapacitacionValidator.ValidarDuracion(input.DuracionMinutos);

        if (input.FechaHoraInicio == default)
            throw new CapacitacionServiceException("INVALID_FECHA", "'fechaHoraInicio' es requerido.");

        var tipoCert = CapacitacionValidator.ParsearTipoCertificacion(input.TipoCertificacion);

        CapacitacionValidator.ValidarPuntajeMinimo(input.PuntajeMinimo, tipoCert);

        await CapacitacionValidator.ValidarCatalogosAsync(
            input.ModalidadId, input.TipoActividadId, _modalidades, _tiposActividad, ct);

        CapacitacionValidator.ValidarResponsableIds(input.ResponsableIds);
        await CapacitacionValidator.ValidarResponsablesActivosAsync(input.ResponsableIds, _responsables, ct);

        var now = DateTime.UtcNow;
        var entity = new Capacitacion
        {
            Id = Guid.NewGuid(),
            // Codigo se asigna dentro de AddAsync (misma transacción que claim del número).
            Codigo = string.Empty,
            Tema = input.Tema.Trim(),
            Capacitador = input.Capacitador.Trim(),
            CargoCapacitador = string.IsNullOrWhiteSpace(input.CargoCapacitador) ? null : input.CargoCapacitador.Trim(),
            EmpresaCapacitador = string.IsNullOrWhiteSpace(input.EmpresaCapacitador) ? null : input.EmpresaCapacitador.Trim(),
            EmailCapacitador = string.IsNullOrWhiteSpace(input.EmailCapacitador) ? null : input.EmailCapacitador.Trim(),
            FirmaCapacitador = null,
            Descripcion = string.IsNullOrWhiteSpace(input.Descripcion) ? null : input.Descripcion,
            ModalidadId = input.ModalidadId,
            TipoActividadId = input.TipoActividadId,
            TipoCertificacion = tipoCert,
            FechaHoraInicio = input.FechaHoraInicio,
            DuracionMinutos = input.DuracionMinutos,
            PuntajeMinimo = tipoCert == TipoCertificacion.Aprobacion ? input.PuntajeMinimo : null,
            // El logo se carga aparte vía POST /api/capacitaciones/{id}/logo.
            LogoPath = null,
            LogoContentType = null,
            Activo = true,
            FechaCreacion = now,
            FechaActualizacion = null,
            CapacitacionResponsables = BuildPivotRelations(input.ResponsableIds)
        };

        // El repositorio orquesta IExecutionStrategy + transacción y llama a nuestro factory
        // (que delega en INumeracionService) dentro de la misma transacción, garantizando que
        // si falla el insert nunca se "queme" un número.
        await _repo.AddAsync(entity, async innerCt => await _numeracion.ClaimNextCodeAsync(innerCt), ct);

        // Volvemos a cargar con navegaciones para poder devolver Modalidad.Nombre / TipoActividad.Nombre
        // + los datos de cada responsable linkeado.
        var recargada = await _repo.GetByIdWithResponsablesAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("No se pudo recuperar la capacitación recién creada.");

        return CapacitacionMapper.ToDetailDto(recargada);
    }

    private static List<CapacitacionResponsable> BuildPivotRelations(IEnumerable<Guid>? ids)
    {
        if (ids is null) return new();
        return ids
            .Select((id, index) => new CapacitacionResponsable
            {
                ResponsableId = id,
                Orden = index
            })
            .ToList();
    }
}
