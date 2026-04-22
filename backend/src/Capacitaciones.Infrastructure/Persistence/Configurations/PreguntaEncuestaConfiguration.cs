using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

public class PreguntaEncuestaConfiguration : IEntityTypeConfiguration<PreguntaEncuesta>
{
    public void Configure(EntityTypeBuilder<PreguntaEncuesta> builder)
    {
        builder.ToTable("PreguntaEncuesta", "dbo");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Texto)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.TipoPregunta)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TipoPregunta.SeleccionMultiple);

        builder.Property(p => p.OpcionesJson);

        builder.Property(p => p.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.FechaCreacion)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(p => p.FechaActualizacion);

        builder.HasOne(p => p.TipoActividad)
            .WithMany()
            .HasForeignKey(p => p.TipoActividadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.TipoActividadId)
            .HasDatabaseName("IX_PreguntaEncuesta_TipoActividadId");
    }
}
