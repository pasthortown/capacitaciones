using System.Text;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Admin;
using Capacitaciones.Application.UseCases.Asistentes;
using Capacitaciones.Application.UseCases.Auth;
using Capacitaciones.Application.UseCases.Calificaciones;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Application.UseCases.Catalogos;
using Capacitaciones.Application.UseCases.Certificados;
using Capacitaciones.Application.UseCases.Colaboradores;
using Capacitaciones.Application.UseCases.Convenios;
using Capacitaciones.Application.UseCases.Configuracion;
using Capacitaciones.Application.UseCases.Encuesta;
using Capacitaciones.Application.UseCases.Inscripcion;
using Capacitaciones.Application.UseCases.Notifications;
using Capacitaciones.Application.UseCases.PaseLista;
using Capacitaciones.Application.UseCases.PreguntasEncuesta;
using Capacitaciones.Application.UseCases.Recursos;
using Capacitaciones.Application.UseCases.Responsable;
using Capacitaciones.Application.UseCases.Responsables;
using Capacitaciones.Domain.Entities;
using Capacitaciones.Infrastructure.Adapters.Storage;
using Capacitaciones.Infrastructure.Adapters.Xlsx;
using Capacitaciones.Infrastructure.Persistence;
using Capacitaciones.Infrastructure.Persistence.Repositories;
using Capacitaciones.Infrastructure.Persistence.Services;
using Capacitaciones.Infrastructure.Security;
using Capacitaciones.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
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
            // Los tokens de enlaces (capacitador/inscripción/responsable/pase-lista/calificaciones)
            // se emiten sin claim `exp` para que NO caduquen. Con RequireExpirationTime=false la
            // validación de lifetime los acepta; el token de admin sigue trayendo `exp` y, por tanto,
            // sigue caducando normalmente.
            RequireExpirationTime = false,
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
    // Policy Fase 10: token de pase de lista (role=PaseLista). El controller valida el claim cid.
    o.AddPolicy("PaseLista", p => p.RequireRole("PaseLista"));
    // Policy Fase 11: token de calificaciones (role=Calificaciones). El controller valida el claim cid.
    o.AddPolicy("Calificaciones", p => p.RequireRole("Calificaciones"));
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
builder.Services.AddScoped<IRecursoRepository, RecursoRepository>();
builder.Services.AddScoped<IColaboradorRepository, ColaboradorRepository>();
builder.Services.AddScoped<IConvenioRepository, ConvenioRepository>();
builder.Services.AddScoped<IPreguntaEncuestaRepository, PreguntaEncuestaRepository>();
builder.Services.AddScoped<IRespuestaEncuestaRepository, RespuestaEncuestaRepository>();

// También registramos el puerto genérico ICatalogoRepository<T> para que el CatalogoService<T>
// pueda resolverlo directamente sin acoplarse a los puertos específicos.
builder.Services.AddScoped<ICatalogoRepository<Modalidad>>(sp => sp.GetRequiredService<IModalidadRepository>());
builder.Services.AddScoped<ICatalogoRepository<TipoActividad>>(sp => sp.GetRequiredService<ITipoActividadRepository>());
builder.Services.AddScoped<ICatalogoRepository<Area>>(sp => sp.GetRequiredService<IAreaRepository>());

// Seguridad.
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

// Autenticación con dominio (AD vía SOAP del portal de servicios). Config por entorno:
// AD_AUTH_URL (.../CMDBWS/Authentication.asmx) + AD_DOMAIN (ej. COMPUEQUIP). Sin AD_AUTH_URL, deshabilitado.
builder.Services.AddSingleton(new Capacitaciones.Infrastructure.Security.AdOptions
{
    AuthUrl = Environment.GetEnvironmentVariable("AD_AUTH_URL"),
    Domain = Environment.GetEnvironmentVariable("AD_DOMAIN"),
});
builder.Services.AddHttpClient<IAdAuthenticator, Capacitaciones.Infrastructure.Security.SoapAdAuthenticator>();

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
builder.Services.AddScoped<SubirLogoCapacitacionUseCase>();
builder.Services.AddScoped<EliminarLogoCapacitacionUseCase>();

