using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

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

        // Firma: base64, sin límite (nvarchar(max)).
        builder.Property(r => r.Firma)
            .IsRequired();

        builder.Property(r => r.Orden)
            .IsRequired();

        // FK a Capacitacion: configurada desde CapacitacionConfiguration con cascade delete.
        // Índice único (CapacitacionId, Orden) para garantizar orden único por capacitación.
        builder.HasIndex(r => new { r.CapacitacionId, r.Orden })
            .IsUnique()
            .HasDatabaseName("UX_Responsable_Capacitacion_Orden");
    }
}
