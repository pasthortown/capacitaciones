using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Capacitaciones.Infrastructure.Persistence;

/// <summary>
/// Factoría consumida por <c>dotnet ef</c> para instanciar el DbContext sin necesidad
/// de conectarse a una base real. La connection string es un placeholder: EF solo la
/// usa para inferir el proveedor (SQL Server) al emitir migraciones.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=Design;Trusted_Connection=True;Encrypt=False;")
            .Options;

        return new AppDbContext(options);
    }
}
