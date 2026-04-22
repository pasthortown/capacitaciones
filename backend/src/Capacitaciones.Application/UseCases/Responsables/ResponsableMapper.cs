using Capacitaciones.Application.Dtos.Responsables;

namespace Capacitaciones.Application.UseCases.Responsables;

/// <summary>Mapeo manual de <see cref="Domain.Entities.Responsable"/> a sus DTOs de lectura.</summary>
internal static class ResponsableMapper
{
    public static ResponsableSummaryDto ToSummary(Domain.Entities.Responsable r) => new()
    {
        Id = r.Id,
        Nombres = r.Nombres,
        Cargo = r.Cargo,
        Empresa = r.Empresa,
        Email = r.Email,
        TieneFirma = !string.IsNullOrWhiteSpace(r.Firma),
        Activo = r.Activo,
        FechaCreacion = r.FechaCreacion,
        FechaActualizacion = r.FechaActualizacion
    };

    public static ResponsableDetailDto ToDetail(Domain.Entities.Responsable r) => new()
    {
        Id = r.Id,
        Nombres = r.Nombres,
        Cargo = r.Cargo,
        Empresa = r.Empresa,
        Email = r.Email,
        TieneFirma = !string.IsNullOrWhiteSpace(r.Firma),
        Activo = r.Activo,
        FechaCreacion = r.FechaCreacion,
        FechaActualizacion = r.FechaActualizacion,
        Firma = r.Firma
    };

    public static ResponsablePerfilDto ToPerfil(Domain.Entities.Responsable r) => new()
    {
        Id = r.Id,
        Nombres = r.Nombres,
        Cargo = r.Cargo,
        Empresa = r.Empresa,
        Firma = r.Firma
    };
}
