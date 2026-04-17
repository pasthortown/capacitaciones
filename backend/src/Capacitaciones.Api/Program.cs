using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Catalogos;
using Capacitaciones.Domain.Entities;
using Capacitaciones.Infrastructure.Adapters.Xlsx;
using Capacitaciones.Infrastructure.Persistence;
using Capacitaciones.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// EF Core DbContext (SQL Server).
var connectionString = builder.Configuration.GetConnectionString("Default") ?? string.Empty;
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        opt.UseSqlServer(connectionString);
    }
    else
    {
        // Connection string ausente: permitimos que el DI resuelva pero las operaciones fallarán.
        // Program.cs loggea una advertencia al inicio; esto evita un crash al arrancar en entornos sin BD lista.
        opt.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Capacitaciones;Trusted_Connection=True;Encrypt=False;");
    }
});

// Repositorios (adaptadores EF Core).
builder.Services.AddScoped<IModalidadRepository, ModalidadRepository>();
builder.Services.AddScoped<ITipoActividadRepository, TipoActividadRepository>();
builder.Services.AddScoped<IAreaRepository, AreaRepository>();

// También registramos el puerto genérico ICatalogoRepository<T> para que el CatalogoService<T>
// pueda resolverlo directamente sin acoplarse a los puertos específicos.
builder.Services.AddScoped<ICatalogoRepository<Modalidad>>(sp => sp.GetRequiredService<IModalidadRepository>());
builder.Services.AddScoped<ICatalogoRepository<TipoActividad>>(sp => sp.GetRequiredService<ITipoActividadRepository>());
builder.Services.AddScoped<ICatalogoRepository<Area>>(sp => sp.GetRequiredService<IAreaRepository>());

// Casos de uso.
builder.Services.AddScoped<CatalogoService<Modalidad>>();
builder.Services.AddScoped<CatalogoService<TipoActividad>>();
builder.Services.AddScoped<CatalogoService<Area>>();

// Adaptador XLSX.
builder.Services.AddSingleton<IXlsxTemplateService, ClosedXmlTemplateService>();

// CORS permisivo solo en Development. En otros entornos se deberá restringir.
const string DevCorsPolicy = "DevCorsPolicy";
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(DevCorsPolicy, policy =>
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });
}

var app = builder.Build();

// --- Migraciones automáticas (Development) ---
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInit");
    try
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("ConnectionStrings:Default no está configurado. La API arrancará pero los endpoints de BD fallarán hasta configurarla.");
        }
        else
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
            logger.LogInformation("Migraciones aplicadas correctamente.");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "No se pudieron aplicar las migraciones al arrancar. La API seguirá en pie; revise la configuración de la base de datos.");
    }
}

// --- Pipeline ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCorsPolicy);
}

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Expuesto para Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory<Program>).
public partial class Program { }
