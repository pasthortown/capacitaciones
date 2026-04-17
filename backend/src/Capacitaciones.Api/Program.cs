using System.Text;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Admin;
using Capacitaciones.Application.UseCases.Asistentes;
using Capacitaciones.Application.UseCases.Auth;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Application.UseCases.Catalogos;
using Capacitaciones.Application.UseCases.Configuracion;
using Capacitaciones.Application.UseCases.Inscripcion;
using Capacitaciones.Application.UseCases.Responsable;
using Capacitaciones.Application.UseCases.Responsables;
using Capacitaciones.Domain.Entities;
using Capacitaciones.Infrastructure.Adapters.Xlsx;
using Capacitaciones.Infrastructure.Persistence;
using Capacitaciones.Infrastructure.Persistence.Repositories;
using Capacitaciones.Infrastructure.Persistence.Services;
using Capacitaciones.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger con soporte Bearer para que el UI muestre el botón "Authorize".
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Capacitaciones API",
        Version = "v1"
    });

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Bearer. Formato: `Bearer {token}`.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };
    options.AddSecurityDefinition(bearerScheme.Reference.Id, bearerScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [bearerScheme] = Array.Empty<string>()
    });
});
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

// --- JWT ---
// Bind Jwt:* desde configuración (appsettings + env JWT_*). Las env vars con prefijo JWT_
// se mapean a Jwt:* si se usan las variables "Jwt__Secret", "Jwt__Issuer", "Jwt__Audience".
// Adicionalmente soportamos las variables sugeridas por instrucciones (JWT_SECRET, JWT_ISSUER, JWT_AUDIENCE).
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.Secret = FirstNonEmpty(
    Environment.GetEnvironmentVariable("JWT_SECRET"),
    jwtOptions.Secret);
jwtOptions.Issuer = FirstNonEmpty(
    Environment.GetEnvironmentVariable("JWT_ISSUER"),
    jwtOptions.Issuer);
jwtOptions.Audience = FirstNonEmpty(
    Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
    jwtOptions.Audience);

// En entornos de test (InMemoryWebAppFactory setea ASPNETCORE_ENVIRONMENT=Testing) se permite un secret por defecto.
if (string.IsNullOrWhiteSpace(jwtOptions.Secret))
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        jwtOptions.Secret = "test-secret-please-change-but-long-enough-for-hmac-sha256-signing";
    }
    else
    {
        throw new InvalidOperationException(
            "JWT_SECRET / Jwt:Secret no está configurado. Abortando arranque: la autenticación no puede operar sin un secret.");
    }
}

if (string.IsNullOrWhiteSpace(jwtOptions.Issuer)) jwtOptions.Issuer = "capacitaciones-api";
if (string.IsNullOrWhiteSpace(jwtOptions.Audience)) jwtOptions.Audience = "capacitaciones-web";

builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jwtOptions));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Admin", p => p.RequireRole("Admin"));
    // Policy Fase 4: el token del capacitador trae claim role=Capacitador (emitido por
    // IJwtTokenGenerator.GenerateCapacitadorToken). El controller valida además el claim cid.
    o.AddPolicy("Capacitador", p => p.RequireRole("Capacitador"));
    // Policy Fase 5: token del link público de inscripción (role=Inscripcion).
    o.AddPolicy("Inscripcion", p => p.RequireRole("Inscripcion"));
    // Policy Refactor Responsables: token del link público del responsable (role=Responsable).
    // El controller valida además el claim rid.
    o.AddPolicy("Responsable", p => p.RequireRole("Responsable"));
});

// Repositorios (adaptadores EF Core).
builder.Services.AddScoped<IModalidadRepository, ModalidadRepository>();
builder.Services.AddScoped<ITipoActividadRepository, TipoActividadRepository>();
builder.Services.AddScoped<IAreaRepository, AreaRepository>();
builder.Services.AddScoped<IAdminUserRepository, AdminUserRepository>();
builder.Services.AddScoped<IConfiguracionNumeracionRepository, ConfiguracionNumeracionRepository>();
builder.Services.AddScoped<ICapacitacionRepository, CapacitacionRepository>();
builder.Services.AddScoped<IResponsableRepository, ResponsableRepository>();
builder.Services.AddScoped<IAsistenteRepository, AsistenteRepository>();

// También registramos el puerto genérico ICatalogoRepository<T> para que el CatalogoService<T>
// pueda resolverlo directamente sin acoplarse a los puertos específicos.
builder.Services.AddScoped<ICatalogoRepository<Modalidad>>(sp => sp.GetRequiredService<IModalidadRepository>());
builder.Services.AddScoped<ICatalogoRepository<TipoActividad>>(sp => sp.GetRequiredService<ITipoActividadRepository>());
builder.Services.AddScoped<ICatalogoRepository<Area>>(sp => sp.GetRequiredService<IAreaRepository>());

