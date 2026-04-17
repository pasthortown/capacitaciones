using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

public class ConfiguracionNumeracionConfiguration : IEntityTypeConfiguration<ConfiguracionNumeracion>
{
    public void Configure(EntityTypeBuilder<ConfiguracionNumeracion> builder)
    {
        builder.ToTable("ConfiguracionNumeracion", "dbo");
        builder.HasKey(c => c.Id);

        // Id es un entero fijo (siempre 1). No queremos que SQL Server lo genere.
        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.SiguienteNumero)
            .IsRequired();

        builder.Property(c => c.UltimaActualizacion);

        // Seed: la fila única se crea al aplicar la migración.
        builder.HasData(new ConfiguracionNumeracion
        {
            Id = 1,
            SiguienteNumero = 1,
            UltimaActualizacion = null
        });
    }
}
