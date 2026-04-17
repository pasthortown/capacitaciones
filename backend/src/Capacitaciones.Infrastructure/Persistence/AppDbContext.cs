using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence;

/// <summary>
/// DbContext raíz de la aplicación.
/// Fase 1: catálogos administrables. Fase 2: AdminUser y ConfiguracionNumeracion.
/// Fase 3: Capacitacion + Responsable (sub-colección).
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
