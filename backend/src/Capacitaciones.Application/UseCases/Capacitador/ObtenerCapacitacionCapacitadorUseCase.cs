using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.Dtos.Capacitador;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Capacitador;

/// <summary>
/// Vista read-only de la capacitación del capacitador (desde el token).
///
/// Validaciones (ver spec Fase 4):
///   - Si no existe → <see cref="CapacitacionNotFoundException"/> (traducido a 404 en el controller).
///   - Si existe pero está inactiva → <see cref="CapacitadorForbiddenException"/> (403).
///   - El capacitador SÍ puede consultar aún cuando la capacitación esté Finalizada.
/// </summary>
public class ObtenerCapacitacionCapacitadorUseCase
{
    private readonly ICapacitacionRepository _repo;

    public ObtenerCapacitacionCapacitadorUseCase(ICapacitacionRepository repo)
    {
        _repo = repo;
    }

    public async Task<CapacitadorCapacitacionDto> ExecuteAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdWithResponsablesAsync(capacitacionId, ct);
        if (entity is null)
        {
            throw new CapacitacionNotFoundException(capacitacionId);
        }

        if (!entity.Activo)
        {
            throw new CapacitadorForbiddenException("La capacitación está inactiva.");
        }

        return MapTo(entity);
    }

    internal static CapacitadorCapacitacionDto MapTo(Capacitacion c) => new()
    {
        Id = c.Id,
        Codigo = c.Codigo,
        Tema = c.Tema,
        Capacitador = c.Capacitador,
        FechaHoraInicio = c.FechaHoraInicio,
        DuracionMinutos = c.DuracionMinutos,
        Modalidad = new CatalogoRefDto { Id = c.ModalidadId, Nombre = c.Modalidad?.Nombre ?? string.Empty },
        TipoActividad = new CatalogoRefDto { Id = c.TipoActividadId, Nombre = c.TipoActividad?.Nombre ?? string.Empty },
        TipoCertificacion = c.TipoCertificacion.ToString(),
        Estado = CapacitacionEstadoCalculator.Calcular(c),
        Descripcion = c.Descripcion,
        FirmaCapacitador = c.FirmaCapacitador,
        CargoCapacitador = c.CargoCapacitador,
        EmpresaCapacitador = c.EmpresaCapacitador
    };
}

/// <summary>
/// Se lanza cuando el token del capacitador es válido (policy pasa) pero la capacitación no
/// puede ser operada: inactiva, finalizada (para PUT), etc. El controlador la traduce a 403.
/// </summary>
public class CapacitadorForbiddenException : Exception
{
    public CapacitadorForbiddenException(string message) : base(message) { }
}
