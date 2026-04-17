using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Area", "dbo");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Nombre)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(a => a.FechaCreacion)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(a => a.FechaActualizacion);

        builder.HasIndex(a => a.Nombre)
            .IsUnique()
            .HasDatabaseName("UX_Area_Nombre");

        // Área no tiene seeds: lo define el usuario.
    }
}
