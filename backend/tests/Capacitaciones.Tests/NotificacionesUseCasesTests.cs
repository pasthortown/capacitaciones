using Capacitaciones.Application.UseCases.Configuracion;
using Capacitaciones.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Tests;

public class NotificacionesUseCasesTests
{
    [Fact]
    public void Seed_CreaLosNueveAvisosActivos()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("SeedNotificaciones_" + Guid.NewGuid())
            .Options;
        using var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        var filas = db.ConfiguracionNotificaciones.OrderBy(n => n.Plantilla).ToList();

        Assert.Equal(9, filas.Count);
        Assert.All(filas, f => Assert.True(f.Activo));
        Assert.All(filas, f => Assert.Null(f.AsuntoPersonalizado));
        Assert.Equal(
            NotificacionesCatalogo.Todas.Select(d => d.Plantilla).OrderBy(p => p),
            filas.Select(f => f.Plantilla));
    }
}
