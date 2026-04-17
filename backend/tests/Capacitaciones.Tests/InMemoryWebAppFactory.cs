using Capacitaciones.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Capacitaciones.Tests;

/// <summary>
/// WebApplicationFactory que reemplaza AppDbContext por un proveedor EF InMemory.
/// Evita la dependencia de SQL Server durante los tests.
/// </summary>
public class InMemoryWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "CapacitacionesTestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remueve la registración SQL Server existente.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseInMemoryDatabase(_databaseName);
                // Ignorar warnings por HasDefaultValueSql no soportado por InMemory.
                opt.ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
