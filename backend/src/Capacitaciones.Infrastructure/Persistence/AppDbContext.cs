using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence;

/// <summary>
/// DbContext raíz de la aplicación.
/// Fase 1: catálogos administrables. Fase 2: AdminUser y ConfiguracionNumeracion.
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
