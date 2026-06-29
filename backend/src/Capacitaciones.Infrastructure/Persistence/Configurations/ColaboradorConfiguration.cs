using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración EF Core de los colaboradores externos a DOS. Se aplica vía
/// <c>ApplyConfigurationsFromAssembly</c>. Cédula única (clave natural).
/// </summary>
public class ColaboradorConfiguration : IEntityTypeConfiguration<Colaborador>
{
    public void Configure(EntityTypeBuilder<Colaborador> builder)
    {
        builder.ToTable("Colaborador", "dbo");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Cedula).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Society).HasMaxLength(150);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.WorkArea).HasMaxLength(100);
        builder.Property(c => c.Address).HasMaxLength(300);
        builder.Property(c => c.Phone).HasMaxLength(50);
        builder.Property(c => c.Sex).HasMaxLength(30);
        builder.Property(c => c.Province).HasMaxLength(100);
        builder.Property(c => c.MaritalStatus).HasMaxLength(50);
        builder.Property(c => c.JobPosition).HasMaxLength(150);
        builder.Property(c => c.Email).HasMaxLength(200);

        builder.Property(c => c.Activo).IsRequired().HasDefaultValue(true);
        builder.Property(c => c.FechaCreacion).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(c => c.FechaActualizacion);

        builder.HasIndex(c => c.Cedula)
            .IsUnique()
            .HasDatabaseName("UX_Colaborador_Cedula");
    }
}