// Notificaciones por correo (mail_sender) — usadas por el controller después
// de operaciones admin (ej. crear/editar capacitación) para enviar al admin
// que disparó la acción un resumen con QR + link de inscripción pública.
builder.Services.AddSingleton<IQrGenerator, QrCoderQrGenerator>();
var mailSenderOptions = builder.Configuration.GetSection(MailSenderOptions.SectionName)
    .Get<MailSenderOptions>() ?? new MailSenderOptions();
if (string.IsNullOrWhiteSpace(mailSenderOptions.BaseUrl))
{
    mailSenderOptions.BaseUrl = "http://mail_sender:8000";
}
if (mailSenderOptions.TimeoutSeconds <= 0)
{
    mailSenderOptions.TimeoutSeconds = 60;
}
builder.Services.AddSingleton(mailSenderOptions);
builder.Services.AddHttpClient<IMailSenderClient, MailSenderHttpClient>(client =>
{
    var baseUrl = mailSenderOptions.BaseUrl;
    if (!baseUrl.EndsWith('/')) baseUrl += "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(mailSenderOptions.TimeoutSeconds);
});

var notificationsConfig = builder.Configuration.GetSection(NotificationsConfigOptions.SectionName)
    .Get<NotificationsConfigOptions>() ?? new NotificationsConfigOptions();
builder.Services.AddSingleton<INotificationsConfig>(notificationsConfig);

builder.Services.AddScoped<NotificarLinksCapacitadorUseCase>();
builder.Services.AddScoped<NotificarResponsableFirmaUseCase>();
builder.Services.AddScoped<EnviarInvitacionInscripcionUseCase>();
builder.Services.AddScoped<EnviarReporteAsistenciaAdminUseCase>();
builder.Services.AddScoped<GenerarYEnviarCertificadosUseCase>();

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
builder.Services.AddScoped<DescargarReporteAsistenciaUseCase>();

// Fase 6 — integración con servicio externo emisor_documentos (Node + Puppeteer).
var emisorOptions = builder.Configuration.GetSection(EmisorDocumentosOptions.SectionName)
    .Get<EmisorDocumentosOptions>() ?? new EmisorDocumentosOptions();
if (string.IsNullOrWhiteSpace(emisorOptions.BaseUrl))
{
    emisorOptions.BaseUrl = "http://emisor_documentos:3000";
}
if (emisorOptions.TimeoutSeconds <= 0)
{
    emisorOptions.TimeoutSeconds = 120;
}
builder.Services.AddSingleton(emisorOptions);

var certificadosOptions = builder.Configuration.GetSection(CertificadosOptions.SectionName)
    .Get<CertificadosOptions>() ?? new CertificadosOptions();
if (string.IsNullOrWhiteSpace(certificadosOptions.OutputDir))
{
    certificadosOptions.OutputDir = "/output";
}

// Firma por defecto: logo DOS embebido como data URI. Se usa para cualquier firmante
// (capacitador o responsable) que no tenga firma cargada, evitando que la emisión se
// detenga por FIRMAS_FALTANTES. Precedencia: env FIRMA_POR_DEFECTO_PATH > asset bundleado.
if (string.IsNullOrWhiteSpace(certificadosOptions.FirmaPorDefecto))
{
    var firmaDefaultPath = Environment.GetEnvironmentVariable("FIRMA_POR_DEFECTO_PATH");
    if (string.IsNullOrWhiteSpace(firmaDefaultPath))
    {
        firmaDefaultPath = Path.Combine(builder.Environment.ContentRootPath, "Assets", "firma-capacitador-default.svg");
    }
    try
    {
        if (File.Exists(firmaDefaultPath))
        {
            var svgBytes = File.ReadAllBytes(firmaDefaultPath);
            var mime = firmaDefaultPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? "image/svg+xml"
                : "image/png";
            certificadosOptions.FirmaPorDefecto = $"data:{mime};base64,{Convert.ToBase64String(svgBytes)}";
        }
    }
    catch
    {
        // Si no se puede leer el asset, dejamos FirmaPorDefecto nula y se conserva el
        // comportamiento anterior (faltantes ⇒ 409). No bloqueamos el arranque por esto.
    }
}

