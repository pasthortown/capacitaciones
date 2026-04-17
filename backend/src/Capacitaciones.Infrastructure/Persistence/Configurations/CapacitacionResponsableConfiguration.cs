using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración EF Core de la pivote N–N entre <see cref="Capacitacion"/> y
/// <see cref="Responsable"/>. PK compuesta (CapacitacionId, ResponsableId); índice único
/// (CapacitacionId, Orden) para garantizar orden único por capacitación.
/// Cascade delete desde el lado de Capacitacion (al borrar físicamente una capacitación se
/// limpian sus entradas pivote); Restrict desde el lado de Responsable (no se puede borrar
/// físicamente un responsable referenciado — y en la práctica el admin usa baja lógica).
/// </summary>
public class CapacitacionResponsableConfiguration : IEntityTypeConfiguration<CapacitacionResponsable>
{
    public void Configure(EntityTypeBuilder<CapacitacionResponsable> builder)
    {
        builder.ToTable("CapacitacionResponsable", "dbo");

        builder.HasKey(cr => new { cr.CapacitacionId, cr.ResponsableId });

        builder.Property(cr => cr.Orden)
            .IsRequired();

        builder.HasIndex(cr => new { cr.CapacitacionId, cr.Orden })
            .IsUnique()
            .HasDatabaseName("UX_CapacitacionResponsable_Capacitacion_Orden");

        builder.HasOne(cr => cr.Capacitacion)
            .WithMany(c => c.CapacitacionResponsables)
            .HasForeignKey(cr => cr.CapacitacionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cr => cr.Responsable)
            .WithMany()
            .HasForeignKey(cr => cr.ResponsableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
