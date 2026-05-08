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

        // Email del capacitador (320 cubre RFC 5321: 64 local + @ + 255 dominio).
        builder.Property(c => c.EmailCapacitador)
            .HasMaxLength(320);

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

        // Fase 9 — Puntaje mínimo (solo aplica cuando TipoCertificacion == Aprobacion).
        // decimal(4,2) cubre el rango 0.00 – 99.99; el dominio restringe a 0–10 vía validator.
        builder.Property(c => c.PuntajeMinimo)
            .HasColumnType("decimal(4,2)");

        // Fase 9 — Logo de la capacitación (archivo físico vive en volumen IMAGEN_CAPACITACIONES_DIR).
        builder.Property(c => c.LogoPath)
            .HasMaxLength(500);

        builder.Property(c => c.LogoContentType)
            .HasMaxLength(100);

        // Bandera "emite certificado". Default true a nivel de BD para que las filas
        // existentes (creadas antes de esta migración) hereden el comportamiento previo.
        builder.Property(c => c.EmiteCertificado)
            .IsRequired()
            .HasDefaultValue(true);

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

        // La navegación a responsables ahora es N–N vía la pivote CapacitacionResponsable.
        // Las FKs y el cascade delete se configuran en CapacitacionResponsableConfiguration.

        // Global query filter: las capacitaciones con Activo=false son invisibles para
        // cualquier consulta EF (list, get, include, etc.). El soft-delete deja la fila
        // accesible solo por BDD o por consultas que invoquen IgnoreQueryFilters().
        builder.HasQueryFilter(c => c.Activo);
    }
}
