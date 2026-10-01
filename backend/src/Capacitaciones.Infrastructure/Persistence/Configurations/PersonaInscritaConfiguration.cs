using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de <see cref="PersonaInscrita"/>. Identificación única: es la clave natural
/// del autocompletado por cédula de la página pública de inscripción.
/// </summary>
public class PersonaInscritaConfiguration : IEntityTypeConfiguration<PersonaInscrita>
{
    public void Configure(EntityTypeBuilder<PersonaInscrita> builder)
    {
        builder.ToTable("PersonaInscrita", "dbo");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Identificacion).HasMaxLength(30).IsRequired();
        builder.Property(p => p.Nombres).HasMaxLength(120).IsRequired();
        builder.Property(p => p.Apellidos).HasMaxLength(120).IsRequired();
        builder.Property(p => p.EmailUsuario).HasMaxLength(255).IsRequired();

        // Firma: base64, sin límite (nvarchar(max)). Null = sin firma guardada.
        builder.Property(p => p.Firma);

        builder.Property(p => p.FechaCreacion).IsRequired();
        builder.Property(p => p.FechaActualizacion).IsRequired();

        // FK a Area: Restrict, igual que Asistente — no se borra un área referenciada.
        builder.HasOne(p => p.Area)
            .WithMany()
            .HasForeignKey(p => p.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Identificacion)
            .IsUnique()
            .HasDatabaseName("UX_PersonaInscrita_Identificacion");
    }
}
