using Capacitaciones.Application.Dtos.Recursos;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Recursos;

/// <summary>Mapeo manual de <see cref="Recurso"/> a sus DTOs de lectura.</summary>
internal static class RecursoMapper
{
    public static RecursoListDto ToList(Recurso r) => new()
    {
        Id = r.Id,
        NombreOriginal = r.NombreOriginal,
        Extension = r.Extension,
        ContentType = r.ContentType,
        TamanoBytes = r.TamanoBytes,
        Descripcion = r.Descripcion,
        Activo = r.Activo,
        FechaCreacion = r.FechaCreacion,
        FechaActualizacion = r.FechaActualizacion
    };

    public static RecursoDetailDto ToDetail(Recurso r) => new()
    {
        Id = r.Id,
        NombreOriginal = r.NombreOriginal,
        NombreAlmacenado = r.NombreAlmacenado,
        Extension = r.Extension,
        ContentType = r.ContentType,
        TamanoBytes = r.TamanoBytes,
        Descripcion = r.Descripcion,
        Activo = r.Activo,
        FechaCreacion = r.FechaCreacion,
        FechaActualizacion = r.FechaActualizacion
    };
}