builder.Services.AddSingleton(certificadosOptions);

builder.Services.AddHttpClient<IEmisorDocumentosClient, EmisorDocumentosHttpClient>(client =>
{
    var baseUrl = emisorOptions.BaseUrl;
    // Garantizamos el trailing slash para que la ruta relativa ("emitir/certificado") resuelva correcto.
    if (!baseUrl.EndsWith('/')) baseUrl += "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(emisorOptions.TimeoutSeconds);
});

builder.Services.AddScoped<GenerarCertificadoAsistenteUseCase>();
builder.Services.AddScoped<GenerarCertificadosCapacitacionUseCase>();

// Envío de certificados en segundo plano: cola en proceso (singleton) + worker hosted.
// El endpoint "generar-y-enviar" marca pendientes y encola; el worker procesa fuera del request.
builder.Services.AddSingleton<Capacitaciones.Application.Ports.ICertificadoEnvioQueue,
    Capacitaciones.Infrastructure.Services.CertificadoEnvioQueue>();
builder.Services.AddHostedService<Capacitaciones.Api.BackgroundServices.CertificadoEnvioBackgroundService>();

// Módulo Repositorio — storage de archivos + CRUD de recursos.
// Precedencia de configuración: env var REPOSITORIO_DIR > appsettings "ResourceStorage:Directory" > default "/repository".
var resourceStorageOptions = builder.Configuration.GetSection(ResourceStorageOptions.SectionName)
    .Get<ResourceStorageOptions>() ?? new ResourceStorageOptions();
var repositorioDirEnv = Environment.GetEnvironmentVariable("REPOSITORIO_DIR");
if (!string.IsNullOrWhiteSpace(repositorioDirEnv))
{
    resourceStorageOptions.Directory = repositorioDirEnv;
}
if (string.IsNullOrWhiteSpace(resourceStorageOptions.Directory))
{
    resourceStorageOptions.Directory = "/repository";
}
builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(resourceStorageOptions));
builder.Services.AddSingleton<IResourceStorage, FileSystemResourceStorage>();

// Fase 9 — Logo de capacitación. Mismo patrón: env IMAGEN_CAPACITACIONES_DIR > appsettings > default.
var logoStorageOptions = builder.Configuration.GetSection(LogoCapacitacionStorageOptions.SectionName)
    .Get<LogoCapacitacionStorageOptions>() ?? new LogoCapacitacionStorageOptions();
var logoDirEnv = Environment.GetEnvironmentVariable("IMAGEN_CAPACITACIONES_DIR");
if (!string.IsNullOrWhiteSpace(logoDirEnv))
{
    logoStorageOptions.Directory = logoDirEnv;
}
if (string.IsNullOrWhiteSpace(logoStorageOptions.Directory))
{
    logoStorageOptions.Directory = "/imagen_capacitaciones";
}
builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(logoStorageOptions));
builder.Services.AddSingleton<ILogoCapacitacionStorage, LogoCapacitacionStorage>();

builder.Services.AddScoped<SubirRecursoUseCase>();
builder.Services.AddScoped<ListarRecursosUseCase>();
builder.Services.AddScoped<ObtenerRecursoUseCase>();
builder.Services.AddScoped<EditarMetadataRecursoUseCase>();
builder.Services.AddScoped<EliminarRecursoUseCase>();
builder.Services.AddScoped<DescargarRecursoUseCase>();
builder.Services.AddScoped<GenerarLinkDescargaRecursoUseCase>();

// Upload multipart: 100 MB máximo (alineado con SubirRecursoUseCase.MaxBytes y el RequestSizeLimit
// del controller). Kestrel aparte, porque FormOptions no afecta el body-size del servidor.
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 100_000_000;
    o.ValueLengthLimit = 100_000_000;
});
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 100_000_000);

// Fase 10 — pase de lista. Link firmado admin + endpoints públicos con token PaseLista
// + endpoint admin PUT para corregir asistencia desde la tabla de listado.
builder.Services.AddScoped<GenerarLinkPaseListaUseCase>();
builder.Services.AddScoped<ObtenerPaseListaUseCase>();
builder.Services.AddScoped<MarcarAsistenciaUseCase>();

