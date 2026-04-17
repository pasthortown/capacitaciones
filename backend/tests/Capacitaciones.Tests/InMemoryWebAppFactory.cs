using System.Net.Http.Json;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Capacitaciones.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Capacitaciones.Tests;

/// <summary>
/// WebApplicationFactory que reemplaza AppDbContext por un proveedor EF InMemory y
/// siembra un <see cref="AdminUser"/> determinístico para pruebas de autenticación.
/// </summary>
public class InMemoryWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "CapacitacionesTestDb_" + Guid.NewGuid();

    /// <summary>Email del admin sembrado para las pruebas.</summary>
    public const string SeededAdminEmail = "test.admin@dos.com.ec";

    /// <summary>Contraseña del admin sembrado para las pruebas.</summary>
    public const string SeededAdminPassword = "ChangeMe!2026";

    /// <summary>Id del admin sembrado (estable por test run).</summary>
    public Guid SeededAdminId { get; } = Guid.NewGuid();

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

            // Seed del admin de pruebas (directo sobre el DbContext, saltándose el caso de uso).
            if (!db.AdminUsers.Any(u => u.Email == SeededAdminEmail))
            {
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                db.AdminUsers.Add(new AdminUser
                {
                    Id = SeededAdminId,
                    Email = SeededAdminEmail,
                    PasswordHash = hasher.Hash(SeededAdminPassword),
                    Nombres = "Test Admin",
                    Activo = true,
                    FechaCreacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    FechaActualizacion = null,
                    UltimoLogin = null
                });
                db.SaveChanges();
            }
        });
    }

    /// <summary>Crea un HttpClient con el Authorization header ya configurado para el admin sembrado.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();
        var token = await ObtainSeededAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> ObtainSeededAdminTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = SeededAdminEmail,
            password = SeededAdminPassword
        });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<LoginPayload>();
        return payload?.Token ?? throw new InvalidOperationException("Login no devolvió token.");
    }

    private sealed class LoginPayload
    {
        public string Token { get; set; } = string.Empty;
    }
}
