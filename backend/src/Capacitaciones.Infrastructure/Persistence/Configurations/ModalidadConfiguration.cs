using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

public class ModalidadConfiguration : IEntityTypeConfiguration<Modalidad>
{
    public void Configure(EntityTypeBuilder<Modalidad> builder)
    {
        builder.ToTable("Modalidad", "dbo");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Nombre)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(m => m.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(m => m.FechaCreacion)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(m => m.FechaActualizacion);

        // Unicidad case-insensitive: SQL Server por defecto usa collation CI.
        builder.HasIndex(m => m.Nombre)
            .IsUnique()
            .HasDatabaseName("UX_Modalidad_Nombre");

        builder.HasData(CatalogoSeeds.Modalidades);
    }
}
