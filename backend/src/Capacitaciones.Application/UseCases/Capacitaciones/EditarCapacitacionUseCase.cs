using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>
/// Caso de uso: editar capacitación. El <c>Codigo</c> es inmutable. La lista de
/// responsables se reemplaza por completo (estrategia replace-all) dentro de una
/// misma transacción.
/// </summary>
public class EditarCapacitacionUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly IModalidadRepository _modalidades;
    private readonly ITipoActividadRepository _tiposActividad;

    public EditarCapacitacionUseCase(
        ICapacitacionRepository repo,
        IModalidadRepository modalidades,
        ITipoActividadRepository tiposActividad)
    {
        _repo = repo;
        _modalidades = modalidades;
        _tiposActividad = tiposActividad;
    }

    public async Task<CapacitacionDetailDto> ExecuteAsync(Guid id, UpdateCapacitacionDto input, CancellationToken ct = default)
    {
        if (input is null)
            throw new CapacitacionServiceException("INVALID_INPUT", "Payload requerido.");

        var entity = await _repo.GetByIdWithResponsablesAsync(id, ct)
            ?? throw new CapacitacionNotFoundException(id);

        CapacitacionValidator.ValidarTema(input.Tema);
        CapacitacionValidator.ValidarCapacitador(input.Capacitador);
        CapacitacionValidator.ValidarDuracion(input.DuracionMinutos);

        if (input.FechaHoraInicio == default)
            throw new CapacitacionServiceException("INVALID_FECHA", "'fechaHoraInicio' es requerido.");

        var tipoCert = CapacitacionValidator.ParsearTipoCertificacion(input.TipoCertificacion);

        await CapacitacionValidator.ValidarCatalogosAsync(
            input.ModalidadId, input.TipoActividadId, _modalidades, _tiposActividad, ct);

        CapacitacionValidator.ValidarResponsables(input.Responsables);

        entity.Tema = input.Tema.Trim();
        entity.Capacitador = input.Capacitador.Trim();
        entity.CargoCapacitador = string.IsNullOrWhiteSpace(input.CargoCapacitador) ? null : input.CargoCapacitador.Trim();
        entity.EmpresaCapacitador = string.IsNullOrWhiteSpace(input.EmpresaCapacitador) ? null : input.EmpresaCapacitador.Trim();
        entity.FirmaCapacitador = string.IsNullOrWhiteSpace(input.FirmaCapacitador) ? entity.FirmaCapacitador : input.FirmaCapacitador;
        entity.Descripcion = string.IsNullOrWhiteSpace(input.Descripcion) ? null : input.Descripcion;
        entity.ModalidadId = input.ModalidadId;
        entity.TipoActividadId = input.TipoActividadId;
        entity.TipoCertificacion = tipoCert;
        entity.FechaHoraInicio = input.FechaHoraInicio;
        entity.DuracionMinutos = input.DuracionMinutos;
        entity.FechaActualizacion = DateTime.UtcNow;

        var nuevos = (input.Responsables ?? new List<CreateResponsableDto>())
            .Select(r => new Responsable
            {
                Id = Guid.NewGuid(),
                CapacitacionId = entity.Id,
                Nombres = r.Nombres.Trim(),
                Cargo = r.Cargo.Trim(),
                Empresa = r.Empresa.Trim(),
                Firma = r.Firma,
                Orden = r.Orden
            })
            .ToList();

        await _repo.UpdateWithResponsablesAsync(entity, nuevos, ct);

        var recargada = await _repo.GetByIdWithResponsablesAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("No se pudo recuperar la capacitación tras actualizar.");

        return CapacitacionMapper.ToDetailDto(recargada);
    }
}