// Seguridad.
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

// Casos de uso.
builder.Services.AddScoped<CatalogoService<Modalidad>>();
builder.Services.AddScoped<CatalogoService<TipoActividad>>();
builder.Services.AddScoped<CatalogoService<Area>>();
builder.Services.AddScoped<LoginUseCase>();
builder.Services.AddScoped<CrearAdminUseCase>();
builder.Services.AddScoped<ListarAdminsUseCase>();
builder.Services.AddScoped<EliminarAdminUseCase>();
builder.Services.AddScoped<ObtenerNumeracionUseCase>();
builder.Services.AddScoped<ActualizarNumeracionUseCase>();
builder.Services.AddScoped<ListarCapacitacionesUseCase>();
builder.Services.AddScoped<ObtenerCapacitacionUseCase>();
builder.Services.AddScoped<CrearCapacitacionUseCase>();
builder.Services.AddScoped<EditarCapacitacionUseCase>();
builder.Services.AddScoped<EliminarCapacitacionUseCase>();

// Fase 4 — flujo del capacitador (link firmado + GET/PUT sobre su propia capacitación).
builder.Services.AddScoped<GenerarLinkCapacitadorUseCase>();
builder.Services.AddScoped<ObtenerCapacitacionCapacitadorUseCase>();
builder.Services.AddScoped<ActualizarCapacitadorCapacitacionUseCase>();

// Fase 5 — flujo público de inscripción + listado admin de asistentes + stub certificado.
builder.Services.AddScoped<GenerarLinkInscripcionUseCase>();
builder.Services.AddScoped<ObtenerInscripcionPublicaUseCase>();
builder.Services.AddScoped<InscribirAsistenteUseCase>();
builder.Services.AddScoped<ListarAsistentesUseCase>();
builder.Services.AddScoped<DescargarCertificadoUseCase>();

// Refactor Responsables — catálogo global + link firmado para página pública.
builder.Services.AddScoped<ListarResponsablesUseCase>();
builder.Services.AddScoped<ObtenerResponsableUseCase>();
builder.Services.AddScoped<CrearResponsableUseCase>();
builder.Services.AddScoped<EditarResponsableUseCase>();
builder.Services.AddScoped<EliminarResponsableUseCase>();
builder.Services.AddScoped<GenerarLinkResponsableUseCase>();
builder.Services.AddScoped<ObtenerPerfilResponsableUseCase>();
builder.Services.AddScoped<ActualizarPerfilResponsableUseCase>();

// Servicio de numeración (consumido en Fase 3).
builder.Services.AddScoped<INumeracionService, NumeracionService>();

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

// --- Migraciones + seed del admin inicial (Development) ---
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

            await SeedInitialAdminAsync(scope.ServiceProvider, logger);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "No se pudieron aplicar las migraciones o sembrar el admin inicial. La API seguirá en pie; revise la configuración.");
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// --- Helpers ---
static string FirstNonEmpty(params string?[] values)
{
    foreach (var v in values)
    {
        if (!string.IsNullOrWhiteSpace(v)) return v!;
    }
    return string.Empty;
}

static async Task SeedInitialAdminAsync(IServiceProvider services, ILogger logger)
{
    var repo = services.GetRequiredService<IAdminUserRepository>();

    if (await repo.AnyActivoAsync())
    {
        return;
    }

    var email = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
    var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

    if (string.IsNullOrWhiteSpace(email))
    {
        email = "admin@dos.com.ec";
    }

    if (string.IsNullOrWhiteSpace(password))
    {
        logger.LogWarning(
            "No se sembró el admin inicial: falta la variable de entorno ADMIN_PASSWORD. " +
            "Defínela (y opcionalmente ADMIN_EMAIL) para habilitar el seed.");
        return;
    }

    var hasher = services.GetRequiredService<IPasswordHasher>();
    var entity = new AdminUser
    {
        Id = Guid.NewGuid(),
        Email = email.Trim(),
        PasswordHash = hasher.Hash(password),
        Nombres = "Administrador",
        Activo = true,
        FechaCreacion = DateTime.UtcNow,
        FechaActualizacion = null,
        UltimoLogin = null
    };
    await repo.AddAsync(entity);
    logger.LogInformation("Admin inicial sembrado con email '{Email}'.", entity.Email);
}

// Expuesto para Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory<Program>).
public partial class Program { }
