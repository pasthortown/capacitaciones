using Capacitaciones.Application.UseCases.Configuracion;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

public class ConfiguracionNotificacionConfiguration : IEntityTypeConfiguration<ConfiguracionNotificacion>
{
    public void Configure(EntityTypeBuilder<ConfiguracionNotificacion> builder)
    {
        builder.ToTable("ConfiguracionNotificacion", "dbo");
        builder.HasKey(n => n.Plantilla);

        builder.Property(n => n.Plantilla).HasMaxLength(100);
        builder.Property(n => n.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Activo).IsRequired();
        builder.Property(n => n.AsuntoPersonalizado).HasMaxLength(500);
        builder.Property(n => n.ActualizadoPor).HasMaxLength(255);
        builder.Property(n => n.ActualizadoEn);

        // Seed: un aviso por plantilla del catálogo, todos activos y con el asunto original.
        builder.HasData(NotificacionesCatalogo.Todas.Select(d => new ConfiguracionNotificacion
        {
            Plantilla = d.Plantilla,
            Nombre = d.Nombre,
            Activo = true,
            AsuntoPersonalizado = null,
            ActualizadoPor = null,
            ActualizadoEn = null
        }));
    }
}
