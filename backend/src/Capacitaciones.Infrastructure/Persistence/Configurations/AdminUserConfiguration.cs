using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

public class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.ToTable("AdminUser", "dbo");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.UsuarioRed)
            .HasMaxLength(100)
            .IsRequired();

        // Con login por dominio, estos ya no son obligatorios (se completan del AD al ingresar).
        builder.Property(u => u.Email)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(u => u.Nombres)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(u => u.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.FechaCreacion)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(u => u.FechaActualizacion);
        builder.Property(u => u.UltimoLogin);

        // Unicidad del usuario de red (case-insensitive por la collation CI de SQL Server).
        builder.HasIndex(u => u.UsuarioRed)
            .IsUnique()
            .HasDatabaseName("UX_AdminUser_UsuarioRed");
    }
}
