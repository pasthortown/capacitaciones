using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración EF Core del catálogo global de responsables. La relación N–N con
/// <see cref="Capacitacion"/> vive en <see cref="CapacitacionResponsableConfiguration"/>.
/// </summary>
public class ResponsableConfiguration : IEntityTypeConfiguration<Responsable>
{
    public void Configure(EntityTypeBuilder<Responsable> builder)
    {
        builder.ToTable("Responsable", "dbo");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Nombres)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(r => r.Cargo)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(r => r.Empresa)
            .HasMaxLength(255)
            .IsRequired();

        // Firma: base64/dataURL. Opcional — el responsable la carga desde su link firmado.
        builder.Property(r => r.Firma);

        builder.Property(r => r.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(r => r.FechaCreacion)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(r => r.FechaActualizacion);
    }
}
