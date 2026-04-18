using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración EF Core del módulo Repositorio. Se aplica automáticamente vía
/// <c>ApplyConfigurationsFromAssembly</c> en <c>AppDbContext.OnModelCreating</c>.
/// </summary>
public class RecursoConfiguration : IEntityTypeConfiguration<Recurso>
{
    public void Configure(EntityTypeBuilder<Recurso> builder)
    {
        builder.ToTable("Recurso", "dbo");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.NombreOriginal)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.NombreAlmacenado)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Extension)
            .HasMaxLength(20);

        builder.Property(r => r.ContentType)
            .HasMaxLength(200);

        builder.Property(r => r.TamanoBytes)
            .IsRequired();

        builder.Property(r => r.Descripcion)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(r => r.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(r => r.FechaCreacion)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(r => r.FechaActualizacion);

        // NombreAlmacenado debe ser globalmente único: protege contra colisiones de GUID
        // (imposibles estadísticamente, pero la restricción cuesta nada) y hace explícito
        // el contrato de "un nombre de archivo = una entidad".
        builder.HasIndex(r => r.NombreAlmacenado)
            .IsUnique()
            .HasDatabaseName("UX_Recurso_NombreAlmacenado");

        // Índice filtrado para la consulta más frecuente (listado de recursos activos).
        builder.HasIndex(r => r.Activo)
            .HasDatabaseName("IX_Recurso_Activo")
            .HasFilter("[Activo] = 1");
    }
}
