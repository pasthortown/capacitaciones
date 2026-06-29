using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

/// <summary>Configuración EF Core de Convenios, sus ítems de costo y sus anexos (1:N, cascade).</summary>
public class ConvenioConfiguration : IEntityTypeConfiguration<Convenio>
{
    public void Configure(EntityTypeBuilder<Convenio> b)
    {
        b.ToTable("Convenio", "dbo");
        b.HasKey(c => c.Id);

        b.Property(c => c.CedulaColaborador).HasMaxLength(20).IsRequired();
        b.Property(c => c.NombreColaborador).HasMaxLength(200).IsRequired();
        b.Property(c => c.OrigenColaborador).HasMaxLength(20);
        b.Property(c => c.CargoColaborador).HasMaxLength(150);
        b.Property(c => c.AreaColaborador).HasMaxLength(150);
        b.Property(c => c.SolicitadoPor).HasMaxLength(200);
        b.Property(c => c.AutorizadoPor).HasMaxLength(200);
        b.Property(c => c.Titulo).HasMaxLength(250).IsRequired();
        b.Property(c => c.Descripcion).HasMaxLength(2000);
        b.Property(c => c.Tipo).HasMaxLength(150);
        b.Property(c => c.TipoCurso).HasMaxLength(150);
        b.Property(c => c.NombreCurso).HasMaxLength(250);
        b.Property(c => c.Marca).HasMaxLength(150);
        b.Property(c => c.Fecha).IsRequired();
        b.Property(c => c.MesesADevengar).IsRequired();
        b.Property(c => c.Estado).HasConversion<int>().IsRequired();
        b.Property(c => c.FechaCorte);
        b.Property(c => c.MontoCongelado).HasColumnType("decimal(18,2)");

        b.Property(c => c.Activo).IsRequired().HasDefaultValue(true);
        b.Property(c => c.FechaCreacion).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(c => c.FechaActualizacion);

        b.HasIndex(c => c.CedulaColaborador).HasDatabaseName("IX_Convenio_Cedula");

        b.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.ConvenioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(c => c.Anexos)
            .WithOne()
            .HasForeignKey(a => a.ConvenioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Configuración EF Core de los ítems de costo de un convenio.</summary>
public class ConvenioItemConfiguration : IEntityTypeConfiguration<ConvenioItem>
{
    public void Configure(EntityTypeBuilder<ConvenioItem> b)
    {
        b.ToTable("ConvenioItem", "dbo");
        b.HasKey(i => i.Id);
        b.Property(i => i.Tipo).HasMaxLength(150).IsRequired();
        b.Property(i => i.Valor).HasColumnType("decimal(18,2)");
        b.Property(i => i.Devengable).IsRequired();
        b.Property(i => i.Observacion).HasMaxLength(2000);
        b.HasIndex(i => i.ConvenioId);
    }
}

/// <summary>Configuración EF Core de los anexos de un convenio.</summary>
public class ConvenioAnexoConfiguration : IEntityTypeConfiguration<ConvenioAnexo>
{
    public void Configure(EntityTypeBuilder<ConvenioAnexo> b)
    {
        b.ToTable("ConvenioAnexo", "dbo");
        b.HasKey(a => a.Id);
        b.Property(a => a.NombreOriginal).HasMaxLength(500).IsRequired();
        b.Property(a => a.NombreAlmacenado).HasMaxLength(100).IsRequired();
        b.Property(a => a.ContentType).HasMaxLength(200);
        b.Property(a => a.FechaCreacion).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasIndex(a => a.ConvenioId);
    }
}