// Fase 11 — calificaciones. Link firmado admin + endpoints públicos con token Calificaciones
// + endpoint admin PUT para editar calificación desde la tabla de listado.
builder.Services.AddScoped<GenerarLinkCalificacionesUseCase>();
builder.Services.AddScoped<ObtenerCalificacionesUseCase>();
builder.Services.AddScoped<CalificarAsistenteUseCase>();

// Encuesta de satisfacción — CRUD admin del catálogo de preguntas + endpoints públicos.
builder.Services.AddScoped<ListarPreguntasEncuestaUseCase>();
builder.Services.AddScoped<ObtenerPreguntaEncuestaUseCase>();
builder.Services.AddScoped<CrearPreguntaEncuestaUseCase>();
builder.Services.AddScoped<EditarPreguntaEncuestaUseCase>();
builder.Services.AddScoped<EliminarPreguntaEncuestaUseCase>();
builder.Services.AddScoped<ObtenerEncuestaPublicaUseCase>();
builder.Services.AddScoped<SubmitEncuestaUseCase>();
builder.Services.AddScoped<ObtenerResultadosEncuestaUseCase>();
builder.Services.AddScoped<DescargarReporteEncuestaUseCase>();

// Servicio externo emisor_reportes (Python + matplotlib + reportlab).
var emisorReportesOptions = builder.Configuration.GetSection(EmisorReportesOptions.SectionName)
    .Get<EmisorReportesOptions>() ?? new EmisorReportesOptions();
if (string.IsNullOrWhiteSpace(emisorReportesOptions.BaseUrl))
{
    emisorReportesOptions.BaseUrl = "http://emisor_reportes:5000";
}
if (emisorReportesOptions.TimeoutSeconds <= 0)
{
    emisorReportesOptions.TimeoutSeconds = 120;
}
builder.Services.AddSingleton(emisorReportesOptions);
builder.Services.AddHttpClient<IEmisorReportesClient, EmisorReportesHttpClient>(client =>
{
    var baseUrl = emisorReportesOptions.BaseUrl;
    if (!baseUrl.EndsWith('/')) baseUrl += "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(emisorReportesOptions.TimeoutSeconds);
});

// --- Integración con ControlTareas (SGI): trae los colaboradores internos de DOS. ---
// Config por entorno (la URL cambiará a dominio en el futuro). Sin URL/usuario/clave queda
// deshabilitada y la pestaña "DOS" sale vacía sin romper la app.
var controlTareasOptions = new ControlTareasOptions
{
    BaseUrl = Environment.GetEnvironmentVariable("CONTROLTAREAS_API_URL"),
    User = Environment.GetEnvironmentVariable("CONTROLTAREAS_API_USER"),
    Password = Environment.GetEnvironmentVariable("CONTROLTAREAS_API_PASSWORD"),
};
var ctTimeout = Environment.GetEnvironmentVariable("CONTROLTAREAS_API_TIMEOUT_SECONDS");
if (int.TryParse(ctTimeout, out var ctSecs) && ctSecs > 0) controlTareasOptions.TimeoutSeconds = ctSecs;
builder.Services.AddSingleton(controlTareasOptions);
builder.Services.AddHttpClient<IControlTareasColaboradoresClient, ControlTareasHttpClient>(client =>
{
    if (!string.IsNullOrWhiteSpace(controlTareasOptions.BaseUrl))
    {
        var baseUrl = controlTareasOptions.BaseUrl!;
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        client.BaseAddress = new Uri(baseUrl);
    }
    client.Timeout = TimeSpan.FromSeconds(controlTareasOptions.TimeoutSeconds);
});
builder.Services.AddScoped<ListarColaboradoresDosUseCase>();
builder.Services.AddScoped<ListarColaboradoresExternosUseCase>();
builder.Services.AddScoped<ObtenerColaboradorExternoUseCase>();
builder.Services.AddScoped<CrearColaboradorExternoUseCase>();
builder.Services.AddScoped<EditarColaboradorExternoUseCase>();
builder.Services.AddScoped<EliminarColaboradorExternoUseCase>();
builder.Services.AddScoped<BuscarColaboradorPorCedulaUseCase>();

