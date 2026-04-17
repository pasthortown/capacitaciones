using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>
/// Mapeo manual de entidades → DTOs (sin AutoMapper).
/// Centraliza las dos formas de proyección (list vs detail) para evitar divergencia.
/// </summary>
internal static class CapacitacionMapper
{
    public static CapacitacionListDto ToListDto(Capacitacion c, int totalAsistentes = 0) => new()
    {
        Id = c.Id,
        Codigo = c.Codigo,
        Tema = c.Tema,
        Capacitador = c.Capacitador,
        Modalidad = new CatalogoRefDto { Id = c.ModalidadId, Nombre = c.Modalidad?.Nombre ?? string.Empty },
        TipoActividad = new CatalogoRefDto { Id = c.TipoActividadId, Nombre = c.TipoActividad?.Nombre ?? string.Empty },
        TipoCertificacion = c.TipoCertificacion.ToString(),
        FechaHoraInicio = c.FechaHoraInicio,
        DuracionMinutos = c.DuracionMinutos,
        Estado = CapacitacionEstadoCalculator.Calcular(c),
        TotalAsistentes = totalAsistentes,
        Activo = c.Activo
    };

    public static CapacitacionDetailDto ToDetailDto(Capacitacion c, int totalAsistentes = 0) => new()
    {
        Id = c.Id,
        Codigo = c.Codigo,
        Tema = c.Tema,
        Capacitador = c.Capacitador,
        CargoCapacitador = c.CargoCapacitador,
        EmpresaCapacitador = c.EmpresaCapacitador,
        FirmaCapacitador = c.FirmaCapacitador,
        Descripcion = c.Descripcion,
        Modalidad = new CatalogoRefDto { Id = c.ModalidadId, Nombre = c.Modalidad?.Nombre ?? string.Empty },
        TipoActividad = new CatalogoRefDto { Id = c.TipoActividadId, Nombre = c.TipoActividad?.Nombre ?? string.Empty },
        TipoCertificacion = c.TipoCertificacion.ToString(),
        FechaHoraInicio = c.FechaHoraInicio,
        DuracionMinutos = c.DuracionMinutos,
        Estado = CapacitacionEstadoCalculator.Calcular(c),
        TotalAsistentes = totalAsistentes,
        Activo = c.Activo,
        FechaCreacion = c.FechaCreacion,
        FechaActualizacion = c.FechaActualizacion,
        Responsables = c.CapacitacionResponsables
            .OrderBy(cr => cr.Orden)
            .Select(cr => new ResponsableDto
            {
                Id = cr.ResponsableId,
                Nombres = cr.Responsable?.Nombres ?? string.Empty,
                Cargo = cr.Responsable?.Cargo ?? string.Empty,
                Empresa = cr.Responsable?.Empresa ?? string.Empty,
                Firma = cr.Responsable?.Firma,
                Orden = cr.Orden
            })
            .ToList()
    };
}
