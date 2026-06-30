using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Capacitaciones.Infrastructure.Persistence;

/// <summary>
/// DbContext raíz de la aplicación.
/// Fase 1: catálogos administrables. Fase 2: AdminUser y ConfiguracionNumeracion.
/// Fase 3: Capacitacion + Responsable (sub-colección).
/// Fase 5: Asistente (inscripción pública por link firmado).
/// Refactor Responsables: Responsable pasa a catálogo global + pivote N–N CapacitacionResponsable.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Modalidad> Modalidades => Set<Modalidad>();
    public DbSet<TipoActividad> TiposActividad => Set<TipoActividad>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<ConfiguracionNumeracion> ConfiguracionNumeracion => Set<ConfiguracionNumeracion>();
    public DbSet<Capacitacion> Capacitaciones => Set<Capacitacion>();
    public DbSet<Responsable> Responsables => Set<Responsable>();
    public DbSet<CapacitacionResponsable> CapacitacionResponsables => Set<CapacitacionResponsable>();
    public DbSet<Asistente> Asistentes => Set<Asistente>();
    public DbSet<Recurso> Recursos => Set<Recurso>();
    public DbSet<PreguntaEncuesta> PreguntasEncuesta => Set<PreguntaEncuesta>();
    public DbSet<RespuestaEncuesta> RespuestasEncuesta => Set<RespuestaEncuesta>();
    public DbSet<Colaborador> Colaboradores => Set<Colaborador>();
    public DbSet<Convenio> Convenios => Set<Convenio>();
    public DbSet<ConvenioItem> ConvenioItems => Set<ConvenioItem>();
    public DbSet<ConvenioAnexo> ConvenioAnexos => Set<ConvenioAnexo>();
    public DbSet<ConvenioNumeracion> ConvenioNumeracion => Set<ConvenioNumeracion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // SQL Server DATETIME2 no guarda zona: EF Core devuelve DateTime con Kind=Unspecified.
        // System.Text.Json serializa Kind!=Utc sin la "Z", y los clientes JS interpretan esos
        // strings como hora local (corrimiento visible de 5h en Ecuador/Colombia/Perú).
        //
        // Aplicamos un ValueConverter global: al leer, marcamos como Kind=Utc; al escribir,
        // si llegara algo con Kind!=Utc lo reinterpretamos. Toda la app usa DateTime.UtcNow,
        // así que la intención siempre es UTC.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var utcNullableConverter = new ValueConverter<DateTime?, DateTime?>(
            v => !v.HasValue
                ? v
                : (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)),
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(utcNullableConverter);
                }
            }
        }
    }
}