// --- Convenios (Entrenamiento) ---
var conveniosDir = Environment.GetEnvironmentVariable("CONVENIOS_DIR");
var convenioAnexoOptions = new ConvenioAnexoStorageOptions
{
    Directory = string.IsNullOrWhiteSpace(conveniosDir) ? "/convenios_anexos" : conveniosDir,
};
builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(convenioAnexoOptions));
builder.Services.AddSingleton<IConvenioAnexoStorage, FileSystemConvenioAnexoStorage>();
builder.Services.AddScoped<ListarConveniosUseCase>();
builder.Services.AddScoped<ObtenerConvenioUseCase>();
builder.Services.AddScoped<CrearConvenioUseCase>();
builder.Services.AddScoped<EditarConvenioUseCase>();
builder.Services.AddScoped<EliminarConvenioUseCase>();
builder.Services.AddScoped<ListarConveniosPorColaboradorUseCase>();
builder.Services.AddScoped<SubirAnexoConvenioUseCase>();
builder.Services.AddScoped<EliminarAnexoConvenioUseCase>();
builder.Services.AddScoped<DescargarAnexoConvenioUseCase>();
// Numeración de convenios (GIC-EC-REG-###).
builder.Services.AddScoped<IConvenioNumeracionRepository, ConvenioNumeracionRepository>();
builder.Services.AddScoped<IConvenioNumeracionService, ConvenioNumeracionService>();
builder.Services.AddScoped<ObtenerConvenioNumeracionUseCase>();
builder.Services.AddScoped<ActualizarConvenioNumeracionUseCase>();
// PDFs de convenios (vía emisor_documentos) + dashboard + liquidación.
builder.Services.AddScoped<ImprimirConvenioUseCase>();
builder.Services.AddScoped<DescargarReporteConveniosUseCase>();
builder.Services.AddScoped<DashboardConveniosUseCase>();
builder.Services.AddScoped<LiquidacionColaboradorUseCase>();
builder.Services.AddScoped<DescargarReporteLiquidacionUseCase>();
builder.Services.AddScoped<DescargarAnexosConvenioZipUseCase>();
builder.Services.AddScoped<PaqueteDesvinculacionUseCase>();

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

// Eagerly resolve the resource storage to force the directory setup + effective-path log
// at startup (the adapter loggea el path absoluto en su constructor). En Testing saltamos
// esto porque el test factory reemplaza el puerto por un InMemoryResourceStorageAdapter.
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var bootScope = app.Services.CreateScope())
    {
        try
        {
            _ = bootScope.ServiceProvider.GetRequiredService<IResourceStorage>();
        }
        catch (Exception ex)
        {
            var bootLogger = bootScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RepositorioInit");
            bootLogger.LogWarning(ex, "No se pudo inicializar el directorio del repositorio. El endpoint de subida fallará hasta resolverlo.");
        }

        try
        {
            _ = bootScope.ServiceProvider.GetRequiredService<ILogoCapacitacionStorage>();
        }
        catch (Exception ex)
        {
            var bootLogger = bootScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("LogoCapacitacionInit");
            bootLogger.LogWarning(ex, "No se pudo inicializar el directorio de logos de capacitación. El upload de logo fallará hasta resolverlo.");
        }
    }
}

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
    // Lista inicial de usuarios de red permitidos (login por dominio + lista de permitidos).
    // Se agregan los que falten sin duplicar (idempotente). Editable luego en la ventana Usuarios.
    var repo = services.GetRequiredService<IAdminUserRepository>();
    string[] usuariosRed = { "dos", "gcomina", "lasalazar" };

    foreach (var u in usuariosRed)
    {
        var usuario = u.Trim();
        if (await repo.GetByUsuarioRedAsync(usuario) is not null) continue;
        await repo.AddAsync(new AdminUser
        {
            Id = Guid.NewGuid(),
            UsuarioRed = usuario,
            Email = string.Empty,
            PasswordHash = string.Empty,
            Nombres = string.Empty,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
        });
        logger.LogInformation("Usuario de red permitido sembrado: '{Usuario}'.", usuario);
    }
}

// Expuesto para Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory<Program>).
public partial class Program { }
