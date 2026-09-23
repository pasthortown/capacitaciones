using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

public class ConfiguracionCorreoConfiguration : IEntityTypeConfiguration<ConfiguracionCorreo>
{
    public void Configure(EntityTypeBuilder<ConfiguracionCorreo> builder)
    {
        builder.ToTable("ConfiguracionCorreo", "dbo");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.SmtpHost).HasMaxLength(255).IsRequired();
        builder.Property(c => c.SmtpPort).IsRequired();
        builder.Property(c => c.SmtpUser).HasMaxLength(255);
        builder.Property(c => c.SmtpPasswordCifrada).HasMaxLength(1024);
        builder.Property(c => c.UsarTls).IsRequired();
        builder.Property(c => c.RemitenteCorreo).HasMaxLength(255).IsRequired();
        builder.Property(c => c.RemitenteNombre).HasMaxLength(255);
        builder.Property(c => c.CcGlobal).HasMaxLength(1000);
        builder.Property(c => c.BccGlobal).HasMaxLength(1000);
        builder.Property(c => c.ActualizadoPor).HasMaxLength(255).IsRequired();
        builder.Property(c => c.ActualizadoEn).IsRequired();
    }
}
