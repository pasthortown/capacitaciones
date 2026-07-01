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

        b.Property(c => c.NumeroRegistro);
        b.Property(c => c.CedulaColaborador).HasMaxLength(20).IsRequired();
        b.Property(c => c.NombreColaborador).HasMaxLength(200).IsRequired();
        b.Property(c => c.OrigenColaborador).HasMaxLength(20);
        b.Property(c => c.CargoColaborador).HasMaxLength(150);
        b.Property(c => c.AreaColaborador).HasMaxLength(150);
        b.Property(c => c.EmpresaColaborador).HasMaxLength(200);
        b.Property(c => c.GeneroColaborador).HasMaxLength(40);
        b.Property(c => c.CentroCostos).HasMaxLength(150);
        b.Property(c => c.JefeInmediato).HasMaxLength(200);
        b.Property(c => c.RelacionLaboral).HasMaxLength(100);
        b.Property(c => c.FechaIngreso);
        b.Property(c => c.FechaFirma);
        b.Property(c => c.SolicitadoPor).HasMaxLength(200);
        b.Property(c => c.AutorizadoPor).HasMaxLength(200);
        b.Property(c => c.Descripcion).HasMaxLength(2000);
        b.Property(c => c.Tipo).HasMaxLength(150);
        b.Property(c => c.TipoCurso).HasMaxLength(150);
        b.Property(c => c.NombreCurso).HasMaxLength(250);
        b.Property(c => c.Marca).HasMaxLength(150);
        b.Property(c => c.FechaInicioCurso);
        b.Property(c => c.FechaFinCurso);
        b.Property(c => c.Horas).HasColumnType("decimal(6,2)");
        b.Property(c => c.Resultado).HasMaxLength(40);
        b.Property(c => c.ConvenioFirmado).IsRequired().HasDefaultValue(false);
        b.Property(c => c.Fecha).IsRequired();
        b.Property(c => c.ValorAsumidoEmpresa).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValue(0m);
        b.Property(c => c.MesesADevengar).IsRequired();
        b.Property(c => c.Estado).HasConversion<int>().IsRequired();
        b.Property(c => c.FechaCorte);
        b.Property(c => c.MontoCongelado).HasColumnType("decimal(18,2)");

        b.Property(c => c.Activo).IsRequired().HasDefaultValue(true);
        b.Property(c => c.FechaCreacion).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(c => c.FechaActualizacion);

        b.HasIndex(c => c.CedulaColaborador).HasDatabaseName("IX_Convenio_Cedula");

        // Auto-referencia opcional: un convenio puede ser parte/continuación de otro previo.
        // Sin cascade (NoAction) para no borrar en cadena ni crear ciclos en SQL Server.
        b.HasOne(c => c.ConvenioReferencia)
            .WithMany()
            .HasForeignKey(c => c.ConvenioReferenciaId)
            .OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(c => c.ConvenioReferenciaId).HasDatabaseName("IX_Convenio_Referencia");

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

/// <summary>Configuración EF Core del contador de numeración de convenios (fila única Id = 1).</summary>
public class ConvenioNumeracionConfiguration : IEntityTypeConfiguration<ConvenioNumeracion>
{
    public void Configure(EntityTypeBuilder<ConvenioNumeracion> b)
    {
        b.ToTable("ConvenioNumeracion", "dbo");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.SiguienteNumero).IsRequired();
        b.Property(c => c.UltimaActualizacion);

        // Seed: la fila única se crea al aplicar la migración.
        b.HasData(new ConvenioNumeracion { Id = 1, SiguienteNumero = 1, UltimaActualizacion = null });
    }
}
