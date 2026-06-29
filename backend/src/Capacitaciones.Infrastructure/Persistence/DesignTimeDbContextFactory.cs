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
        // Placeholder: EF solo lo usa para inferir el proveedor (SQL Server) al emitir/quitar
        // migraciones; no se conecta. Se evita LocalDB porque no se soporta en Linux.
        var conn = Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? "Server=localhost;Database=Design;User Id=sa;Password=Design_placeholder1;TrustServerCertificate=True;Encrypt=False;";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(conn)
            .Options;

        return new AppDbContext(options);
    }
}
