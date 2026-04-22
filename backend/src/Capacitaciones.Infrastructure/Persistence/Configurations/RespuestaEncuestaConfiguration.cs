using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

public class RespuestaEncuestaConfiguration : IEntityTypeConfiguration<RespuestaEncuesta>
{
    public void Configure(EntityTypeBuilder<RespuestaEncuesta> builder)
    {
        builder.ToTable("RespuestaEncuesta", "dbo");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Valor)
            .IsRequired();

        builder.Property(r => r.FechaRespuesta)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(r => r.Asistente)
            .WithMany()
            .HasForeignKey(r => r.AsistenteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.PreguntaEncuesta)
            .WithMany()
            .HasForeignKey(r => r.PreguntaEncuestaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Una respuesta por (asistente, pregunta). Usado para reforzar "encuesta única".
        builder.HasIndex(r => new { r.AsistenteId, r.PreguntaEncuestaId })
            .IsUnique()
            .HasDatabaseName("UX_RespuestaEncuesta_Asistente_Pregunta");
    }
}
