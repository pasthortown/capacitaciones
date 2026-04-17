using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

public class CapacitacionConfiguration : IEntityTypeConfiguration<Capacitacion>
{
    public void Configure(EntityTypeBuilder<Capacitacion> builder)
    {
        builder.ToTable("Capacitacion", "dbo");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Codigo)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(c => c.Codigo)
            .IsUnique()
            .HasDatabaseName("UX_Capacitacion_Codigo");

        builder.Property(c => c.Tema)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.Capacitador)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.CargoCapacitador)
            .HasMaxLength(255);

        builder.Property(c => c.EmpresaCapacitador)
            .HasMaxLength(255);

        // Firma del capacitador y Descripción no tienen límite de tamaño (pueden ser base64 grande o texto libre).
        builder.Property(c => c.FirmaCapacitador);
        builder.Property(c => c.Descripcion);

        // Enum se almacena como int (mapeo por defecto de EF Core).
        builder.Property(c => c.TipoCertificacion)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.FechaHoraInicio)
            .IsRequired();

        builder.Property(c => c.DuracionMinutos)
            .IsRequired();

        builder.Property(c => c.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.FechaCreacion)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(c => c.FechaActualizacion);

        // FKs — restrict delete para evitar borrados en cascada accidentales del catálogo.
        builder.HasOne(c => c.Modalidad)
            .WithMany()
            .HasForeignKey(c => c.ModalidadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.TipoActividad)
            .WithMany()
            .HasForeignKey(c => c.TipoActividadId)
            .OnDelete(DeleteBehavior.Restrict);

        // Sub-colección: cascade delete — al borrar físicamente una capacitación se eliminan sus responsables.
        builder.HasMany(c => c.Responsables)
            .WithOne(r => r.Capacitacion)
            .HasForeignKey(r => r.CapacitacionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
