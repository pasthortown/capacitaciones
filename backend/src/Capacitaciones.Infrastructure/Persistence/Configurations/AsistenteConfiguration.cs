using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de <see cref="Asistente"/>. El índice único (CapacitacionId, Identificacion)
/// previene inscripciones duplicadas a nivel BD aún si la validación aplicativa fallara
/// por una condición de carrera.
/// </summary>
public class AsistenteConfiguration : IEntityTypeConfiguration<Asistente>
{
    public void Configure(EntityTypeBuilder<Asistente> builder)
    {
        builder.ToTable("Asistente", "dbo");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Nombres)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(a => a.Apellidos)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(a => a.Identificacion)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.EmailUsuario)
            .HasMaxLength(255)
            .IsRequired();

        // Firma: base64, sin límite (nvarchar(max)).
        builder.Property(a => a.Firma)
            .IsRequired();

        builder.Property(a => a.FechaInscripcion)
            .IsRequired();

        // FK a Capacitacion: cascade delete (al borrar físicamente una capacitación se
        // eliminan sus asistentes; el delete lógico por defecto no propaga nada).
        builder.HasOne(a => a.Capacitacion)
            .WithMany()
            .HasForeignKey(a => a.CapacitacionId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK a Area: Restrict — no se puede borrar un área referenciada por inscripciones.
        builder.HasOne(a => a.Area)
            .WithMany()
            .HasForeignKey(a => a.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice único compuesto: no se puede inscribir dos veces la misma identificación
        // a la misma capacitación. Case-sensitive según la collation por defecto (CI en SQL Server
        // hace que "1234" == "1234" trivialmente; normalizamos Trim pero NO case en identificacion).
        builder.HasIndex(a => new { a.CapacitacionId, a.Identificacion })
            .IsUnique()
            .HasDatabaseName("UX_Asistente_Capacitacion_Identificacion");

        // Índice auxiliar para consultas por capacitación ordenadas por fecha.
        builder.HasIndex(a => new { a.CapacitacionId, a.FechaInscripcion })
            .HasDatabaseName("IX_Asistente_Capacitacion_Fecha");
    }
}
