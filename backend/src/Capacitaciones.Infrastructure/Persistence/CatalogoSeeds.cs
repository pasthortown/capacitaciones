using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Infrastructure.Persistence;

/// <summary>
/// Semillas iniciales para catálogos. Los <see cref="Guid"/> son determinísticos
/// (hard-codeados) para que EF no regenere valores en cada migración.
/// </summary>
public static class CatalogoSeeds
{
    /// <summary>Timestamp determinístico usado como FechaCreacion en todos los seeds.</summary>
    public static readonly DateTime SeedTimestamp =
        new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<Modalidad> Modalidades { get; } = new List<Modalidad>
    {
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111001"),
            Nombre = "Presencial",
            Activo = true,
            FechaCreacion = SeedTimestamp,
            FechaActualizacion = null
        },
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111002"),
            Nombre = "Virtual",
            Activo = true,
            FechaCreacion = SeedTimestamp,
            FechaActualizacion = null
        },
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111003"),
            Nombre = "Híbrida",
            Activo = true,
            FechaCreacion = SeedTimestamp,
            FechaActualizacion = null
        }
    };

    public static IReadOnlyList<TipoActividad> TiposActividad { get; } = new List<TipoActividad>
    {
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222001"),
            Nombre = "Charla",
            Activo = true,
            FechaCreacion = SeedTimestamp,
            FechaActualizacion = null
        },
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222002"),
            Nombre = "Workshop",
            Activo = true,
            FechaCreacion = SeedTimestamp,
            FechaActualizacion = null
        },
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222003"),
            Nombre = "Capacitación",
            Activo = true,
            FechaCreacion = SeedTimestamp,
            FechaActualizacion = null
        },
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222004"),
            Nombre = "Curso",
            Activo = true,
            FechaCreacion = SeedTimestamp,
            FechaActualizacion = null
        },
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222005"),
            Nombre = "Taller",
            Activo = true,
            FechaCreacion = SeedTimestamp,
            FechaActualizacion = null
        },
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222006"),
            Nombre = "Seminario",
            Activo = true,
            FechaCreacion = SeedTimestamp,
            FechaActualizacion = null
        }
    };
}
