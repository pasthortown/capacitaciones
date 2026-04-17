using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

public class TipoActividadConfiguration : IEntityTypeConfiguration<TipoActividad>
{
    public void Configure(EntityTypeBuilder<TipoActividad> builder)
    {
        builder.ToTable("TipoActividad", "dbo");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Nombre)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.FechaCreacion)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(t => t.FechaActualizacion);

        builder.HasIndex(t => t.Nombre)
            .IsUnique()
            .HasDatabaseName("UX_TipoActividad_Nombre");

        builder.HasData(CatalogoSeeds.TiposActividad);
    }
}
