using System.Globalization;
using Capacitaciones.Application.Dtos.Colaboradores;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Colaboradores;

/// <summary>Mapeo entidad ↔ DTO y parseo de fecha (<c>yyyy-MM-dd</c>) del módulo Colaboradores.</summary>
public static class ColaboradorMapper
{
    public static ColaboradorDto ToDto(Colaborador c) => new()
    {
        Id = c.Id,
        Cedula = c.Cedula,
        Name = c.Name,
        Society = c.Society,
        JobPosition = c.JobPosition,
        WorkArea = c.WorkArea,
        City = c.City,
        Address = c.Address,
        Phone = c.Phone,
        Sex = c.Sex,
        BirthDate = c.BirthDate,
        Province = c.Province,
        MaritalStatus = c.MaritalStatus,
        Email = c.Email,
        IsActive = c.Activo,
    };

    /// <summary>Copia los campos del request a la entidad (sin tocar Id/Activo/fechas de auditoría).</summary>
    public static void Apply(Colaborador c, ColaboradorRequest req)
    {
        c.Cedula = (req.Cedula ?? string.Empty).Trim();
        c.Name = (req.Name ?? string.Empty).Trim();
        c.Society = Clean(req.Society);
        c.City = Clean(req.City);
        c.WorkArea = Clean(req.WorkArea);
        c.Address = Clean(req.Address);
        c.Phone = Clean(req.Phone);
        c.Sex = Clean(req.Sex);
        c.BirthDate = ParseOptionalDate(req.BirthDate);
        c.Province = Clean(req.Province);
        c.MaritalStatus = Clean(req.MaritalStatus);
        c.JobPosition = Clean(req.JobPosition);
        c.Email = Clean(req.Email);
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public static DateTime? ParseOptionalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
        throw new ColaboradorValidacionException("La fecha de nacimiento no es válida (formato yyyy-MM-dd).");
    }
}
