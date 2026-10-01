# Módulo de Configuración de Correo — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que un admin configure desde Configuración → Correo el servidor SMTP / remitente, active o desactive cada notificación, personalice sus asuntos y defina CC/BCC globales, sin tocar `.env`.

**Architecture:** La configuración se guarda en SQL Server (dos tablas nuevas) y se administra con endpoints Admin del backend .NET. `mail_sender` la lee de un endpoint interno del backend (protegido por `X-Internal-Key`, cache de 60 s) y la aplica en `POST /send-mail`; si no hay configuración o el backend no responde, usa el `.env` como hoy. Los llamadores existentes (casos de uso del backend y `event_monitor`) no cambian.

**Tech Stack:** ASP.NET Core 8 + EF Core 8 (SQL Server) + xUnit; Python 3.12 + FastAPI + Jinja2 + pytest; React 18 + Vite 5.

**Spec:** `docs/superpowers/specs/2026-09-23-configuracion-correo-design.md`

## Global Constraints

- Rama: `feature/configuracion-correo`. No hacer push ni PR sin pedido explícito del usuario.
- Commits con `git -c user.name="gcampuzano" -c user.email="gcampuzano@dos.com.ec" commit ...` y terminando en `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`.
- Backend: `net8.0`, paquetes EF Core `8.0.10`. Seguir el patrón de `ConfiguracionNumeracion` (entidad en Domain, puerto en Application/Ports, repo en Infrastructure/Persistence/Repositories, casos de uso en Application/UseCases/Configuracion, DTOs en Application/Dtos/Configuracion).
- Endpoints admin bajo `api/configuracion/correo`, política `Admin`. Endpoint interno `GET api/internal/correo-config` con header `X-Internal-Key`.
- Variables nuevas: `CORREO_ENCRYPTION_KEY` (32 bytes base64, backend), `MAIL_CONFIG_API_KEY` (backend y mail_sender), `BACKEND_INTERNAL_URL=http://capacitaciones-backend:8080` (mail_sender), `CONFIG_CACHE_SECONDS` (opcional, default 60).
- La contraseña SMTP nunca se devuelve al frontend. Cifrado AES-GCM, formato base64(nonce 12 B ‖ tag 16 B ‖ ciphertext).
- Límites: CC y BCC globales máx. 20 direcciones cada uno; asunto personalizado máx. 500 caracteres; puerto 1–65535.
- `mail_sender` responde `{"status": "omitido"}` para avisos desactivados y conserva `{"status": "sent"}` en envíos exitosos.
- Textos visibles al usuario en español.
- Nada de producción se toca sin confirmación explícita del usuario en cada paso (Task 14).

---

## Mapa de archivos

**Backend (`backend/`)**

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `src/Capacitaciones.Application/Ports/ISecretProtector.cs` | Crear | Puerto de cifrado de secretos |
| `src/Capacitaciones.Infrastructure/Security/AesGcmSecretProtector.cs` | Crear | Implementación AES-GCM |
| `src/Capacitaciones.Domain/Entities/ConfiguracionCorreo.cs` | Crear | Entidad fila única SMTP/remitente/copias |
| `src/Capacitaciones.Domain/Entities/ConfiguracionNotificacion.cs` | Crear | Entidad por tipo de aviso |
| `src/Capacitaciones.Application/UseCases/Configuracion/NotificacionesCatalogo.cs` | Crear | Catálogo fijo de los 9 avisos (nombre, asunto actual, variables) |
| `src/Capacitaciones.Infrastructure/Persistence/Configurations/ConfiguracionCorreoConfiguration.cs` | Crear | Mapeo EF |
| `src/Capacitaciones.Infrastructure/Persistence/Configurations/ConfiguracionNotificacionConfiguration.cs` | Crear | Mapeo EF + seed desde el catálogo |
| `src/Capacitaciones.Infrastructure/Persistence/AppDbContext.cs` | Modificar | 2 DbSets |
| `src/Capacitaciones.Infrastructure/Persistence/Migrations/*_ConfiguracionCorreo.cs` | Generar | Migración aditiva |
| `src/Capacitaciones.Application/Ports/IConfiguracionCorreoRepository.cs` | Crear | Puerto |
| `src/Capacitaciones.Application/Ports/IConfiguracionNotificacionRepository.cs` | Crear | Puerto |
| `src/Capacitaciones.Infrastructure/Persistence/Repositories/ConfiguracionCorreoRepository.cs` | Crear | Repo |
| `src/Capacitaciones.Infrastructure/Persistence/Repositories/ConfiguracionNotificacionRepository.cs` | Crear | Repo |
| `src/Capacitaciones.Application/Dtos/Configuracion/ConfiguracionCorreoDtos.cs` | Crear | DTOs admin + internos |
| `src/Capacitaciones.Application/UseCases/Configuracion/ConfiguracionCorreoValidator.cs` | Crear | Validación y parseo de listas de correos |
| `src/Capacitaciones.Application/UseCases/Configuracion/ConfiguracionCorreoException.cs` | Crear | Excepción con errores por campo |
| `src/Capacitaciones.Application/UseCases/Configuracion/ObtenerConfiguracionCorreoUseCase.cs` | Crear | GET admin |
| `src/Capacitaciones.Application/UseCases/Configuracion/ActualizarConfiguracionCorreoUseCase.cs` | Crear | PUT admin |
| `src/Capacitaciones.Application/UseCases/Configuracion/ListarNotificacionesUseCase.cs` | Crear | GET notificaciones |
| `src/Capacitaciones.Application/UseCases/Configuracion/ActualizarNotificacionesUseCase.cs` | Crear | PUT notificaciones |
| `src/Capacitaciones.Application/UseCases/Configuracion/ObtenerConfiguracionCorreoInternaUseCase.cs` | Crear | Config para mail_sender |
| `src/Capacitaciones.Application/UseCases/Configuracion/EnviarCorreoPruebaUseCase.cs` | Crear | Correo de prueba |
| `src/Capacitaciones.Application/Dtos/Notifications/MailSendResult.cs` | Crear | `Enviado`/`Omitido` + DTOs de prueba |
| `src/Capacitaciones.Application/Ports/IMailSenderClient.cs` | Modificar | Devuelve `MailSendResult`; agrega `SendTestAsync` |
| `src/Capacitaciones.Infrastructure/Services/MailSenderHttpClient.cs` | Modificar | Parsea `status`; implementa `/send-test` |
| `src/Capacitaciones.Domain/Entities/EstadoEnvioCertificado.cs` | Modificar | `Omitido = 4` |
| `src/Capacitaciones.Application/UseCases/Certificados/GenerarYEnviarCertificadosUseCase.cs` | Modificar | Marca `Omitido` |
| `src/Capacitaciones.Api/Filters/InternalApiKeyFilter.cs` | Crear | Valida `X-Internal-Key` |
| `src/Capacitaciones.Api/Controllers/ConfiguracionController.cs` | Modificar | Endpoints admin de correo |
| `src/Capacitaciones.Api/Controllers/InternalCorreoController.cs` | Crear | Endpoint interno |
| `src/Capacitaciones.Api/Program.cs` | Modificar | Registros DI |
| `tests/Capacitaciones.Tests/Fakes/CorreoFakes.cs` | Crear | Repos y mail client en memoria |
| `tests/Capacitaciones.Tests/AesGcmSecretProtectorTests.cs` | Crear | |
| `tests/Capacitaciones.Tests/ConfiguracionCorreoUseCasesTests.cs` | Crear | |
| `tests/Capacitaciones.Tests/NotificacionesUseCasesTests.cs` | Crear | |
| `tests/Capacitaciones.Tests/CorreoEndpointsTests.cs` | Crear | Endpoints admin + interno |
| `tests/Capacitaciones.Tests/MailSenderHttpClientTests.cs` | Crear | |

**mail_sender/**

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `config_provider.py` | Crear | Lectura remota con cache y fallback |
| `app.py` | Modificar | Aplica reglas; `/send-test`; `SmtpSettings` |
| `plantillas/prueba_configuracion.html` | Crear | Plantilla del correo de prueba |
| `requirements-dev.txt` | Crear | pytest + httpx |
| `tests/conftest.py`, `tests/test_config_provider.py`, `tests/test_send_mail_rules.py`, `tests/test_send_test.py` | Crear | |
| `Dockerfile` | Modificar | Copia `config_provider.py` |
| `documentacion.md` | Modificar | Sección SMTP |

**Raíz:** `docker-compose.yml`, `.env.example` (Modificar).

**Frontend (`frontend/src/`)**

| Archivo | Acción |
|---|---|
| `services/configuracion.js` | Modificar |
| `pages/configuracion/CorreoPage.jsx` | Crear |
| `pages/configuracion/CorreoServidorTab.jsx` | Crear |
| `pages/configuracion/CorreoNotificacionesTab.jsx` | Crear |
| `App.jsx`, `components/Sidebar/Sidebar.jsx` | Modificar |
| `pages/asistentes/AsistentesPage.jsx`, `AsistentesPage.module.css` | Modificar |

---

### Task 0: Preparar el entorno local

La PC solo tiene .NET SDK 5; el backend es `net8.0`. **Pedir confirmación al usuario antes de instalar.**

- [ ] **Step 1: Instalar .NET 8 SDK y dotnet-ef 8.0.10**

```powershell
winget install --id Microsoft.DotNet.SDK.8 --silent --accept-package-agreements --accept-source-agreements
dotnet tool install --global dotnet-ef --version 8.0.10
```

Verificar: `dotnet --list-sdks` muestra una línea `8.0.x`; `dotnet ef --version` muestra `8.0.10`.

- [ ] **Step 2: Línea base del backend**

Run: `cd backend; dotnet test`
Expected: todos los tests existentes PASS. Si alguno falla ya antes de empezar, anotarlo y avisar al usuario (no arreglarlo en este plan).

- [ ] **Step 3: Entorno Python de mail_sender**

Crear `mail_sender/requirements-dev.txt`:

```text
-r requirements.txt
pytest==8.3.3
httpx==0.27.2
```

```powershell
cd mail_sender
python -m venv .venv
.\.venv\Scripts\pip install -r requirements-dev.txt
```

Agregar `.venv/` a `mail_sender/.gitignore` si no está cubierto por el `.gitignore` raíz (verificar con `git status`).

- [ ] **Step 4: Línea base del frontend**

Run: `cd frontend; npm ci; npm run lint; npm run build`
Expected: build OK. Si `lint` ya falla antes de empezar, anotarlo como estado previo.

- [ ] **Step 5: Commit**

```bash
git add mail_sender/requirements-dev.txt
git commit -m "chore(mail_sender): dependencias de desarrollo para tests"
```

---

### Task 1: Cifrado de secretos (AES-GCM)

**Files:**
- Create: `backend/src/Capacitaciones.Application/Ports/ISecretProtector.cs`
- Create: `backend/src/Capacitaciones.Infrastructure/Security/AesGcmSecretProtector.cs`
- Test: `backend/tests/Capacitaciones.Tests/AesGcmSecretProtectorTests.cs`

**Interfaces:**
- Produces: `ISecretProtector { bool IsConfigured; string Protect(string plain); string? TryUnprotect(string cipher); }`, `AesGcmSecretProtector(string? base64Key)`.

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
using System.Security.Cryptography;
using Capacitaciones.Infrastructure.Security;

namespace Capacitaciones.Tests;

public class AesGcmSecretProtectorTests
{
    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Protect_Unprotect_IdaYVuelta()
    {
        var p = new AesGcmSecretProtector(NewKey());
        var cipher = p.Protect("S3cr3t*ñ");
        Assert.NotEqual("S3cr3t*ñ", cipher);
        Assert.Equal("S3cr3t*ñ", p.TryUnprotect(cipher));
    }

    [Fact]
    public void Protect_MismoTexto_GeneraCifradosDistintos()
    {
        var p = new AesGcmSecretProtector(NewKey());
        Assert.NotEqual(p.Protect("abc"), p.Protect("abc"));
    }

    [Fact]
    public void TryUnprotect_ConOtraLlave_DevuelveNull()
    {
        var cipher = new AesGcmSecretProtector(NewKey()).Protect("abc");
        Assert.Null(new AesGcmSecretProtector(NewKey()).TryUnprotect(cipher));
    }

    [Fact]
    public void TryUnprotect_TextoBasura_DevuelveNull()
    {
        var p = new AesGcmSecretProtector(NewKey());
        Assert.Null(p.TryUnprotect("no-es-base64!!"));
        Assert.Null(p.TryUnprotect(Convert.ToBase64String(new byte[5])));
    }

    [Fact]
    public void SinLlave_NoConfigurado_ProtectLanza_TryUnprotectNull()
    {
        var p = new AesGcmSecretProtector(null);
        Assert.False(p.IsConfigured);
        Assert.Throws<InvalidOperationException>(() => p.Protect("abc"));
        Assert.Null(p.TryUnprotect("abc"));
    }

    [Fact]
    public void LlaveDeLongitudIncorrecta_Lanza()
    {
        Assert.Throws<ArgumentException>(() =>
            new AesGcmSecretProtector(Convert.ToBase64String(new byte[16])));
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `cd backend; dotnet test --filter AesGcmSecretProtectorTests`
Expected: error de compilación, no existe `AesGcmSecretProtector`.

- [ ] **Step 3: Implementar**

`ISecretProtector.cs`:

```csharp
namespace Capacitaciones.Application.Ports;

/// <summary>
/// Cifra/descifra secretos que se guardan en base de datos (ej. la contraseña SMTP de
/// <c>ConfiguracionCorreo</c>). La llave vive fuera de la BD (variable de entorno).
/// </summary>
public interface ISecretProtector
{
    /// <summary>false si no hay llave configurada: <see cref="Protect"/> lanzará.</summary>
    bool IsConfigured { get; }

    /// <summary>Cifra <paramref name="plain"/>. Lanza <see cref="InvalidOperationException"/> sin llave.</summary>
    string Protect(string plain);

    /// <summary>Descifra; devuelve null si no hay llave, el texto está corrupto o se cifró con otra llave.</summary>
    string? TryUnprotect(string cipher);
}
```

`AesGcmSecretProtector.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Infrastructure.Security;

/// <summary>
/// <see cref="ISecretProtector"/> con AES-256-GCM. Formato del texto cifrado:
/// base64( nonce[12] ‖ tag[16] ‖ ciphertext ). La llave (32 bytes en base64) llega de
/// la variable <c>CORREO_ENCRYPTION_KEY</c>.
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[]? _key;

    public AesGcmSecretProtector(string? base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key)) return;
        var key = Convert.FromBase64String(base64Key.Trim());
        if (key.Length != 32)
        {
            throw new ArgumentException("CORREO_ENCRYPTION_KEY debe ser de 32 bytes codificados en base64.");
        }
        _key = key;
    }

    public bool IsConfigured => _key is not null;

    public string Protect(string plain)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("CORREO_ENCRYPTION_KEY no está configurada.");
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plainBytes, cipher, tag);
        }

        var output = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(output);
    }

    public string? TryUnprotect(string cipher)
    {
        if (_key is null || string.IsNullOrWhiteSpace(cipher)) return null;
        try
        {
            var data = Convert.FromBase64String(cipher);
            if (data.Length < NonceSize + TagSize) return null;

            var nonce = data.AsSpan(0, NonceSize);
            var tag = data.AsSpan(NonceSize, TagSize);
            var encrypted = data.AsSpan(NonceSize + TagSize);
            var plain = new byte[encrypted.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, encrypted, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Correr y verificar que pasa**

Run: `dotnet test --filter AesGcmSecretProtectorTests`
Expected: 6 PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Capacitaciones.Application/Ports/ISecretProtector.cs backend/src/Capacitaciones.Infrastructure/Security/AesGcmSecretProtector.cs backend/tests/Capacitaciones.Tests/AesGcmSecretProtectorTests.cs
git commit -m "feat(correo): cifrado AES-GCM para secretos de configuración"
```

---

### Task 2: Entidades, catálogo de avisos, mapeo EF y migración

**Files:**
- Create: `backend/src/Capacitaciones.Domain/Entities/ConfiguracionCorreo.cs`
- Create: `backend/src/Capacitaciones.Domain/Entities/ConfiguracionNotificacion.cs`
- Create: `backend/src/Capacitaciones.Application/UseCases/Configuracion/NotificacionesCatalogo.cs`
- Create: `backend/src/Capacitaciones.Infrastructure/Persistence/Configurations/ConfiguracionCorreoConfiguration.cs`
- Create: `backend/src/Capacitaciones.Infrastructure/Persistence/Configurations/ConfiguracionNotificacionConfiguration.cs`
- Modify: `backend/src/Capacitaciones.Infrastructure/Persistence/AppDbContext.cs:37` (después del DbSet `ConvenioNumeracion`)
- Generate: migración `ConfiguracionCorreo`
- Test: `backend/tests/Capacitaciones.Tests/NotificacionesUseCasesTests.cs` (primer test)

**Interfaces:**
- Produces: entidades `ConfiguracionCorreo` y `ConfiguracionNotificacion` (propiedades abajo); `NotificacionesCatalogo.Todas : IReadOnlyList<NotificacionDefinicion>`, `NotificacionesCatalogo.Buscar(string plantilla) : NotificacionDefinicion?`, `record NotificacionDefinicion(string Plantilla, string Nombre, string AsuntoActual, IReadOnlyList<string> Variables)`; `AppDbContext.ConfiguracionCorreo`, `AppDbContext.ConfiguracionNotificaciones`.

- [ ] **Step 1: Escribir el test que falla**

`NotificacionesUseCasesTests.cs`:

```csharp
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
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test --filter NotificacionesUseCasesTests`
Expected: error de compilación (no existen `NotificacionesCatalogo` ni `ConfiguracionNotificaciones`).

- [ ] **Step 3: Implementar entidades, catálogo y mapeo**

`ConfiguracionCorreo.cs`:

```csharp
namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Fila única (Id = 1) con el servidor SMTP, el remitente y las copias globales de las
/// notificaciones. No se siembra: mientras no exista, mail_sender usa el <c>.env</c>.
/// </summary>
public class ConfiguracionCorreo
{
    public int Id { get; set; } = 1;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;

    /// <summary>Usuario de autenticación. Null = se autentica con <see cref="RemitenteCorreo"/>.</summary>
    public string? SmtpUser { get; set; }

    /// <summary>Contraseña cifrada con <c>ISecretProtector</c>. Null = sin autenticación (relay).</summary>
    public string? SmtpPasswordCifrada { get; set; }

    public bool UsarTls { get; set; } = true;
    public string RemitenteCorreo { get; set; } = string.Empty;
    public string? RemitenteNombre { get; set; }

    /// <summary>Correos separados por coma.</summary>
    public string? CcGlobal { get; set; }

    /// <summary>Correos separados por coma.</summary>
    public string? BccGlobal { get; set; }

    public string ActualizadoPor { get; set; } = string.Empty;
    public DateTime ActualizadoEn { get; set; }
}
```

`ConfiguracionNotificacion.cs`:

```csharp
namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Regla por tipo de notificación (clave = nombre de plantilla de mail_sender):
/// si se envía y con qué asunto.
/// </summary>
public class ConfiguracionNotificacion
{
    public string Plantilla { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    /// <summary>Plantilla Jinja del asunto. Null = asunto original del sistema.</summary>
    public string? AsuntoPersonalizado { get; set; }

    public string? ActualizadoPor { get; set; }
    public DateTime? ActualizadoEn { get; set; }
}
```

`NotificacionesCatalogo.cs`:

```csharp
namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Definición fija de un tipo de notificación existente en el sistema.</summary>
public sealed record NotificacionDefinicion(
    string Plantilla,
    string Nombre,
    string AsuntoActual,
    IReadOnlyList<string> Variables);

/// <summary>
/// Catálogo de las notificaciones que envían el backend y event_monitor. Es la fuente
/// del seed de <c>ConfiguracionNotificacion</c> y de las variables que muestra la UI.
/// Al agregar una plantilla nueva en mail_sender, agregarla aquí y crear una migración.
/// </summary>
public static class NotificacionesCatalogo
{
    /// <summary>Variable disponible en todos los asuntos: el asunto que habría usado el sistema.</summary>
    public const string VariableAsuntoOriginal = "asunto_original";

    public static IReadOnlyList<NotificacionDefinicion> Todas { get; } = new[]
    {
        new NotificacionDefinicion("invitacion_inscripcion", "Invitación de inscripción",
            "{tipo} Creado / {tipo} Actualizado / Invitación a {tipo}: {tema}",
            new[] { "tema", "fecha", "hora", "duracion", "modalidad", "capacitador" }),
        new NotificacionDefinicion("capacitador_descripcion", "Capacitador: cargar información del curso",
            "Cargar información del curso: {tema}",
            new[] { "nombre", "tema", "link" }),
        new NotificacionDefinicion("capacitador_pase_lista", "Capacitador: pase de lista",
            "Pase de lista: {tema}",
            new[] { "nombre", "tema", "link" }),
        new NotificacionDefinicion("responsable_firma", "Responsable: carga de datos y firma",
            "Carga tus datos y firma en CapacitaDOS",
            new[] { "nombre", "link" }),
        new NotificacionDefinicion("registro_asistencia_admin", "Reporte de asistencia al admin",
            "Registro de asistencia: {tema}",
            new[] { "tema", "codigo", "fecha" }),
        new NotificacionDefinicion("certificado_participante", "Certificado al participante",
            "Tu certificado: {tema}",
            new[] { "nombre", "tema", "fecha" }),
        new NotificacionDefinicion("recordatorio_inicio_proximo", "Recordatorio: capacitación por iniciar",
            "Recordatorio: tu capacitación inicia pronto - {tema}",
            new[] { "nombre", "tema", "fecha", "hora", "modalidad" }),
        new NotificacionDefinicion("recordatorio_evento_iniciado", "Aviso: capacitación iniciada",
            "Tu capacitación ya inició: {tema}",
            new[] { "nombre", "tema", "modalidad" }),
        new NotificacionDefinicion("encuesta_satisfaccion", "Encuesta de satisfacción",
            "Cuéntanos tu experiencia: {tema}",
            new[] { "nombre", "tema", "link" }),
    };

    public static NotificacionDefinicion? Buscar(string plantilla) =>
        Todas.FirstOrDefault(d => string.Equals(d.Plantilla, plantilla, StringComparison.Ordinal));
}
```

`ConfiguracionCorreoConfiguration.cs`:

```csharp
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

public class ConfiguracionCorreoConfiguration : IEntityTypeConfiguration<ConfiguracionCorreo>
{
    public void Configure(EntityTypeBuilder<ConfiguracionCorreo> builder)
    {
        builder.ToTable("ConfiguracionCorreo", "dbo");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.SmtpHost).HasMaxLength(255).IsRequired();
        builder.Property(c => c.SmtpPort).IsRequired();
        builder.Property(c => c.SmtpUser).HasMaxLength(255);
        builder.Property(c => c.SmtpPasswordCifrada).HasMaxLength(1024);
        builder.Property(c => c.UsarTls).IsRequired().HasDefaultValue(true);
        builder.Property(c => c.RemitenteCorreo).HasMaxLength(255).IsRequired();
        builder.Property(c => c.RemitenteNombre).HasMaxLength(255);
        builder.Property(c => c.CcGlobal).HasMaxLength(1000);
        builder.Property(c => c.BccGlobal).HasMaxLength(1000);
        builder.Property(c => c.ActualizadoPor).HasMaxLength(255).IsRequired();
        builder.Property(c => c.ActualizadoEn).IsRequired();
    }
}
```

`ConfiguracionNotificacionConfiguration.cs`:

```csharp
using Capacitaciones.Application.UseCases.Configuracion;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

public class ConfiguracionNotificacionConfiguration : IEntityTypeConfiguration<ConfiguracionNotificacion>
{
    public void Configure(EntityTypeBuilder<ConfiguracionNotificacion> builder)
    {
        builder.ToTable("ConfiguracionNotificacion", "dbo");
        builder.HasKey(n => n.Plantilla);

        builder.Property(n => n.Plantilla).HasMaxLength(100);
        builder.Property(n => n.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Activo).IsRequired();
        builder.Property(n => n.AsuntoPersonalizado).HasMaxLength(500);
        builder.Property(n => n.ActualizadoPor).HasMaxLength(255);
        builder.Property(n => n.ActualizadoEn);

        // Seed: un aviso por plantilla del catálogo, todos activos y con el asunto original.
        builder.HasData(NotificacionesCatalogo.Todas.Select(d => new ConfiguracionNotificacion
        {
            Plantilla = d.Plantilla,
            Nombre = d.Nombre,
            Activo = true,
            AsuntoPersonalizado = null,
            ActualizadoPor = null,
            ActualizadoEn = null
        }));
    }
}
```

En `AppDbContext.cs`, después de `public DbSet<ConvenioNumeracion> ConvenioNumeracion => Set<ConvenioNumeracion>();`:

```csharp
    public DbSet<ConfiguracionCorreo> ConfiguracionCorreo => Set<ConfiguracionCorreo>();
    public DbSet<ConfiguracionNotificacion> ConfiguracionNotificaciones => Set<ConfiguracionNotificacion>();
```

- [ ] **Step 4: Correr el test**

Run: `dotnet test --filter NotificacionesUseCasesTests`
Expected: PASS.

- [ ] **Step 5: Generar la migración**

```powershell
cd backend
dotnet ef migrations add ConfiguracionCorreo --project src/Capacitaciones.Infrastructure --startup-project src/Capacitaciones.Api --output-dir Persistence/Migrations
```

Revisar el `.cs` generado. Debe contener **solo** `CreateTable` de `ConfiguracionCorreo` y `ConfiguracionNotificacion` más `InsertData` con las 9 filas. Si aparece cualquier `AlterColumn`/`DropColumn` sobre tablas existentes, detenerse: el snapshot está desalineado con la BD y hay que investigar antes de seguir.

- [ ] **Step 6: Suite completa y commit**

Run: `dotnet test` → Expected: todo PASS.

```bash
git add backend/src
git add backend/tests/Capacitaciones.Tests/NotificacionesUseCasesTests.cs
git commit -m "feat(correo): entidades y migración de configuración de correo y notificaciones"
```

---

### Task 3: Repositorios, DTOs, validación y casos de uso de configuración SMTP

**Files:**
- Create: `backend/src/Capacitaciones.Application/Ports/IConfiguracionCorreoRepository.cs`
- Create: `backend/src/Capacitaciones.Application/Ports/IConfiguracionNotificacionRepository.cs`
- Create: `backend/src/Capacitaciones.Infrastructure/Persistence/Repositories/ConfiguracionCorreoRepository.cs`
- Create: `backend/src/Capacitaciones.Infrastructure/Persistence/Repositories/ConfiguracionNotificacionRepository.cs`
- Create: `backend/src/Capacitaciones.Application/Dtos/Configuracion/ConfiguracionCorreoDtos.cs`
- Create: `backend/src/Capacitaciones.Application/UseCases/Configuracion/ConfiguracionCorreoException.cs`
- Create: `backend/src/Capacitaciones.Application/UseCases/Configuracion/ConfiguracionCorreoValidator.cs`
- Create: `backend/src/Capacitaciones.Application/UseCases/Configuracion/ObtenerConfiguracionCorreoUseCase.cs`
- Create: `backend/src/Capacitaciones.Application/UseCases/Configuracion/ActualizarConfiguracionCorreoUseCase.cs`
- Create: `backend/tests/Capacitaciones.Tests/Fakes/CorreoFakes.cs`
- Test: `backend/tests/Capacitaciones.Tests/ConfiguracionCorreoUseCasesTests.cs`

**Interfaces:**
- Consumes: `ISecretProtector` (Task 1), entidades (Task 2).
- Produces:
  - `IConfiguracionCorreoRepository { Task<ConfiguracionCorreo?> GetAsync(CancellationToken ct = default); Task UpsertAsync(ConfiguracionCorreo entity, CancellationToken ct = default); }`
  - `IConfiguracionNotificacionRepository { Task<List<ConfiguracionNotificacion>> ListAsync(CancellationToken ct = default); Task SaveChangesAsync(CancellationToken ct = default); }`
  - DTOs `ConfiguracionCorreoDto`, `UpdateConfiguracionCorreoDto`, `NotificacionDto`, `UpdateNotificacionDto`, `CorreoConfigInternaDto`, `SmtpInternoDto`, `NotificacionReglaDto`, `CorreoPruebaResultadoDto`.
  - `ConfiguracionCorreoValidator.Validar(UpdateConfiguracionCorreoDto) : Dictionary<string,string>`, `ConfiguracionCorreoValidator.ParsearLista(string?) : List<string>`, `ConfiguracionCorreoValidator.Normalizar(string?) : string?`.
  - `ConfiguracionCorreoException(string codigo, string message, IReadOnlyDictionary<string,string>? errores = null)` con `Codigo` y `Errores`.
  - `ObtenerConfiguracionCorreoUseCase.ExecuteAsync(ct) : Task<ConfiguracionCorreoDto>`.
  - `ActualizarConfiguracionCorreoUseCase.ExecuteAsync(UpdateConfiguracionCorreoDto input, string adminEmail, ct) : Task<ConfiguracionCorreoDto>`.
  - Fakes: `InMemoryConfiguracionCorreoRepository`, `InMemoryConfiguracionNotificacionRepository`, `FakeMailSenderClient`.

- [ ] **Step 1: Crear DTOs, puertos, excepción y fakes (sin lógica)**

`ConfiguracionCorreoDtos.cs`:

```csharp
namespace Capacitaciones.Application.Dtos.Configuracion;

/// <summary>Respuesta de <c>GET/PUT /api/configuracion/correo</c>. Nunca incluye la contraseña.</summary>
public class ConfiguracionCorreoDto
{
    /// <summary>false = no hay fila guardada; mail_sender está usando el .env.</summary>
    public bool Configurado { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public bool TienePassword { get; set; }

    /// <summary>true = hay contraseña guardada pero no se puede descifrar (llave cambió o se perdió).</summary>
    public bool PasswordInvalida { get; set; }
    public bool UsarTls { get; set; } = true;
    public string RemitenteCorreo { get; set; } = string.Empty;
    public string? RemitenteNombre { get; set; }
    public string? CcGlobal { get; set; }
    public string? BccGlobal { get; set; }
    public string? ActualizadoPor { get; set; }
    public DateTime? ActualizadoEn { get; set; }
}

/// <summary>Payload de <c>PUT /api/configuracion/correo</c> y de <c>POST .../prueba</c>.</summary>
public class UpdateConfiguracionCorreoDto
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string? SmtpUser { get; set; }

    /// <summary>Vacío o null = conservar la contraseña guardada.</summary>
    public string? Password { get; set; }

    /// <summary>true = borrar la contraseña guardada (modo relay sin autenticación).</summary>
    public bool QuitarPassword { get; set; }
    public bool UsarTls { get; set; } = true;
    public string RemitenteCorreo { get; set; } = string.Empty;
    public string? RemitenteNombre { get; set; }
    public string? CcGlobal { get; set; }
    public string? BccGlobal { get; set; }
}

/// <summary>Elemento de <c>GET /api/configuracion/correo/notificaciones</c>.</summary>
public class NotificacionDto
{
    public string Plantilla { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string? AsuntoPersonalizado { get; set; }
    public string AsuntoActual { get; set; } = string.Empty;
    public List<string> Variables { get; set; } = new();
}

/// <summary>Elemento del payload de <c>PUT /api/configuracion/correo/notificaciones</c>.</summary>
public class UpdateNotificacionDto
{
    public string Plantilla { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string? AsuntoPersonalizado { get; set; }
}

/// <summary>Respuesta de <c>POST /api/configuracion/correo/prueba</c>.</summary>
public class CorreoPruebaResultadoDto
{
    public bool Ok { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

/// <summary>Respuesta de <c>GET /api/internal/correo-config</c> (solo para mail_sender).</summary>
public class CorreoConfigInternaDto
{
    public bool Configurado { get; set; }
    public bool PasswordInvalida { get; set; }
    public SmtpInternoDto? Smtp { get; set; }
    public List<string> CcGlobal { get; set; } = new();
    public List<string> BccGlobal { get; set; } = new();
    public Dictionary<string, NotificacionReglaDto> Notificaciones { get; set; } = new();
}

/// <summary>Configuración SMTP con la contraseña en claro. Mismo contrato que <c>SmtpSettings</c> de mail_sender.</summary>
public class SmtpInternoDto
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; }
    public string From { get; set; } = string.Empty;
    public string? FromName { get; set; }
}

public class NotificacionReglaDto
{
    public bool Activo { get; set; }
    public string? Asunto { get; set; }
}
```

`IConfiguracionCorreoRepository.cs`:

```csharp
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>Puerto de la fila única <see cref="ConfiguracionCorreo"/> (Id = 1). No hay seed: puede no existir.</summary>
public interface IConfiguracionCorreoRepository
{
    Task<ConfiguracionCorreo?> GetAsync(CancellationToken ct = default);

    /// <summary>Inserta la fila si no existe; si existe, guarda los cambios.</summary>
    Task UpsertAsync(ConfiguracionCorreo entity, CancellationToken ct = default);
}
```

`IConfiguracionNotificacionRepository.cs`:

```csharp
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>Puerto de <see cref="ConfiguracionNotificacion"/>. Las filas vienen del seed; no se crean ni borran.</summary>
public interface IConfiguracionNotificacionRepository
{
    /// <summary>Devuelve las entidades rastreadas: modificarlas y llamar <see cref="SaveChangesAsync"/>.</summary>
    Task<List<ConfiguracionNotificacion>> ListAsync(CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
```

`ConfiguracionCorreoException.cs`:

```csharp
namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Error de validación/negocio del módulo de correo. <see cref="Errores"/> = mensaje por campo.</summary>
public class ConfiguracionCorreoException : Exception
{
    public string Codigo { get; }
    public IReadOnlyDictionary<string, string> Errores { get; }

    public ConfiguracionCorreoException(string codigo, string message, IReadOnlyDictionary<string, string>? errores = null)
        : base(message)
    {
        Codigo = codigo;
        Errores = errores ?? new Dictionary<string, string>();
    }
}
```

`tests/Capacitaciones.Tests/Fakes/CorreoFakes.cs`:

```csharp
using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Configuracion;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests.Fakes;

internal sealed class InMemoryConfiguracionCorreoRepository : IConfiguracionCorreoRepository
{
    public ConfiguracionCorreo? Current { get; set; }

    public Task<ConfiguracionCorreo?> GetAsync(CancellationToken ct = default) => Task.FromResult(Current);

    public Task UpsertAsync(ConfiguracionCorreo entity, CancellationToken ct = default)
    {
        Current = entity;
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryConfiguracionNotificacionRepository : IConfiguracionNotificacionRepository
{
    public List<ConfiguracionNotificacion> Items { get; } = NotificacionesCatalogo.Todas
        .Select(d => new ConfiguracionNotificacion { Plantilla = d.Plantilla, Nombre = d.Nombre, Activo = true })
        .ToList();

    public int SaveCount { get; private set; }

    public Task<List<ConfiguracionNotificacion>> ListAsync(CancellationToken ct = default) => Task.FromResult(Items);

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
```

> `FakeMailSenderClient` se agrega a este archivo en la Task 5, cuando exista `SendTestAsync`.

- [ ] **Step 2: Escribir los tests que fallan**

`ConfiguracionCorreoUseCasesTests.cs`:

```csharp
using System.Security.Cryptography;
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.UseCases.Configuracion;
using Capacitaciones.Infrastructure.Security;
using Capacitaciones.Tests.Fakes;

namespace Capacitaciones.Tests;

public class ConfiguracionCorreoUseCasesTests
{
    private const string Admin = "admin@dos.com.ec";

    private static AesGcmSecretProtector NewProtector() =>
        new(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    private static UpdateConfiguracionCorreoDto ValidInput() => new()
    {
        SmtpHost = "smtp.office365.com",
        SmtpPort = 587,
        SmtpUser = null,
        Password = "Clave123*",
        UsarTls = true,
        RemitenteCorreo = "capacitaciones@dos.com.ec",
        RemitenteNombre = "CapacitaDOS",
        CcGlobal = "a@dos.com.ec; b@dos.com.ec",
        BccGlobal = null
    };

    [Fact]
    public async Task Obtener_SinFila_DevuelveNoConfiguradoConDefaults()
    {
        var uc = new ObtenerConfiguracionCorreoUseCase(new InMemoryConfiguracionCorreoRepository(), NewProtector());
        var dto = await uc.ExecuteAsync();

        Assert.False(dto.Configurado);
        Assert.Equal(587, dto.SmtpPort);
        Assert.True(dto.UsarTls);
        Assert.False(dto.TienePassword);
    }

    [Fact]
    public async Task Actualizar_Valido_GuardaCifradoYNormalizaListas()
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var protector = NewProtector();
        var uc = new ActualizarConfiguracionCorreoUseCase(repo, protector);

        var dto = await uc.ExecuteAsync(ValidInput(), Admin);

        Assert.True(dto.Configurado);
        Assert.True(dto.TienePassword);
        Assert.Equal("a@dos.com.ec, b@dos.com.ec", dto.CcGlobal);
        Assert.NotNull(repo.Current);
        Assert.NotEqual("Clave123*", repo.Current!.SmtpPasswordCifrada);
        Assert.Equal("Clave123*", protector.TryUnprotect(repo.Current.SmtpPasswordCifrada!));
        Assert.Equal(Admin, repo.Current.ActualizadoPor);
    }

    [Fact]
    public async Task Actualizar_SinPassword_ConservaLaGuardada()
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var protector = NewProtector();
        var uc = new ActualizarConfiguracionCorreoUseCase(repo, protector);
        await uc.ExecuteAsync(ValidInput(), Admin);
        var cifradaAntes = repo.Current!.SmtpPasswordCifrada;

        var input = ValidInput();
        input.Password = "";
        input.RemitenteNombre = "Otro nombre";
        await uc.ExecuteAsync(input, Admin);

        Assert.Equal(cifradaAntes, repo.Current!.SmtpPasswordCifrada);
        Assert.Equal("Otro nombre", repo.Current.RemitenteNombre);
    }

    [Fact]
    public async Task Actualizar_QuitarPassword_LaBorra()
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var uc = new ActualizarConfiguracionCorreoUseCase(repo, NewProtector());
        await uc.ExecuteAsync(ValidInput(), Admin);

        var input = ValidInput();
        input.Password = null;
        input.QuitarPassword = true;
        var dto = await uc.ExecuteAsync(input, Admin);

        Assert.Null(repo.Current!.SmtpPasswordCifrada);
        Assert.False(dto.TienePassword);
    }

    [Theory]
    [InlineData("SmtpHost", "", 587, "capacitaciones@dos.com.ec", null)]
    [InlineData("SmtpPort", "smtp.x.com", 0, "capacitaciones@dos.com.ec", null)]
    [InlineData("SmtpPort", "smtp.x.com", 70000, "capacitaciones@dos.com.ec", null)]
    [InlineData("RemitenteCorreo", "smtp.x.com", 587, "no-es-correo", null)]
    [InlineData("CcGlobal", "smtp.x.com", 587, "capacitaciones@dos.com.ec", "ok@dos.com.ec, malo")]
    public async Task Actualizar_Invalido_LanzaConErrorDelCampo(
        string campo, string host, int port, string remitente, string? cc)
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var uc = new ActualizarConfiguracionCorreoUseCase(repo, NewProtector());
        var input = ValidInput();
        input.SmtpHost = host;
        input.SmtpPort = port;
        input.RemitenteCorreo = remitente;
        input.CcGlobal = cc;

        var ex = await Assert.ThrowsAsync<ConfiguracionCorreoException>(() => uc.ExecuteAsync(input, Admin));

        Assert.Equal("VALIDACION", ex.Codigo);
        Assert.True(ex.Errores.ContainsKey(campo));
        Assert.Null(repo.Current);
    }

    [Fact]
    public async Task Actualizar_MasDe20Copias_Lanza()
    {
        var uc = new ActualizarConfiguracionCorreoUseCase(new InMemoryConfiguracionCorreoRepository(), NewProtector());
        var input = ValidInput();
        input.BccGlobal = string.Join(",", Enumerable.Range(1, 21).Select(i => $"u{i}@dos.com.ec"));

        var ex = await Assert.ThrowsAsync<ConfiguracionCorreoException>(() => uc.ExecuteAsync(input, Admin));
        Assert.True(ex.Errores.ContainsKey("BccGlobal"));
    }

    [Fact]
    public async Task Actualizar_PasswordNuevaSinLlave_LanzaEncryptionKeyMissing()
    {
        var uc = new ActualizarConfiguracionCorreoUseCase(
            new InMemoryConfiguracionCorreoRepository(), new AesGcmSecretProtector(null));

        var ex = await Assert.ThrowsAsync<ConfiguracionCorreoException>(() => uc.ExecuteAsync(ValidInput(), Admin));
        Assert.Equal("ENCRYPTION_KEY_MISSING", ex.Codigo);
    }

    [Fact]
    public async Task Obtener_PasswordCifradaConOtraLlave_MarcaPasswordInvalida()
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        await new ActualizarConfiguracionCorreoUseCase(repo, NewProtector()).ExecuteAsync(ValidInput(), Admin);

        var dto = await new ObtenerConfiguracionCorreoUseCase(repo, NewProtector()).ExecuteAsync();

        Assert.True(dto.TienePassword);
        Assert.True(dto.PasswordInvalida);
    }
}
```

- [ ] **Step 3: Correr y verificar que falla**

Run: `dotnet test --filter ConfiguracionCorreoUseCasesTests`
Expected: error de compilación, no existen los casos de uso ni el validador.

- [ ] **Step 4: Implementar validador y casos de uso**

`ConfiguracionCorreoValidator.cs`:

```csharp
using System.Net.Mail;
using Capacitaciones.Application.Dtos.Configuracion;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Validación y normalización de <see cref="UpdateConfiguracionCorreoDto"/>.</summary>
public static class ConfiguracionCorreoValidator
{
    public const int MaxCopias = 20;

    /// <summary>Devuelve los errores por campo (nombre de propiedad del DTO). Vacío = válido.</summary>
    public static Dictionary<string, string> Validar(UpdateConfiguracionCorreoDto input)
    {
        var errores = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(input.SmtpHost))
            errores[nameof(input.SmtpHost)] = "El servidor SMTP es obligatorio.";
        if (input.SmtpPort < 1 || input.SmtpPort > 65535)
            errores[nameof(input.SmtpPort)] = "El puerto debe estar entre 1 y 65535.";
        if (!EsEmail(input.RemitenteCorreo))
            errores[nameof(input.RemitenteCorreo)] = "Ingresa un correo de remitente válido.";
        if (!string.IsNullOrWhiteSpace(input.SmtpUser) && input.SmtpUser.Trim().Length > 255)
            errores[nameof(input.SmtpUser)] = "El usuario no puede superar 255 caracteres.";
        if (!string.IsNullOrWhiteSpace(input.RemitenteNombre) && input.RemitenteNombre.Trim().Length > 255)
            errores[nameof(input.RemitenteNombre)] = "El nombre no puede superar 255 caracteres.";

        ValidarLista(input.CcGlobal, nameof(input.CcGlobal), errores);
        ValidarLista(input.BccGlobal, nameof(input.BccGlobal), errores);
        return errores;
    }

    /// <summary>Separa por coma o punto y coma, recorta y descarta vacíos.</summary>
    public static List<string> ParsearLista(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? new List<string>()
            : valor.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

    /// <summary>Lista normalizada como texto "a@x, b@x"; null si queda vacía.</summary>
    public static string? Normalizar(string? valor)
    {
        var items = ParsearLista(valor);
        return items.Count == 0 ? null : string.Join(", ", items);
    }

    public static bool EsEmail(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return false;
        var trimmed = valor.Trim();
        return MailAddress.TryCreate(trimmed, out var addr) && addr.Address == trimmed;
    }

    private static void ValidarLista(string? valor, string campo, Dictionary<string, string> errores)
    {
        var items = ParsearLista(valor);
        if (items.Count > MaxCopias)
        {
            errores[campo] = $"Máximo {MaxCopias} direcciones.";
            return;
        }
        var invalido = items.FirstOrDefault(i => !EsEmail(i));
        if (invalido is not null)
        {
            errores[campo] = $"'{invalido}' no es un correo válido.";
        }
        else if (Normalizar(valor)?.Length > 1000)
        {
            errores[campo] = "La lista no puede superar 1000 caracteres.";
        }
    }
}
```

`ObtenerConfiguracionCorreoUseCase.cs`:

```csharp
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Caso de uso: leer la configuración SMTP para la pantalla de administración.</summary>
public class ObtenerConfiguracionCorreoUseCase
{
    private readonly IConfiguracionCorreoRepository _repo;
    private readonly ISecretProtector _protector;

    public ObtenerConfiguracionCorreoUseCase(IConfiguracionCorreoRepository repo, ISecretProtector protector)
    {
        _repo = repo;
        _protector = protector;
    }

    public async Task<ConfiguracionCorreoDto> ExecuteAsync(CancellationToken ct = default)
    {
        var cfg = await _repo.GetAsync(ct);
        return ToDto(cfg, _protector);
    }

    internal static ConfiguracionCorreoDto ToDto(ConfiguracionCorreo? cfg, ISecretProtector protector)
    {
        if (cfg is null) return new ConfiguracionCorreoDto { Configurado = false };

        var tienePassword = !string.IsNullOrEmpty(cfg.SmtpPasswordCifrada);
        return new ConfiguracionCorreoDto
        {
            Configurado = true,
            SmtpHost = cfg.SmtpHost,
            SmtpPort = cfg.SmtpPort,
            SmtpUser = cfg.SmtpUser,
            TienePassword = tienePassword,
            PasswordInvalida = tienePassword && protector.TryUnprotect(cfg.SmtpPasswordCifrada!) is null,
            UsarTls = cfg.UsarTls,
            RemitenteCorreo = cfg.RemitenteCorreo,
            RemitenteNombre = cfg.RemitenteNombre,
            CcGlobal = cfg.CcGlobal,
            BccGlobal = cfg.BccGlobal,
            ActualizadoPor = cfg.ActualizadoPor,
            ActualizadoEn = cfg.ActualizadoEn
        };
    }
}
```

`ActualizarConfiguracionCorreoUseCase.cs`:

```csharp
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Caso de uso: guardar la configuración SMTP / remitente / copias globales.</summary>
public class ActualizarConfiguracionCorreoUseCase
{
    private readonly IConfiguracionCorreoRepository _repo;
    private readonly ISecretProtector _protector;

    public ActualizarConfiguracionCorreoUseCase(IConfiguracionCorreoRepository repo, ISecretProtector protector)
    {
        _repo = repo;
        _protector = protector;
    }

    public async Task<ConfiguracionCorreoDto> ExecuteAsync(
        UpdateConfiguracionCorreoDto input, string adminEmail, CancellationToken ct = default)
    {
        if (input is null) throw new ConfiguracionCorreoException("INVALID_INPUT", "Payload requerido.");

        var errores = ConfiguracionCorreoValidator.Validar(input);
        if (errores.Count > 0)
        {
            throw new ConfiguracionCorreoException("VALIDACION", "Revisa los campos marcados.", errores);
        }

        var passwordNueva = string.IsNullOrEmpty(input.Password) ? null : input.Password;
        if (passwordNueva is not null && !input.QuitarPassword && !_protector.IsConfigured)
        {
            throw new ConfiguracionCorreoException(
                "ENCRYPTION_KEY_MISSING",
                "El servidor no tiene configurada la llave de cifrado (CORREO_ENCRYPTION_KEY). Contacta al administrador del sistema.");
        }

        var cfg = await _repo.GetAsync(ct) ?? new ConfiguracionCorreo { Id = 1 };
        cfg.SmtpHost = input.SmtpHost.Trim();
        cfg.SmtpPort = input.SmtpPort;
        cfg.SmtpUser = string.IsNullOrWhiteSpace(input.SmtpUser) ? null : input.SmtpUser.Trim();
        cfg.UsarTls = input.UsarTls;
        cfg.RemitenteCorreo = input.RemitenteCorreo.Trim();
        cfg.RemitenteNombre = string.IsNullOrWhiteSpace(input.RemitenteNombre) ? null : input.RemitenteNombre.Trim();
        cfg.CcGlobal = ConfiguracionCorreoValidator.Normalizar(input.CcGlobal);
        cfg.BccGlobal = ConfiguracionCorreoValidator.Normalizar(input.BccGlobal);

        if (input.QuitarPassword)
        {
            cfg.SmtpPasswordCifrada = null;
        }
        else if (passwordNueva is not null)
        {
            cfg.SmtpPasswordCifrada = _protector.Protect(passwordNueva);
        }

        cfg.ActualizadoPor = adminEmail;
        cfg.ActualizadoEn = DateTime.UtcNow;
        await _repo.UpsertAsync(cfg, ct);

        return ObtenerConfiguracionCorreoUseCase.ToDto(cfg, _protector);
    }
}
```

Repositorios en Infrastructure:

`ConfiguracionCorreoRepository.cs`:

```csharp
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

public class ConfiguracionCorreoRepository : IConfiguracionCorreoRepository
{
    private readonly AppDbContext _db;

    public ConfiguracionCorreoRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<ConfiguracionCorreo?> GetAsync(CancellationToken ct = default) =>
        _db.ConfiguracionCorreo.FirstOrDefaultAsync(c => c.Id == 1, ct);

    public async Task UpsertAsync(ConfiguracionCorreo entity, CancellationToken ct = default)
    {
        if (_db.Entry(entity).State == EntityState.Detached)
        {
            await _db.ConfiguracionCorreo.AddAsync(entity, ct);
        }
        await _db.SaveChangesAsync(ct);
    }
}
```

`ConfiguracionNotificacionRepository.cs`:

```csharp
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

public class ConfiguracionNotificacionRepository : IConfiguracionNotificacionRepository
{
    private readonly AppDbContext _db;

    public ConfiguracionNotificacionRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<ConfiguracionNotificacion>> ListAsync(CancellationToken ct = default) =>
        _db.ConfiguracionNotificaciones.ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
```

- [ ] **Step 5: Correr y verificar que pasa**

Run: `dotnet test --filter ConfiguracionCorreoUseCasesTests`
Expected: todos PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(correo): casos de uso para leer y guardar la configuración SMTP"
```

---

### Task 4: Casos de uso de notificaciones y configuración interna

**Files:**
- Create: `backend/src/Capacitaciones.Application/UseCases/Configuracion/ListarNotificacionesUseCase.cs`
- Create: `backend/src/Capacitaciones.Application/UseCases/Configuracion/ActualizarNotificacionesUseCase.cs`
- Create: `backend/src/Capacitaciones.Application/UseCases/Configuracion/ObtenerConfiguracionCorreoInternaUseCase.cs`
- Test: `backend/tests/Capacitaciones.Tests/NotificacionesUseCasesTests.cs` (agregar tests)

**Interfaces:**
- Consumes: puertos, DTOs, fakes y catálogo (Tasks 2–3).
- Produces:
  - `ListarNotificacionesUseCase.ExecuteAsync(ct) : Task<List<NotificacionDto>>`
  - `ActualizarNotificacionesUseCase.ExecuteAsync(List<UpdateNotificacionDto> input, string adminEmail, ct) : Task<List<NotificacionDto>>`
  - `ObtenerConfiguracionCorreoInternaUseCase.ExecuteAsync(ct) : Task<CorreoConfigInternaDto>`

- [ ] **Step 1: Escribir los tests que fallan**

Agregar al final de `NotificacionesUseCasesTests` (y agregar `using Capacitaciones.Application.Dtos.Configuracion;`, `using Capacitaciones.Tests.Fakes;`, `using Capacitaciones.Infrastructure.Security;`, `using System.Security.Cryptography;`, `using Capacitaciones.Domain.Entities;`):

```csharp
    [Fact]
    public async Task Listar_DevuelveCatalogoConVariablesYAsuntoOriginal()
    {
        var uc = new ListarNotificacionesUseCase(new InMemoryConfiguracionNotificacionRepository());
        var lista = await uc.ExecuteAsync();

        Assert.Equal(9, lista.Count);
        Assert.Equal("invitacion_inscripcion", lista[0].Plantilla);
        var cert = lista.Single(n => n.Plantilla == "certificado_participante");
        Assert.Equal("Tu certificado: {tema}", cert.AsuntoActual);
        Assert.Contains("tema", cert.Variables);
        Assert.Contains("asunto_original", cert.Variables);
    }

    [Fact]
    public async Task Actualizar_CambiaSoloLasEnviadasYRecortaAsunto()
    {
        var repo = new InMemoryConfiguracionNotificacionRepository();
        var uc = new ActualizarNotificacionesUseCase(repo);

        await uc.ExecuteAsync(new List<UpdateNotificacionDto>
        {
            new() { Plantilla = "encuesta_satisfaccion", Activo = false, AsuntoPersonalizado = "  " },
            new() { Plantilla = "certificado_participante", Activo = true, AsuntoPersonalizado = "  [DOS] {{ tema }} " }
        }, "admin@dos.com.ec");

        var encuesta = repo.Items.Single(i => i.Plantilla == "encuesta_satisfaccion");
        Assert.False(encuesta.Activo);
        Assert.Null(encuesta.AsuntoPersonalizado);
        Assert.Equal("admin@dos.com.ec", encuesta.ActualizadoPor);

        var cert = repo.Items.Single(i => i.Plantilla == "certificado_participante");
        Assert.Equal("[DOS] {{ tema }}", cert.AsuntoPersonalizado);

        Assert.True(repo.Items.Single(i => i.Plantilla == "responsable_firma").Activo);
        Assert.Null(repo.Items.Single(i => i.Plantilla == "responsable_firma").ActualizadoPor);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Actualizar_PlantillaDesconocida_LanzaSinGuardar()
    {
        var repo = new InMemoryConfiguracionNotificacionRepository();
        var uc = new ActualizarNotificacionesUseCase(repo);

        var ex = await Assert.ThrowsAsync<ConfiguracionCorreoException>(() => uc.ExecuteAsync(
            new List<UpdateNotificacionDto> { new() { Plantilla = "no_existe", Activo = true } }, "a@dos.com.ec"));

        Assert.Equal("PLANTILLA_DESCONOCIDA", ex.Codigo);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Actualizar_AsuntoMuyLargo_Lanza()
    {
        var uc = new ActualizarNotificacionesUseCase(new InMemoryConfiguracionNotificacionRepository());

        var ex = await Assert.ThrowsAsync<ConfiguracionCorreoException>(() => uc.ExecuteAsync(
            new List<UpdateNotificacionDto>
            {
                new() { Plantilla = "encuesta_satisfaccion", Activo = true, AsuntoPersonalizado = new string('x', 501) }
            }, "a@dos.com.ec"));

        Assert.Equal("ASUNTO_MUY_LARGO", ex.Codigo);
    }

    [Fact]
    public async Task Interna_SinConfiguracion_DevuelveReglasSinSmtp()
    {
        var notif = new InMemoryConfiguracionNotificacionRepository();
        notif.Items.Single(i => i.Plantilla == "encuesta_satisfaccion").Activo = false;
        var uc = new ObtenerConfiguracionCorreoInternaUseCase(
            new InMemoryConfiguracionCorreoRepository(), notif,
            new AesGcmSecretProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

        var dto = await uc.ExecuteAsync();

        Assert.False(dto.Configurado);
        Assert.Null(dto.Smtp);
        Assert.Equal(9, dto.Notificaciones.Count);
        Assert.False(dto.Notificaciones["encuesta_satisfaccion"].Activo);
    }

    [Fact]
    public async Task Interna_Configurada_DescifraPasswordYParseaCopias()
    {
        var protector = new AesGcmSecretProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var repo = new InMemoryConfiguracionCorreoRepository
        {
            Current = new ConfiguracionCorreo
            {
                SmtpHost = "smtp.office365.com", SmtpPort = 587, SmtpUser = null,
                SmtpPasswordCifrada = protector.Protect("Clave123*"), UsarTls = true,
                RemitenteCorreo = "capacitaciones@dos.com.ec", RemitenteNombre = "CapacitaDOS",
                CcGlobal = "a@dos.com.ec, b@dos.com.ec", BccGlobal = null,
                ActualizadoPor = "x", ActualizadoEn = DateTime.UtcNow
            }
        };
        var uc = new ObtenerConfiguracionCorreoInternaUseCase(repo, new InMemoryConfiguracionNotificacionRepository(), protector);

        var dto = await uc.ExecuteAsync();

        Assert.True(dto.Configurado);
        Assert.False(dto.PasswordInvalida);
        Assert.Equal("Clave123*", dto.Smtp!.Password);
        Assert.Equal("capacitaciones@dos.com.ec", dto.Smtp.From);
        Assert.Equal(new[] { "a@dos.com.ec", "b@dos.com.ec" }, dto.CcGlobal);
        Assert.Empty(dto.BccGlobal);
    }

    [Fact]
    public async Task Interna_PasswordIlegible_MarcaPasswordInvalida()
    {
        var repo = new InMemoryConfiguracionCorreoRepository
        {
            Current = new ConfiguracionCorreo
            {
                SmtpHost = "h", SmtpPort = 587, SmtpPasswordCifrada = "basura",
                RemitenteCorreo = "r@dos.com.ec", ActualizadoPor = "x", ActualizadoEn = DateTime.UtcNow
            }
        };
        var uc = new ObtenerConfiguracionCorreoInternaUseCase(
            repo, new InMemoryConfiguracionNotificacionRepository(),
            new AesGcmSecretProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

        var dto = await uc.ExecuteAsync();

        Assert.True(dto.PasswordInvalida);
        Assert.Null(dto.Smtp!.Password);
    }
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test --filter NotificacionesUseCasesTests`
Expected: error de compilación.

- [ ] **Step 3: Implementar**

`ListarNotificacionesUseCase.cs`:

```csharp
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Caso de uso: listar los tipos de notificación con su regla y variables disponibles.</summary>
public class ListarNotificacionesUseCase
{
    private readonly IConfiguracionNotificacionRepository _repo;

    public ListarNotificacionesUseCase(IConfiguracionNotificacionRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<NotificacionDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var filas = await _repo.ListAsync(ct);
        return ToDtos(filas);
    }

    /// <summary>Ordena según el catálogo; plantillas en BD que no están en el catálogo se omiten.</summary>
    internal static List<NotificacionDto> ToDtos(IEnumerable<ConfiguracionNotificacion> filas)
    {
        var porPlantilla = filas.ToDictionary(f => f.Plantilla, StringComparer.Ordinal);
        return NotificacionesCatalogo.Todas
            .Where(d => porPlantilla.ContainsKey(d.Plantilla))
            .Select(d =>
            {
                var f = porPlantilla[d.Plantilla];
                return new NotificacionDto
                {
                    Plantilla = d.Plantilla,
                    Nombre = d.Nombre,
                    Activo = f.Activo,
                    AsuntoPersonalizado = f.AsuntoPersonalizado,
                    AsuntoActual = d.AsuntoActual,
                    Variables = d.Variables.Append(NotificacionesCatalogo.VariableAsuntoOriginal).ToList()
                };
            })
            .ToList();
    }
}
```

`ActualizarNotificacionesUseCase.cs`:

```csharp
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Caso de uso: activar/desactivar notificaciones y fijar asuntos personalizados.</summary>
public class ActualizarNotificacionesUseCase
{
    public const int MaxAsunto = 500;

    private readonly IConfiguracionNotificacionRepository _repo;

    public ActualizarNotificacionesUseCase(IConfiguracionNotificacionRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<NotificacionDto>> ExecuteAsync(
        List<UpdateNotificacionDto> input, string adminEmail, CancellationToken ct = default)
    {
        if (input is null) throw new ConfiguracionCorreoException("INVALID_INPUT", "Payload requerido.");

        // Validar todo antes de tocar entidades: o se aplica el lote completo o nada.
        foreach (var item in input)
        {
            if (NotificacionesCatalogo.Buscar(item.Plantilla) is null)
            {
                throw new ConfiguracionCorreoException(
                    "PLANTILLA_DESCONOCIDA", $"La plantilla '{item.Plantilla}' no existe.");
            }
            if ((item.AsuntoPersonalizado?.Trim().Length ?? 0) > MaxAsunto)
            {
                throw new ConfiguracionCorreoException(
                    "ASUNTO_MUY_LARGO", $"El asunto de '{item.Plantilla}' supera {MaxAsunto} caracteres.");
            }
        }

        var filas = await _repo.ListAsync(ct);
        var ahora = DateTime.UtcNow;
        foreach (var item in input)
        {
            var fila = filas.FirstOrDefault(f => f.Plantilla == item.Plantilla);
            if (fila is null) continue;

            fila.Activo = item.Activo;
            fila.AsuntoPersonalizado = string.IsNullOrWhiteSpace(item.AsuntoPersonalizado)
                ? null
                : item.AsuntoPersonalizado.Trim();
            fila.ActualizadoPor = adminEmail;
            fila.ActualizadoEn = ahora;
        }

        await _repo.SaveChangesAsync(ct);
        return ListarNotificacionesUseCase.ToDtos(filas);
    }
}
```

`ObtenerConfiguracionCorreoInternaUseCase.cs`:

```csharp
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>
/// Caso de uso: configuración completa (con contraseña en claro) para mail_sender.
/// Solo se expone por el endpoint interno protegido con X-Internal-Key.
/// </summary>
public class ObtenerConfiguracionCorreoInternaUseCase
{
    private readonly IConfiguracionCorreoRepository _correo;
    private readonly IConfiguracionNotificacionRepository _notificaciones;
    private readonly ISecretProtector _protector;

    public ObtenerConfiguracionCorreoInternaUseCase(
        IConfiguracionCorreoRepository correo,
        IConfiguracionNotificacionRepository notificaciones,
        ISecretProtector protector)
    {
        _correo = correo;
        _notificaciones = notificaciones;
        _protector = protector;
    }

    public async Task<CorreoConfigInternaDto> ExecuteAsync(CancellationToken ct = default)
    {
        var cfg = await _correo.GetAsync(ct);
        var reglas = await _notificaciones.ListAsync(ct);

        var dto = new CorreoConfigInternaDto
        {
            Notificaciones = reglas.ToDictionary(
                r => r.Plantilla,
                r => new NotificacionReglaDto { Activo = r.Activo, Asunto = r.AsuntoPersonalizado })
        };

        if (cfg is null) return dto;

        string? password = null;
        if (!string.IsNullOrEmpty(cfg.SmtpPasswordCifrada))
        {
            password = _protector.TryUnprotect(cfg.SmtpPasswordCifrada);
            dto.PasswordInvalida = password is null;
        }

        dto.Configurado = true;
        dto.Smtp = new SmtpInternoDto
        {
            Host = cfg.SmtpHost,
            Port = cfg.SmtpPort,
            User = cfg.SmtpUser,
            Password = password,
            UseTls = cfg.UsarTls,
            From = cfg.RemitenteCorreo,
            FromName = cfg.RemitenteNombre
        };
        dto.CcGlobal = ConfiguracionCorreoValidator.ParsearLista(cfg.CcGlobal);
        dto.BccGlobal = ConfiguracionCorreoValidator.ParsearLista(cfg.BccGlobal);
        return dto;
    }
}
```

- [ ] **Step 4: Correr y verificar que pasa**

Run: `dotnet test --filter NotificacionesUseCasesTests`
Expected: 8 PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(correo): casos de uso de notificaciones y configuración interna para mail_sender"
```

---

### Task 5: Cliente de mail_sender (resultado omitido + envío de prueba) y certificados Omitido

**Files:**
- Create: `backend/src/Capacitaciones.Application/Dtos/Notifications/MailSendResult.cs`
- Modify: `backend/src/Capacitaciones.Application/Ports/IMailSenderClient.cs`
- Modify: `backend/src/Capacitaciones.Infrastructure/Services/MailSenderHttpClient.cs`
- Modify: `backend/src/Capacitaciones.Domain/Entities/EstadoEnvioCertificado.cs`
- Modify: `backend/src/Capacitaciones.Application/UseCases/Certificados/GenerarYEnviarCertificadosUseCase.cs:176-188`
- Modify: `backend/tests/Capacitaciones.Tests/Fakes/CorreoFakes.cs` (agregar `FakeMailSenderClient`)
- Test: `backend/tests/Capacitaciones.Tests/MailSenderHttpClientTests.cs`

**Interfaces:**
- Produces:
  - `enum MailSendResult { Enviado, Omitido }`
  - `IMailSenderClient.SendMailAsync(SendMailRequest, CancellationToken) : Task<MailSendResult>` (antes `Task`)
  - `IMailSenderClient.SendTestAsync(SendTestMailRequest, CancellationToken) : Task<MailTestResult>`
  - `class SendTestMailRequest { SmtpInternoDto Smtp; string Recipient; }`, `class MailTestResult { bool Ok; string? Error; }`
  - `EstadoEnvioCertificado.Omitido = 4`
  - `FakeMailSenderClient { SendTestMailRequest? LastTest; MailTestResult NextTestResult; }`

- [ ] **Step 1: Escribir los tests que fallan**

`MailSenderHttpClientTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Infrastructure.Services;

namespace Capacitaciones.Tests;

public class MailSenderHttpClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public string? LastPath { get; private set; }
        public string? LastBody { get; private set; }

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }

    private static (MailSenderHttpClient, StubHandler) Build(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://mail_sender:8000/") };
        return (new MailSenderHttpClient(http), handler);
    }

    private static SendMailRequest Req() => new()
    {
        Template = "certificado_participante", Subject = "s", Recipients = new() { "x@dos.com.ec" }
    };

    [Fact]
    public async Task SendMail_StatusSent_DevuelveEnviado()
    {
        var (client, _) = Build(HttpStatusCode.OK, "{\"status\":\"sent\"}");
        Assert.Equal(MailSendResult.Enviado, await client.SendMailAsync(Req(), CancellationToken.None));
    }

    [Fact]
    public async Task SendMail_StatusOmitido_DevuelveOmitido()
    {
        var (client, _) = Build(HttpStatusCode.OK, "{\"status\":\"omitido\"}");
        Assert.Equal(MailSendResult.Omitido, await client.SendMailAsync(Req(), CancellationToken.None));
    }

    [Fact]
    public async Task SendMail_CuerpoVacio_DevuelveEnviado()
    {
        var (client, _) = Build(HttpStatusCode.OK, "");
        Assert.Equal(MailSendResult.Enviado, await client.SendMailAsync(Req(), CancellationToken.None));
    }

    [Fact]
    public async Task SendMail_502_Lanza()
    {
        var (client, _) = Build(HttpStatusCode.BadGateway, "{\"detail\":\"x\"}");
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendMailAsync(Req(), CancellationToken.None));
    }

    [Fact]
    public async Task SendTest_EnviaContratoCamelCaseYLeeResultado()
    {
        var (client, handler) = Build(HttpStatusCode.OK, "{\"ok\":false,\"error\":\"535 Authentication failed\"}");

        var result = await client.SendTestAsync(new SendTestMailRequest
        {
            Recipient = "admin@dos.com.ec",
            Smtp = new SmtpInternoDto
            {
                Host = "smtp.office365.com", Port = 587, Password = "p", UseTls = true,
                From = "capacitaciones@dos.com.ec", FromName = "CapacitaDOS"
            }
        }, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal("535 Authentication failed", result.Error);
        Assert.Equal("/send-test", handler.LastPath);
        using var doc = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("admin@dos.com.ec", doc.RootElement.GetProperty("recipient").GetString());
        Assert.Equal("capacitaciones@dos.com.ec", doc.RootElement.GetProperty("smtp").GetProperty("from").GetString());
        Assert.True(doc.RootElement.GetProperty("smtp").GetProperty("useTls").GetBoolean());
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test --filter MailSenderHttpClientTests`
Expected: error de compilación.

- [ ] **Step 3: Implementar**

`MailSendResult.cs`:

```csharp
using Capacitaciones.Application.Dtos.Configuracion;

namespace Capacitaciones.Application.Dtos.Notifications;

/// <summary>Resultado de <c>POST /send-mail</c>: <c>Omitido</c> = el aviso está desactivado en Configuración → Correo.</summary>
public enum MailSendResult
{
    Enviado,
    Omitido
}

/// <summary>Payload de <c>POST /send-test</c> de mail_sender.</summary>
public class SendTestMailRequest
{
    public SmtpInternoDto Smtp { get; set; } = new();
    public string Recipient { get; set; } = string.Empty;
}

/// <summary>Respuesta de <c>POST /send-test</c>. <c>Error</c> trae el mensaje del servidor SMTP.</summary>
public class MailTestResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
}
```

`IMailSenderClient.cs` — reemplazar el cuerpo de la interfaz:

```csharp
public interface IMailSenderClient
{
    /// <summary>
    /// Invoca <c>POST /send-mail</c>. Lanza <see cref="HttpRequestException"/>
    /// si el servicio no responde o devuelve un código no exitoso. El caller
    /// decide si propaga el error o lo silencia (el flujo de notificaciones
    /// del backend lo silencia para no bloquear la operación principal).
    /// Devuelve <see cref="MailSendResult.Omitido"/> si el aviso está desactivado.
    /// </summary>
    Task<MailSendResult> SendMailAsync(SendMailRequest request, CancellationToken ct);

    /// <summary>
    /// Invoca <c>POST /send-test</c> con una configuración SMTP explícita (no la guardada).
    /// Lanza <see cref="HttpRequestException"/> si mail_sender no responde.
    /// </summary>
    Task<MailTestResult> SendTestAsync(SendTestMailRequest request, CancellationToken ct);
}
```

`MailSenderHttpClient.cs` — reemplazar `SendMailAsync` y agregar `SendTestAsync`:

```csharp
    public async Task<MailSendResult> SendMailAsync(SendMailRequest request, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("send-mail", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body)) return MailSendResult.Enviado;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("status", out var status)
                && string.Equals(status.GetString(), "omitido", StringComparison.OrdinalIgnoreCase))
            {
                return MailSendResult.Omitido;
            }
        }
        catch (JsonException)
        {
            // Respuesta no JSON con 2xx: se considera enviada (comportamiento previo).
        }
        return MailSendResult.Enviado;
    }

    public async Task<MailTestResult> SendTestAsync(SendTestMailRequest request, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("send-test", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MailTestResult>(JsonOptions, ct)
            ?? new MailTestResult { Ok = false, Error = "Respuesta vacía de mail_sender." };
    }
```

> Con `JsonIgnoreCondition.WhenWritingNull`, `smtp.password = null` no se envía; mail_sender lo toma como "sin contraseña". Es el comportamiento buscado.

`EstadoEnvioCertificado.cs` — agregar a la doc `///   <c>Omitido</c>  — el aviso "Certificado al participante" está desactivado; no se envió.` y el valor:

```csharp
public enum EstadoEnvioCertificado
{
    Pendiente = 1,
    Enviado = 2,
    Error = 3,
    Omitido = 4
}
```

`GenerarYEnviarCertificadosUseCase.cs` — reemplazar el bloque "3. Envío del correo" hasta la marca de `Enviado`:

```csharp
            // 3. Envío del correo (mail_sender → O365) con reintentos.
            var resultado = await ReintentarAsync(() => _mail.SendMailAsync(request, ct), ct);

            // Aviso desactivado en Configuración → Correo: no salió ningún correo.
            var estado = resultado == MailSendResult.Omitido
                ? EstadoEnvioCertificado.Omitido
                : EstadoEnvioCertificado.Enviado;
            DateTime? fechaEnvio = estado == EstadoEnvioCertificado.Enviado ? DateTime.UtcNow : null;

            await _asistentes.ActualizarResultadoEnvioAsync(asistenteId, estado, fechaEnvio, null, ct);
```

Verificar que el archivo ya tiene `using Capacitaciones.Application.Dtos.Notifications;` (usa `SendMailRequest`); si no, agregarlo. `MarcarErroresComoPendientesAsync` (`AsistenteRepository.cs:185`) filtra solo `EstadoEnvioCertificado.Error`, así que los `Omitido` no se reintentan.

Agregar a `tests/Capacitaciones.Tests/Fakes/CorreoFakes.cs`:

```csharp
internal sealed class FakeMailSenderClient : IMailSenderClient
{
    public SendTestMailRequest? LastTest { get; private set; }
    public MailTestResult NextTestResult { get; set; } = new() { Ok = true };

    public Task<MailSendResult> SendMailAsync(SendMailRequest request, CancellationToken ct) =>
        Task.FromResult(MailSendResult.Enviado);

    public Task<MailTestResult> SendTestAsync(SendTestMailRequest request, CancellationToken ct)
    {
        LastTest = request;
        return Task.FromResult(NextTestResult);
    }
}
```

- [ ] **Step 4: Correr la suite completa**

Run: `dotnet test`
Expected: todo PASS (los 5 casos de uso que hacen `await _mail.SendMailAsync(...)` compilan igual, descartando el resultado).

- [ ] **Step 5: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(correo): cliente mail_sender distingue avisos omitidos y envía correos de prueba"
```

---

### Task 6: Caso de uso de correo de prueba, endpoints admin, endpoint interno y DI

**Files:**
- Create: `backend/src/Capacitaciones.Application/UseCases/Configuracion/EnviarCorreoPruebaUseCase.cs`
- Create: `backend/src/Capacitaciones.Api/Filters/InternalApiKeyFilter.cs`
- Create: `backend/src/Capacitaciones.Api/Controllers/InternalCorreoController.cs`
- Modify: `backend/src/Capacitaciones.Api/Controllers/ConfiguracionController.cs`
- Modify: `backend/src/Capacitaciones.Api/Program.cs` (después de la línea `builder.Services.AddScoped<ActualizarNumeracionUseCase>();` ~235)
- Test: `backend/tests/Capacitaciones.Tests/CorreoEndpointsTests.cs`

**Interfaces:**
- Consumes: todo lo anterior.
- Produces:
  - `EnviarCorreoPruebaUseCase.ExecuteAsync(UpdateConfiguracionCorreoDto input, string adminEmail, ct) : Task<CorreoPruebaResultadoDto>`
  - `InternalApiOptions { string? ApiKey }`, `InternalApiKeyFilter` (header `X-Internal-Key`)
  - HTTP: `GET/PUT api/configuracion/correo`, `GET/PUT api/configuracion/correo/notificaciones`, `POST api/configuracion/correo/prueba`, `GET api/internal/correo-config`

- [ ] **Step 1: Escribir los tests de endpoints que fallan**

`CorreoEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Capacitaciones.Api.Filters;
using Capacitaciones.Application.Ports;
using Capacitaciones.Infrastructure.Security;
using Capacitaciones.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Capacitaciones.Tests;

/// <summary>Factory que fija llave de cifrado, API key interna y un mail_sender falso.</summary>
public class CorreoWebAppFactory : InMemoryWebAppFactory
{
    public const string InternalKey = "test-internal-key";
    internal FakeMailSenderClient Mail { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ISecretProtector>(
                new AesGcmSecretProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
            services.AddSingleton(new InternalApiOptions { ApiKey = InternalKey });
            services.AddSingleton<IMailSenderClient>(Mail);
        });
    }
}

public class CorreoEndpointsTests : IClassFixture<CorreoWebAppFactory>
{
    private readonly CorreoWebAppFactory _factory;

    public CorreoEndpointsTests(CorreoWebAppFactory factory)
    {
        _factory = factory;
    }

    private static object ValidPayload(string? password = "Clave123*") => new
    {
        smtpHost = "smtp.office365.com",
        smtpPort = 587,
        password,
        usarTls = true,
        remitenteCorreo = "capacitaciones@dos.com.ec",
        remitenteNombre = "CapacitaDOS",
        ccGlobal = "copia@dos.com.ec"
    };

    [Fact]
    public async Task Correo_SinToken_401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/configuracion/correo");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Correo_PutYGet_NoExponePassword()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var put = await client.PutAsJsonAsync("/api/configuracion/correo", ValidPayload());
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var raw = await client.GetStringAsync("/api/configuracion/correo");
        Assert.DoesNotContain("Clave123*", raw);
        using var doc = JsonDocument.Parse(raw);
        Assert.True(doc.RootElement.GetProperty("configurado").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("tienePassword").GetBoolean());
    }

    [Fact]
    public async Task Correo_PutInvalido_400ConErroresPorCampo()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync("/api/configuracion/correo", new
        {
            smtpHost = "", smtpPort = 587, remitenteCorreo = "x"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VALIDACION", doc.RootElement.GetProperty("error").GetString());
        Assert.True(doc.RootElement.GetProperty("errores").TryGetProperty("SmtpHost", out _));
    }

    [Fact]
    public async Task Notificaciones_PutYGet()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var put = await client.PutAsJsonAsync("/api/configuracion/correo/notificaciones", new[]
        {
            new { plantilla = "encuesta_satisfaccion", activo = false, asuntoPersonalizado = (string?)null }
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/configuracion/correo/notificaciones"));
        var encuesta = doc.RootElement.EnumerateArray()
            .Single(e => e.GetProperty("plantilla").GetString() == "encuesta_satisfaccion");
        Assert.False(encuesta.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Prueba_UsaPasswordGuardadaYEnviaAlAdmin()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        await client.PutAsJsonAsync("/api/configuracion/correo", ValidPayload("Guardada1*"));

        var response = await client.PostAsJsonAsync("/api/configuracion/correo/prueba", ValidPayload(password: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Guardada1*", _factory.Mail.LastTest!.Smtp.Password);
        Assert.Equal(InMemoryWebAppFactory.SeededAdminEmail, _factory.Mail.LastTest.Recipient);
    }

    [Fact]
    public async Task Interna_SinHeader_403()
    {
        var response = await _factory.CreateClient().GetAsync("/api/internal/correo-config");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Interna_HeaderIncorrecto_403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Key", "otra");
        var response = await client.GetAsync("/api/internal/correo-config");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Interna_HeaderCorrecto_DevuelveNotificaciones()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Key", CorreoWebAppFactory.InternalKey);

        var response = await client.GetAsync("/api/internal/correo-config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("notificaciones").TryGetProperty("certificado_participante", out _));
    }
}
```

> Los tests comparten la BD de la factory (`IClassFixture`). Ningún test asume el orden de ejecución: `Prueba_...` hace su propio `PUT` antes.

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test --filter CorreoEndpointsTests`
Expected: error de compilación (`InternalApiOptions` no existe).

- [ ] **Step 3: Implementar caso de uso de prueba, filtro y controllers**

`EnviarCorreoPruebaUseCase.cs`:

```csharp
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>
/// Caso de uso: enviar un correo de prueba con los datos del formulario (aunque no estén
/// guardados) al admin que lo pide. Si no se escribe contraseña, usa la guardada.
/// </summary>
public class EnviarCorreoPruebaUseCase
{
    private readonly IConfiguracionCorreoRepository _repo;
    private readonly ISecretProtector _protector;
    private readonly IMailSenderClient _mail;

    public EnviarCorreoPruebaUseCase(
        IConfiguracionCorreoRepository repo, ISecretProtector protector, IMailSenderClient mail)
    {
        _repo = repo;
        _protector = protector;
        _mail = mail;
    }

    public async Task<CorreoPruebaResultadoDto> ExecuteAsync(
        UpdateConfiguracionCorreoDto input, string adminEmail, CancellationToken ct = default)
    {
        if (input is null) throw new ConfiguracionCorreoException("INVALID_INPUT", "Payload requerido.");

        var errores = ConfiguracionCorreoValidator.Validar(input);
        if (errores.Count > 0)
        {
            throw new ConfiguracionCorreoException("VALIDACION", "Revisa los campos marcados.", errores);
        }

        string? password;
        if (input.QuitarPassword)
        {
            password = null;
        }
        else if (!string.IsNullOrEmpty(input.Password))
        {
            password = input.Password;
        }
        else
        {
            var guardada = (await _repo.GetAsync(ct))?.SmtpPasswordCifrada;
            password = string.IsNullOrEmpty(guardada) ? null : _protector.TryUnprotect(guardada);
        }

        var request = new SendTestMailRequest
        {
            Recipient = adminEmail,
            Smtp = new SmtpInternoDto
            {
                Host = input.SmtpHost.Trim(),
                Port = input.SmtpPort,
                User = string.IsNullOrWhiteSpace(input.SmtpUser) ? null : input.SmtpUser.Trim(),
                Password = password,
                UseTls = input.UsarTls,
                From = input.RemitenteCorreo.Trim(),
                FromName = string.IsNullOrWhiteSpace(input.RemitenteNombre) ? null : input.RemitenteNombre.Trim()
            }
        };

        try
        {
            var result = await _mail.SendTestAsync(request, ct);
            return result.Ok
                ? new CorreoPruebaResultadoDto { Ok = true, Mensaje = $"Correo de prueba enviado a {adminEmail}." }
                : new CorreoPruebaResultadoDto { Ok = false, Mensaje = result.Error ?? "Error desconocido del servidor SMTP." };
        }
        catch (HttpRequestException ex)
        {
            return new CorreoPruebaResultadoDto
            {
                Ok = false,
                Mensaje = $"No se pudo contactar al servicio de correo: {ex.Message}"
            };
        }
    }
}
```

`Api/Filters/InternalApiKeyFilter.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Capacitaciones.Api.Filters;

/// <summary>API key compartida con servicios internos (mail_sender). Env: <c>MAIL_CONFIG_API_KEY</c>.</summary>
public class InternalApiOptions
{
    public string? ApiKey { get; set; }
}

/// <summary>
/// Exige el header <c>X-Internal-Key</c> igual a <see cref="InternalApiOptions.ApiKey"/>.
/// Sin llave configurada responde siempre 403. Además nginx bloquea /api/internal/ desde fuera.
/// </summary>
public class InternalApiKeyFilter : IAuthorizationFilter
{
    public const string HeaderName = "X-Internal-Key";

    private readonly InternalApiOptions _options;

    public InternalApiKeyFilter(InternalApiOptions options)
    {
        _options = options;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = _options.ApiKey;
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(expected)
            || string.IsNullOrEmpty(provided)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided)))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
    }
}
```

`Api/Controllers/InternalCorreoController.cs`:

```csharp
using Capacitaciones.Api.Filters;
using Capacitaciones.Application.UseCases.Configuracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>Endpoints para servicios internos de la red Docker. No usa JWT: valida X-Internal-Key.</summary>
[ApiController]
[AllowAnonymous]
[ServiceFilter(typeof(InternalApiKeyFilter))]
[Route("api/internal")]
public class InternalCorreoController : ControllerBase
{
    private readonly ObtenerConfiguracionCorreoInternaUseCase _obtener;

    public InternalCorreoController(ObtenerConfiguracionCorreoInternaUseCase obtener)
    {
        _obtener = obtener;
    }

    [HttpGet("correo-config")]
    public async Task<IActionResult> GetCorreoConfig(CancellationToken ct) =>
        Ok(await _obtener.ExecuteAsync(ct));
}
```

`ConfiguracionController.cs` — ampliar el constructor e incluir los endpoints nuevos (dejar los de numeración tal cual):

```csharp
using System.Security.Claims;
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.UseCases.Configuracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/configuracion")]
public class ConfiguracionController : ControllerBase
{
    private readonly ObtenerNumeracionUseCase _obtener;
    private readonly ActualizarNumeracionUseCase _actualizar;
    private readonly ObtenerConfiguracionCorreoUseCase _obtenerCorreo;
    private readonly ActualizarConfiguracionCorreoUseCase _actualizarCorreo;
    private readonly ListarNotificacionesUseCase _listarNotificaciones;
    private readonly ActualizarNotificacionesUseCase _actualizarNotificaciones;
    private readonly EnviarCorreoPruebaUseCase _enviarPrueba;

    public ConfiguracionController(
        ObtenerNumeracionUseCase obtener,
        ActualizarNumeracionUseCase actualizar,
        ObtenerConfiguracionCorreoUseCase obtenerCorreo,
        ActualizarConfiguracionCorreoUseCase actualizarCorreo,
        ListarNotificacionesUseCase listarNotificaciones,
        ActualizarNotificacionesUseCase actualizarNotificaciones,
        EnviarCorreoPruebaUseCase enviarPrueba)
    {
        _obtener = obtener;
        _actualizar = actualizar;
        _obtenerCorreo = obtenerCorreo;
        _actualizarCorreo = actualizarCorreo;
        _listarNotificaciones = listarNotificaciones;
        _actualizarNotificaciones = actualizarNotificaciones;
        _enviarPrueba = enviarPrueba;
    }

    // ... GetNumeracion y PutNumeracion sin cambios ...

    [HttpGet("correo")]
    public async Task<IActionResult> GetCorreo(CancellationToken ct) =>
        Ok(await _obtenerCorreo.ExecuteAsync(ct));

    [HttpPut("correo")]
    public Task<IActionResult> PutCorreo([FromBody] UpdateConfiguracionCorreoDto input, CancellationToken ct) =>
        Ejecutar(async email => Ok(await _actualizarCorreo.ExecuteAsync(input, email, ct)));

    [HttpGet("correo/notificaciones")]
    public async Task<IActionResult> GetNotificaciones(CancellationToken ct) =>
        Ok(await _listarNotificaciones.ExecuteAsync(ct));

    [HttpPut("correo/notificaciones")]
    public Task<IActionResult> PutNotificaciones([FromBody] List<UpdateNotificacionDto> input, CancellationToken ct) =>
        Ejecutar(async email => Ok(await _actualizarNotificaciones.ExecuteAsync(input, email, ct)));

    [HttpPost("correo/prueba")]
    public Task<IActionResult> PostPrueba([FromBody] UpdateConfiguracionCorreoDto input, CancellationToken ct) =>
        Ejecutar(async email => Ok(await _enviarPrueba.ExecuteAsync(input, email, ct)));

    /// <summary>Resuelve el email del admin y traduce <see cref="ConfiguracionCorreoException"/> a HTTP.</summary>
    private async Task<IActionResult> Ejecutar(Func<string, Task<IActionResult>> accion)
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "SIN_EMAIL", message = "El token no incluye el email del administrador." });
        }

        try
        {
            return await accion(email);
        }
        catch (ConfiguracionCorreoException ex)
        {
            var status = ex.Codigo == "ENCRYPTION_KEY_MISSING"
                ? StatusCodes.Status500InternalServerError
                : StatusCodes.Status400BadRequest;
            return new ObjectResult(new { error = ex.Codigo, message = ex.Message, errores = ex.Errores })
            {
                StatusCode = status
            };
        }
    }
}
```

`Program.cs` — después de `builder.Services.AddScoped<ActualizarNumeracionUseCase>();`:

```csharp
// --- Configuración de correo (Configuración → Correo) ---
ISecretProtector secretProtector;
try
{
    secretProtector = new AesGcmSecretProtector(builder.Configuration["CORREO_ENCRYPTION_KEY"]);
}
catch (Exception ex) when (ex is FormatException or ArgumentException)
{
    // Llave mal formada: el backend arranca igual; guardar una contraseña SMTP fallará con un mensaje claro.
    Console.Error.WriteLine($"[CorreoConfig] CORREO_ENCRYPTION_KEY inválida: {ex.Message}");
    secretProtector = new AesGcmSecretProtector(null);
}
builder.Services.AddSingleton<ISecretProtector>(secretProtector);
builder.Services.AddSingleton(new InternalApiOptions { ApiKey = builder.Configuration["MAIL_CONFIG_API_KEY"] });
builder.Services.AddScoped<InternalApiKeyFilter>();
builder.Services.AddScoped<IConfiguracionCorreoRepository, ConfiguracionCorreoRepository>();
builder.Services.AddScoped<IConfiguracionNotificacionRepository, ConfiguracionNotificacionRepository>();
builder.Services.AddScoped<ObtenerConfiguracionCorreoUseCase>();
builder.Services.AddScoped<ActualizarConfiguracionCorreoUseCase>();
builder.Services.AddScoped<ListarNotificacionesUseCase>();
builder.Services.AddScoped<ActualizarNotificacionesUseCase>();
builder.Services.AddScoped<ObtenerConfiguracionCorreoInternaUseCase>();
builder.Services.AddScoped<EnviarCorreoPruebaUseCase>();
```

Agregar los `using` que falten al inicio de `Program.cs`: `Capacitaciones.Api.Filters`, `Capacitaciones.Infrastructure.Security` (revisar si ya está, lo usa `JwtTokenGenerator`), `Capacitaciones.Infrastructure.Persistence.Repositories` y `Capacitaciones.Application.UseCases.Configuracion` (probablemente ya existen).

> `EnviarCorreoPruebaUseCase` depende de `IMailSenderClient`, que ya está registrado con `AddHttpClient` (línea ~259). No hay que registrarlo de nuevo.

- [ ] **Step 4: Correr la suite completa**

Run: `dotnet test`
Expected: todo PASS, incluidos los 8 de `CorreoEndpointsTests`.

- [ ] **Step 5: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(correo): endpoints de administración, correo de prueba y endpoint interno para mail_sender"
```

---

### Task 7: mail_sender — proveedor de configuración con cache y fallback

**Files:**
- Create: `mail_sender/config_provider.py`
- Create: `mail_sender/tests/__init__.py` (vacío), `mail_sender/tests/conftest.py`
- Test: `mail_sender/tests/test_config_provider.py`

**Interfaces:**
- Produces: `config_provider.get_remote_config() -> Optional[dict]` (JSON del endpoint interno, claves camelCase: `configurado`, `passwordInvalida`, `smtp`, `ccGlobal`, `bccGlobal`, `notificaciones`), `config_provider.reset_cache() -> None`, `config_provider._fetch_remote() -> Optional[dict]` (se reemplaza en tests).

- [ ] **Step 1: Escribir los tests que fallan**

`tests/conftest.py`:

```python
import os
import sys

# Permite `import app` / `import config_provider` corriendo pytest desde mail_sender/.
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

# Variables SMTP mínimas para el fallback a .env en los tests.
os.environ.setdefault("SMTP_HOST", "smtp.env.local")
os.environ.setdefault("SMTP_PORT", "25")
os.environ.setdefault("SMTP_FROM", "env@dos.com.ec")
os.environ.setdefault("TEMPLATES_DIR", os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "plantillas")))
```

`tests/test_config_provider.py`:

```python
import pytest

import config_provider


@pytest.fixture(autouse=True)
def _reset():
    config_provider.reset_cache()
    yield
    config_provider.reset_cache()


def test_cachea_dentro_de_la_ventana(monkeypatch):
    llamadas = []
    monkeypatch.setattr(config_provider, "_fetch_remote", lambda: llamadas.append(1) or {"configurado": True})

    assert config_provider.get_remote_config() == {"configurado": True}
    assert config_provider.get_remote_config() == {"configurado": True}
    assert len(llamadas) == 1


def test_refresca_al_vencer(monkeypatch):
    llamadas = []
    monkeypatch.setattr(config_provider, "_fetch_remote", lambda: llamadas.append(1) or {"n": len(llamadas)})
    monkeypatch.setattr(config_provider, "CACHE_SECONDS", 0)

    config_provider.get_remote_config()
    assert config_provider.get_remote_config() == {"n": 2}


def test_backend_caido_sin_cache_devuelve_none(monkeypatch):
    def boom():
        raise OSError("connection refused")

    monkeypatch.setattr(config_provider, "_fetch_remote", boom)
    assert config_provider.get_remote_config() is None


def test_backend_caido_con_cache_vencida_devuelve_la_ultima(monkeypatch):
    monkeypatch.setattr(config_provider, "_fetch_remote", lambda: {"configurado": True})
    monkeypatch.setattr(config_provider, "CACHE_SECONDS", 0)
    config_provider.get_remote_config()

    def boom():
        raise OSError("timeout")

    monkeypatch.setattr(config_provider, "_fetch_remote", boom)
    assert config_provider.get_remote_config() == {"configurado": True}


def test_sin_variables_no_llama_al_backend(monkeypatch):
    monkeypatch.delenv("BACKEND_INTERNAL_URL", raising=False)
    monkeypatch.delenv("MAIL_CONFIG_API_KEY", raising=False)
    assert config_provider._fetch_remote() is None
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `cd mail_sender; .\.venv\Scripts\python -m pytest tests/test_config_provider.py -v`
Expected: `ModuleNotFoundError: No module named 'config_provider'`.

- [ ] **Step 3: Implementar `config_provider.py`**

```python
"""
Configuración de correo administrada desde la app (Configuración → Correo).

Se lee del backend (`GET {BACKEND_INTERNAL_URL}/api/internal/correo-config`, header
`X-Internal-Key: {MAIL_CONFIG_API_KEY}`) y se cachea `CONFIG_CACHE_SECONDS` segundos.
Si falta alguna de las variables, o el backend no responde y no hay nada cacheado, se
devuelve None y `app.py` usa las variables SMTP_* del entorno como siempre.
"""

import json
import logging
import os
import time
import urllib.request
from typing import Any, Dict, Optional

log = logging.getLogger("mail_sender.config")

CACHE_SECONDS = float(os.environ.get("CONFIG_CACHE_SECONDS", "60"))
FETCH_TIMEOUT_SECONDS = 5

_cache: Dict[str, Any] = {"value": None, "fetched_at": 0.0}


def _fetch_remote() -> Optional[Dict[str, Any]]:
    base = os.environ.get("BACKEND_INTERNAL_URL", "").strip().rstrip("/")
    key = os.environ.get("MAIL_CONFIG_API_KEY", "").strip()
    if not base or not key:
        return None
    req = urllib.request.Request(
        f"{base}/api/internal/correo-config",
        headers={"X-Internal-Key": key, "Accept": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=FETCH_TIMEOUT_SECONDS) as resp:
        return json.loads(resp.read().decode("utf-8"))


def get_remote_config() -> Optional[Dict[str, Any]]:
    """Config del backend (cacheada). Ante un error devuelve la última conocida, aunque esté vencida."""
    now = time.monotonic()
    cached = _cache["value"]
    if cached is not None and now - _cache["fetched_at"] < CACHE_SECONDS:
        return cached

    try:
        value = _fetch_remote()
    except Exception as exc:  # red, HTTP 4xx/5xx, JSON inválido
        log.warning("No se pudo obtener la configuración de correo del backend: %s", exc)
        return cached

    if value is None:
        return None
    _cache["value"] = value
    _cache["fetched_at"] = now
    return value


def reset_cache() -> None:
    _cache["value"] = None
    _cache["fetched_at"] = 0.0
```

- [ ] **Step 4: Correr y verificar que pasa**

Run: `.\.venv\Scripts\python -m pytest tests/test_config_provider.py -v`
Expected: 5 PASS.

- [ ] **Step 5: Commit**

```bash
git add mail_sender/config_provider.py mail_sender/tests
git commit -m "feat(mail_sender): lee la configuración de correo del backend con cache y fallback"
```

---

### Task 8: mail_sender — aplicar reglas en `/send-mail`

**Files:**
- Modify: `mail_sender/app.py`
- Test: `mail_sender/tests/test_send_mail_rules.py`

**Interfaces:**
- Consumes: `config_provider.get_remote_config()` (Task 7), contrato del endpoint interno (Task 4).
- Produces (en `app.py`):
  - `class SmtpSettings(BaseModel)` con alias `useTls`, `from`, `fromName`
  - `smtp_settings_to_cfg(s: SmtpSettings) -> Dict[str, str]` (mismo formato que `load_smtp_config()`)
  - `resolve_smtp_config(remote: Optional[dict]) -> Dict[str, str]`
  - `resolve_subject(original: str, custom: Optional[str], parameters: dict) -> str`
  - `merge_addresses(own, extra, exclude) -> List[str]`
  - `build_message(sender, to, subject, html_body, cc, attachment) -> MIMEMultipart` (firma nueva)
  - `send_via_smtp(message, recipients, cfg) -> None` (recibe `cfg`)

- [ ] **Step 1: Escribir los tests que fallan**

`tests/test_send_mail_rules.py`:

```python
import pytest
from fastapi.testclient import TestClient

import app as app_module

client = TestClient(app_module.app)

REMOTE = {
    "configurado": True,
    "passwordInvalida": False,
    "smtp": {
        "host": "smtp.db.local", "port": 587, "user": None, "password": "p",
        "useTls": True, "from": "db@dos.com.ec", "fromName": "CapacitaDOS",
    },
    "ccGlobal": ["copia@dos.com.ec", "DESTINO@dos.com.ec"],
    "bccGlobal": ["oculta@dos.com.ec"],
    "notificaciones": {
        "certificado_participante": {"activo": True, "asunto": "[DOS] {{ tema }} | {{ asunto_original }}"},
        "encuesta_satisfaccion": {"activo": False, "asunto": None},
        "responsable_firma": {"activo": True, "asunto": "{{ variable_que_no_existe }}"},
    },
}


@pytest.fixture
def sent(monkeypatch):
    calls = []

    def fake_send(message, recipients, cfg):
        calls.append({"message": message, "recipients": recipients, "cfg": cfg})

    monkeypatch.setattr(app_module, "send_via_smtp", fake_send)
    return calls


def post(template, subject="Original", params=None, cc=None):
    body = {
        "template": template,
        "subject": subject,
        "recipients": ["destino@dos.com.ec"],
        "parameters": params or {"nombre": "Ana", "tema": "Excel", "fecha": "1", "link": "x"},
    }
    if cc:
        body["cc"] = cc
    return client.post("/send-mail", json=body)


def test_desactivado_no_envia_y_responde_omitido(monkeypatch, sent):
    monkeypatch.setattr(app_module, "get_remote_config", lambda: REMOTE)
    r = post("encuesta_satisfaccion")
    assert r.status_code == 200
    assert r.json()["status"] == "omitido"
    assert sent == []


def test_asunto_personalizado_y_copias_globales(monkeypatch, sent):
    monkeypatch.setattr(app_module, "get_remote_config", lambda: REMOTE)
    r = post("certificado_participante", subject="Tu certificado: Excel", cc=["copia@dos.com.ec"])

    assert r.json()["status"] == "sent"
    msg = sent[0]["message"]
    assert msg["Subject"] == "[DOS] Excel | Tu certificado: Excel"
    # CC sin duplicados y sin repetir al destinatario (comparación sin mayúsculas).
    assert msg["Cc"] == "copia@dos.com.ec"
    assert sorted(sent[0]["recipients"]) == ["copia@dos.com.ec", "destino@dos.com.ec", "oculta@dos.com.ec"]
    assert sent[0]["cfg"]["host"] == "smtp.db.local"
    assert sent[0]["cfg"]["from_email"] == "db@dos.com.ec"


def test_asunto_con_error_usa_el_original(monkeypatch, sent):
    monkeypatch.setattr(app_module, "get_remote_config", lambda: REMOTE)
    post("responsable_firma", subject="Carga tus datos")
    assert sent[0]["message"]["Subject"] == "Carga tus datos"


def test_sin_config_remota_usa_env(monkeypatch, sent):
    monkeypatch.setattr(app_module, "get_remote_config", lambda: None)
    r = post("certificado_participante", subject="Tu certificado: Excel")
    assert r.json()["status"] == "sent"
    assert sent[0]["cfg"]["host"] == "smtp.env.local"
    assert sent[0]["message"]["Subject"] == "Tu certificado: Excel"


def test_password_invalida_usa_smtp_env_pero_aplica_reglas(monkeypatch, sent):
    remote = {**REMOTE, "passwordInvalida": True}
    monkeypatch.setattr(app_module, "get_remote_config", lambda: remote)
    post("certificado_participante", subject="Tu certificado: Excel")
    assert sent[0]["cfg"]["host"] == "smtp.env.local"
    assert sent[0]["message"]["Subject"].startswith("[DOS] Excel")


def test_plantilla_sin_regla_se_envia_normal(monkeypatch, sent):
    monkeypatch.setattr(app_module, "get_remote_config", lambda: REMOTE)
    r = post("ejemplo", subject="Hola", params={})
    assert r.json()["status"] == "sent"
    assert sent[0]["message"]["Subject"] == "Hola"
```

> `ejemplo.html` existe en `plantillas/`. Si fallara al renderizar con `params={}`, pasarle los parámetros que use esa plantilla.

- [ ] **Step 2: Correr y verificar que falla**

Run: `.\.venv\Scripts\python -m pytest tests/test_send_mail_rules.py -v`
Expected: FAIL (`app` no tiene `get_remote_config`; `send_via_smtp` recibe 2 argumentos).

- [ ] **Step 3: Implementar en `app.py`**

1. Imports: agregar `import logging`; cambiar `from pydantic import BaseModel, EmailStr, Field` por `from pydantic import BaseModel, ConfigDict, EmailStr, Field`; cambiar el import de jinja2 por `from jinja2 import Environment, FileSystemLoader, StrictUndefined, TemplateNotFound, select_autoescape`; agregar `from config_provider import get_remote_config`. Después de las constantes de reintentos: `log = logging.getLogger("mail_sender")`.

2. Después de `format_from_header`, agregar:

```python
class SmtpSettings(BaseModel):
    """Configuración SMTP explícita (del backend o de /send-test). Claves camelCase del backend."""

    model_config = ConfigDict(populate_by_name=True)

    host: str
    port: int
    user: Optional[str] = None
    password: Optional[str] = None
    use_tls: bool = Field(default=True, alias="useTls")
    from_email: str = Field(alias="from")
    from_name: Optional[str] = Field(default=None, alias="fromName")


def smtp_settings_to_cfg(s: SmtpSettings) -> Dict[str, str]:
    """Convierte a la misma forma que devuelve `load_smtp_config()`."""
    return {
        "host": s.host,
        "port": str(s.port),
        "from_email": s.from_email,
        "from_name": (s.from_name or "").strip(),
        "user": (s.user or "").strip() or s.from_email,
        "password": s.password or "",
        "use_tls": "true" if s.use_tls else "false",
    }


def resolve_smtp_config(remote: Optional[Dict[str, Any]]) -> Dict[str, str]:
    """SMTP de la BD si está configurado y la contraseña es legible; si no, el del entorno (.env)."""
    if remote and remote.get("configurado") and remote.get("smtp"):
        if remote.get("passwordInvalida"):
            log.warning("La contraseña SMTP guardada no se puede descifrar; se usa la configuración del .env.")
        else:
            return smtp_settings_to_cfg(SmtpSettings.model_validate(remote["smtp"]))
    return load_smtp_config()


subject_env = Environment(undefined=StrictUndefined, autoescape=False)


def resolve_subject(original: str, custom: Optional[str], parameters: Dict[str, Any]) -> str:
    """Renderiza el asunto personalizado con los parámetros del correo; ante error, el original."""
    if not custom or not custom.strip():
        return original
    try:
        rendered = subject_env.from_string(custom).render(**{**parameters, "asunto_original": original})
    except Exception as exc:
        log.warning("Asunto personalizado inválido (%r): %s. Se usa el original.", custom, exc)
        return original
    rendered = " ".join(rendered.split())  # sin saltos de línea en el header Subject
    return rendered or original


def merge_addresses(own: Optional[List[str]], extra: Optional[List[str]], exclude: List[str]) -> List[str]:
    """Une dos listas sin duplicados (sin distinguir mayúsculas) y sin direcciones de `exclude`."""
    seen = {str(e).strip().lower() for e in exclude}
    result: List[str] = []
    for addr in list(own or []) + list(extra or []):
        value = str(addr).strip()
        if value and value.lower() not in seen:
            seen.add(value.lower())
            result.append(value)
    return result
```

3. Reemplazar `build_message` por la versión que no depende del request:

```python
def build_message(
    sender: str,
    to: List[str],
    subject: str,
    html_body: str,
    cc: List[str],
    attachment: Optional[Attachment],
) -> MIMEMultipart:
    msg = MIMEMultipart("mixed")
    msg["Subject"] = subject
    msg["From"] = sender
    msg["To"] = ", ".join(to)
    if cc:
        msg["Cc"] = ", ".join(cc)

    alt = MIMEMultipart("alternative")
    alt.attach(MIMEText(html_body, "html", "utf-8"))
    msg.attach(alt)

    if attachment:
        try:
            data = base64.b64decode(attachment.content_base64, validate=True)
        except Exception as exc:
            raise HTTPException(status_code=400, detail=f"Adjunto base64 inválido: {exc}")

        maintype, _, subtype = (attachment.mime_type or "application/octet-stream").partition("/")
        part = MIMEBase(maintype or "application", subtype or "octet-stream")
        part.set_payload(data)
        encoders.encode_base64(part)
        part.add_header(
            "Content-Disposition",
            f'attachment; filename="{attachment.filename}"',
        )
        msg.attach(part)

    return msg
```

4. `send_via_smtp`: cambiar la firma a `def send_via_smtp(message: MIMEMultipart, recipients: List[str], cfg: Dict[str, str]) -> None:` y borrar su primera línea `cfg = load_smtp_config()`. El resto no cambia.

5. Reemplazar el cuerpo de `send_mail` hasta el bucle de reintentos (el bucle y el `return` final no cambian, salvo lo indicado):

```python
@app.post("/send-mail", response_model=SendMailResponse)
def send_mail(request: SendMailRequest) -> SendMailResponse:
    remote = get_remote_config()
    regla = ((remote or {}).get("notificaciones") or {}).get(request.template)

    if regla is not None and regla.get("activo") is False:
        log.info("Notificación '%s' desactivada; se omite el envío a %s.", request.template, request.recipients)
        return SendMailResponse(
            status="omitido",
            template=request.template,
            recipients=[str(r) for r in request.recipients],
            has_attachment=request.attachment is not None,
        )

    subject = resolve_subject(request.subject, (regla or {}).get("asunto"), request.parameters)
    # Las plantillas pueden mostrar {{ subject }} en el cuerpo: que coincida con el asunto final.
    parameters = {**request.parameters, "subject": subject}
    html_body = render_template(request.template, parameters)

    cfg = resolve_smtp_config(remote)
    from_header = format_from_header(cfg)

    to = [str(r) for r in request.recipients]
    cc = merge_addresses(request.cc, (remote or {}).get("ccGlobal"), exclude=to)
    bcc = merge_addresses(request.bcc, (remote or {}).get("bccGlobal"), exclude=to + cc)

    message = build_message(from_header, to, subject, html_body, cc, request.attachment)
    all_recipients = to + cc + bcc
```

En el bucle de reintentos, cambiar `send_via_smtp(message, all_recipients)` por `send_via_smtp(message, all_recipients, cfg)`.

6. Al inicio del módulo, después de los imports, configurar logging si uvicorn no lo hizo: `logging.basicConfig(level=logging.INFO)`.

- [ ] **Step 4: Correr todos los tests de mail_sender**

Run: `.\.venv\Scripts\python -m pytest -v`
Expected: 11 PASS (5 de config_provider + 6 de reglas).

- [ ] **Step 5: Commit**

```bash
git add mail_sender/app.py mail_sender/tests/test_send_mail_rules.py
git commit -m "feat(mail_sender): aplica avisos desactivados, asuntos personalizados y copias globales"
```

---

### Task 9: mail_sender — `/send-test`, plantilla, Docker, compose y documentación

**Files:**
- Modify: `mail_sender/app.py`
- Create: `mail_sender/plantillas/prueba_configuracion.html`
- Modify: `mail_sender/Dockerfile`, `mail_sender/documentacion.md`
- Modify: `docker-compose.yml` (servicio `backend` ~línea 62-72 y `mail_sender` ~línea 188-198), `.env.example` (después de `SMTP_USE_TLS=true`)
- Test: `mail_sender/tests/test_send_test.py`

**Interfaces:**
- Consumes: `SmtpSettings`, `smtp_settings_to_cfg`, `build_message`, `send_via_smtp` (Task 8); contrato de `SendTestMailRequest` (Task 5).
- Produces: `POST /send-test` body `{smtp: SmtpSettings, recipient}` → `{ok: bool, error: str|null}`.

- [ ] **Step 1: Escribir el test que falla**

`tests/test_send_test.py`:

```python
import smtplib

from fastapi.testclient import TestClient

import app as app_module

client = TestClient(app_module.app)

BODY = {
    "recipient": "admin@dos.com.ec",
    "smtp": {"host": "smtp.x.local", "port": 587, "useTls": True, "from": "r@dos.com.ec", "fromName": "CapacitaDOS"},
}


def test_send_test_ok_usa_la_config_recibida(monkeypatch):
    calls = []
    monkeypatch.setattr(app_module, "send_via_smtp", lambda m, r, cfg: calls.append((m, r, cfg)))

    r = client.post("/send-test", json=BODY)

    assert r.status_code == 200
    assert r.json() == {"ok": True, "error": None}
    message, recipients, cfg = calls[0]
    assert recipients == ["admin@dos.com.ec"]
    assert cfg["host"] == "smtp.x.local"
    assert cfg["password"] == ""
    assert "Prueba de configuración" in message["Subject"]


def test_send_test_devuelve_error_smtp(monkeypatch):
    def fail(m, r, cfg):
        raise smtplib.SMTPAuthenticationError(535, b"5.7.3 Authentication unsuccessful")

    monkeypatch.setattr(app_module, "send_via_smtp", fail)

    r = client.post("/send-test", json=BODY)

    assert r.status_code == 200
    assert r.json()["ok"] is False
    assert "535" in r.json()["error"]
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `.\.venv\Scripts\python -m pytest tests/test_send_test.py -v`
Expected: FAIL con 404 en `/send-test`.

- [ ] **Step 3: Implementar**

En `app.py`, al final del archivo:

```python
class SendTestRequest(BaseModel):
    smtp: SmtpSettings
    recipient: EmailStr


class SendTestResponse(BaseModel):
    ok: bool
    error: Optional[str] = None


TEST_SUBJECT = "Prueba de configuración de correo — CapacitaDOS"


@app.post("/send-test", response_model=SendTestResponse)
def send_test(request: SendTestRequest) -> SendTestResponse:
    """Envía un correo de prueba con la configuración recibida (sin reintentos, sin reglas)."""
    cfg = smtp_settings_to_cfg(request.smtp)
    from_header = format_from_header(cfg)
    html_body = render_template("prueba_configuracion", {"remitente": from_header, "servidor": f"{cfg['host']}:{cfg['port']}"})
    to = [str(request.recipient)]
    message = build_message(from_header, to, TEST_SUBJECT, html_body, [], None)
    try:
        send_via_smtp(message, to, cfg)
    except (smtplib.SMTPException, OSError) as exc:
        log.warning("Correo de prueba falló: %s", exc)
        return SendTestResponse(ok=False, error=str(exc))
    return SendTestResponse(ok=True)
```

`plantillas/prueba_configuracion.html` (sigue el estilo simple de las demás plantillas; usa `logo_src` como ellas):

```html
<!DOCTYPE html>
<html lang="es">
  <head>
    <meta charset="UTF-8" />
    <title>Prueba de configuración de correo</title>
  </head>
  <body style="margin:0;padding:24px;background:#f3f4f6;font-family:Arial,Helvetica,sans-serif;color:#111827;">
    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:8px;">
      <tr>
        <td style="padding:24px;text-align:center;">
          {% if logo_src %}<img src="{{ logo_src }}" alt="DOS" style="max-height:56px;" />{% endif %}
        </td>
      </tr>
      <tr>
        <td style="padding:0 24px 24px;">
          <h2 style="margin:0 0 12px;font-size:20px;">La configuración de correo funciona</h2>
          <p style="margin:0 0 8px;">Este es un correo de prueba enviado desde <strong>Configuración → Correo</strong> de CapacitaDOS.</p>
          <p style="margin:0 0 8px;">Remitente: <strong>{{ remitente }}</strong></p>
          <p style="margin:0;">Servidor: <strong>{{ servidor }}</strong></p>
        </td>
      </tr>
    </table>
  </body>
</html>
```

`Dockerfile`: después de `COPY app.py .` agregar `COPY config_provider.py .`

`docker-compose.yml`:
- En `backend.environment` (junto a `MailSender__BaseUrl`):

```yaml
      CORREO_ENCRYPTION_KEY: ${CORREO_ENCRYPTION_KEY}
      MAIL_CONFIG_API_KEY: ${MAIL_CONFIG_API_KEY}
```

- En `mail_sender.environment` (después de `LOGO_URL`):

```yaml
      # Configuración administrada desde la app (Configuración → Correo). Si falta
      # alguna, mail_sender usa solo las SMTP_* de arriba.
      BACKEND_INTERNAL_URL: http://capacitaciones-backend:8080
      MAIL_CONFIG_API_KEY: ${MAIL_CONFIG_API_KEY}
```

- Confirmar que `mail_sender` y `backend` comparten la red `capacitaciones-net`.

`.env.example`, después de `SMTP_USE_TLS=true`:

```bash

# --- Configuración de correo desde la app (Configuración → Correo) ---
# Cuando un admin guarda la configuración en la app, mail_sender la usa en lugar
# de las SMTP_* de arriba (que quedan como respaldo si el backend no responde o
# si nunca se configuró nada).
# CORREO_ENCRYPTION_KEY = llave AES-256 (32 bytes en base64) con la que el backend
#   cifra la contraseña SMTP en la BD. Generar con:
#   python -c "import os,base64;print(base64.b64encode(os.urandom(32)).decode())"
#   Si se pierde, basta con volver a escribir la contraseña en la pantalla.
# MAIL_CONFIG_API_KEY   = clave compartida backend ↔ mail_sender (header X-Internal-Key).
#   Generar con: python -c "import secrets;print(secrets.token_urlsafe(32))"
CORREO_ENCRYPTION_KEY=
MAIL_CONFIG_API_KEY=
```

`documentacion.md`, sección 3 (SMTP): documentar `SMTP_FROM_NAME`, `SMTP_USER`, `BACKEND_INTERNAL_URL`, `MAIL_CONFIG_API_KEY`, `CONFIG_CACHE_SECONDS`; la precedencia (BD → `.env`); el comportamiento `{"status": "omitido"}`; y el endpoint `POST /send-test` con su body y respuesta.

- [ ] **Step 4: Correr todos los tests de mail_sender**

Run: `.\.venv\Scripts\python -m pytest -v`
Expected: 13 PASS.

- [ ] **Step 5: Commit**

```bash
git add mail_sender docker-compose.yml .env.example
git commit -m "feat(mail_sender): endpoint de correo de prueba y variables de configuración"
```

---

### Task 10: Frontend — servicio y pestaña "Servidor y remitente"

**Files:**
- Modify: `frontend/src/services/configuracion.js`
- Create: `frontend/src/pages/configuracion/CorreoPage.jsx`
- Create: `frontend/src/pages/configuracion/CorreoServidorTab.jsx`
- Modify: `frontend/src/App.jsx:21,121-123`
- Modify: `frontend/src/components/Sidebar/Sidebar.jsx:3-20,294-305`

**Interfaces:**
- Consumes: endpoints de la Task 6 (JSON camelCase).
- Produces: `configuracionService.getCorreo()`, `.updateCorreo(payload)`, `.probarCorreo(payload)`, `.getNotificaciones()`, `.updateNotificaciones(lista)`; ruta `/configuracion/correo`.

> El frontend no tiene runner de tests. La verificación es `npm run lint`, `npm run build` y la prueba manual con `npm run dev` contra el backend local (o los pasos manuales de la Task 14).

- [ ] **Step 1: Servicio**

En `services/configuracion.js`, actualizar el comentario del encabezado con los endpoints nuevos y agregar antes del `export default`:

```js
const CORREO = '/configuracion/correo';

/** Configuración SMTP (nunca trae la contraseña; `tienePassword` indica si hay una guardada). */
export function getCorreo() {
  return http.get(CORREO);
}

/**
 * Guarda la configuración SMTP.
 * @param {object} payload - { smtpHost, smtpPort, smtpUser, password, quitarPassword, usarTls,
 *                             remitenteCorreo, remitenteNombre, ccGlobal, bccGlobal }.
 *                           `password` vacío = conservar la guardada.
 */
export function updateCorreo(payload) {
  return http.put(CORREO, payload);
}

/** Envía un correo de prueba con los datos del formulario al admin logueado. -> { ok, mensaje } */
export function probarCorreo(payload) {
  return http.post(`${CORREO}/prueba`, payload);
}

/** Lista de avisos: [{ plantilla, nombre, activo, asuntoPersonalizado, asuntoActual, variables }]. */
export function getNotificaciones() {
  return http.get(`${CORREO}/notificaciones`);
}

/** @param {Array<{plantilla: string, activo: boolean, asuntoPersonalizado: string|null}>} lista */
export function updateNotificaciones(lista) {
  return http.put(`${CORREO}/notificaciones`, lista);
}
```

y ampliar el `export default` con `getCorreo, updateCorreo, probarCorreo, getNotificaciones, updateNotificaciones`.

- [ ] **Step 2: `CorreoServidorTab.jsx`**

```jsx
import { useCallback, useEffect, useState } from 'react';
import { Save, Send, Info, AlertTriangle } from 'lucide-react';
import configuracionService from '../../services/configuracion.js';
import TextField from '../../components/Forms/TextField.jsx';
import Toggle from '../../components/Forms/Toggle.jsx';
import Modal from '../../components/Modal/Modal.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';

const EMPTY = {
  smtpHost: '', smtpPort: '587', smtpUser: '', password: '', quitarPassword: false,
  usarTls: true, remitenteCorreo: '', remitenteNombre: '', ccGlobal: '', bccGlobal: '',
};

function toForm(dto) {
  return {
    ...EMPTY,
    smtpHost: dto?.smtpHost || '',
    smtpPort: String(dto?.smtpPort ?? 587),
    smtpUser: dto?.smtpUser || '',
    usarTls: dto?.usarTls ?? true,
    remitenteCorreo: dto?.remitenteCorreo || '',
    remitenteNombre: dto?.remitenteNombre || '',
    ccGlobal: dto?.ccGlobal || '',
    bccGlobal: dto?.bccGlobal || '',
  };
}

function toPayload(form) {
  return {
    smtpHost: form.smtpHost.trim(),
    smtpPort: Number(form.smtpPort),
    smtpUser: form.smtpUser.trim() || null,
    password: form.quitarPassword ? null : (form.password || null),
    quitarPassword: form.quitarPassword,
    usarTls: form.usarTls,
    remitenteCorreo: form.remitenteCorreo.trim(),
    remitenteNombre: form.remitenteNombre.trim() || null,
    ccGlobal: form.ccGlobal.trim() || null,
    bccGlobal: form.bccGlobal.trim() || null,
  };
}

/** Mapea `errores` del backend (claves PascalCase del DTO) a las claves del formulario. */
function mapErrores(errores) {
  const out = {};
  Object.entries(errores || {}).forEach(([k, v]) => {
    out[k.charAt(0).toLowerCase() + k.slice(1)] = v;
  });
  return out;
}

/** Pestaña: servidor SMTP, remitente y copias globales. */
export default function CorreoServidorTab() {
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState(null);
  const [form, setForm] = useState(EMPTY);
  const [errors, setErrors] = useState({});
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState(null);
  const [confirmOpen, setConfirmOpen] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const response = await configuracionService.getCorreo();
      setData(response);
      setForm(toForm(response));
      setErrors({});
    } catch (error) {
      toast.error(error?.message || 'No se pudo cargar la configuración de correo.');
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const set = (field) => (value) => setForm((f) => ({ ...f, [field]: value }));

  const handleError = (error, fallback) => {
    if (error instanceof HttpError && error.status === 400) {
      setErrors(mapErrores(error?.body?.errores));
      toast.error(error?.body?.message || fallback);
    } else {
      toast.error(error?.body?.message || error?.message || fallback);
    }
  };

  const doSave = async () => {
    setConfirmOpen(false);
    setSaving(true);
    try {
      await configuracionService.updateCorreo(toPayload(form));
      toast.success('Configuración de correo guardada. Se aplica en menos de un minuto.');
      await fetchData();
    } catch (error) {
      handleError(error, 'No se pudo guardar la configuración.');
    } finally {
      setSaving(false);
    }
  };

  const doTest = async () => {
    setTesting(true);
    setTestResult(null);
    setErrors({});
    try {
      const result = await configuracionService.probarCorreo(toPayload(form));
      setTestResult(result);
    } catch (error) {
      handleError(error, 'No se pudo enviar el correo de prueba.');
    } finally {
      setTesting(false);
    }
  };

  if (loading) {
    return (
      <div style={{ padding: 'var(--spacing-6)', display: 'flex', justifyContent: 'center' }}>
        <Spinner size={32} label="Cargando configuración..." />
      </div>
    );
  }

  const busy = saving || testing;
  const passwordPlaceholder = data?.tienePassword ? '•••• (sin cambios)' : '';

  return (
    <div>
      {data && !data.configurado && (
        <div className="alert alert--info" role="status" style={{ marginBottom: 'var(--spacing-4)' }}>
          <Info className="alert__icon" width={20} height={20} />
          <div className="alert__content">
            <div className="alert__message">
              Actualmente se usa la configuración del servidor (.env). Al guardar, esta configuración la reemplaza.
            </div>
          </div>
        </div>
      )}
      {data?.passwordInvalida && (
        <div className="alert alert--warning" role="alert" style={{ marginBottom: 'var(--spacing-4)' }}>
          <AlertTriangle className="alert__icon" width={20} height={20} />
          <div className="alert__content">
            <div className="alert__message">
              La contraseña guardada no se puede leer. Vuelve a ingresarla; mientras tanto se usa la del servidor (.env).
            </div>
          </div>
        </div>
      )}

      <form onSubmit={(e) => { e.preventDefault(); if (!busy) setConfirmOpen(true); }} noValidate>
        <div className="card" style={{ marginBottom: 'var(--spacing-4)' }}>
          <div className="card__header">
            <h3 className="card__title">Servidor SMTP</h3>
          </div>
          <div className="card__body" style={{ display: 'grid', gap: 'var(--spacing-3)', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))' }}>
            <TextField label="Servidor" name="smtpHost" value={form.smtpHost} onChange={set('smtpHost')}
              placeholder="smtp.office365.com" error={errors.smtpHost} disabled={busy} required />
            <TextField label="Puerto" name="smtpPort" type="number" value={form.smtpPort} onChange={set('smtpPort')}
              placeholder="587" error={errors.smtpPort} disabled={busy} required />
            <TextField label="Usuario (opcional)" name="smtpUser" value={form.smtpUser} onChange={set('smtpUser')}
              helper="Si se deja vacío se usa el correo del remitente." error={errors.smtpUser} disabled={busy} />
            <TextField label="Contraseña" name="password" type="password" value={form.password} onChange={set('password')}
              placeholder={passwordPlaceholder} autoComplete="new-password"
              helper={data?.tienePassword ? 'Déjala vacía para conservar la actual.' : undefined}
              disabled={busy || form.quitarPassword} />
            <Toggle label="Sin contraseña (relay)" name="quitarPassword" checked={form.quitarPassword}
              onChange={set('quitarPassword')} disabled={busy} />
            <Toggle label="Usar TLS (STARTTLS)" name="usarTls" checked={form.usarTls}
              onChange={set('usarTls')} disabled={busy} />
          </div>
        </div>

        <div className="card" style={{ marginBottom: 'var(--spacing-4)' }}>
          <div className="card__header">
            <h3 className="card__title">Remitente y copias</h3>
            <p className="card__subtitle">Las copias se agregan a todas las notificaciones. Separa varios correos con coma.</p>
          </div>
          <div className="card__body" style={{ display: 'grid', gap: 'var(--spacing-3)', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))' }}>
            <TextField label="Correo del remitente" name="remitenteCorreo" value={form.remitenteCorreo}
              onChange={set('remitenteCorreo')} placeholder="capacitaciones@dos.com.ec"
              error={errors.remitenteCorreo} disabled={busy} required />
            <TextField label="Nombre del remitente" name="remitenteNombre" value={form.remitenteNombre}
              onChange={set('remitenteNombre')} placeholder="CapacitaDOS" error={errors.remitenteNombre} disabled={busy} />
            <TextField label="CC global" name="ccGlobal" value={form.ccGlobal} onChange={set('ccGlobal')}
              placeholder="talento@dos.com.ec" error={errors.ccGlobal} disabled={busy} />
            <TextField label="BCC global" name="bccGlobal" value={form.bccGlobal} onChange={set('bccGlobal')}
              error={errors.bccGlobal} disabled={busy} />
          </div>
        </div>

        {testResult && (
          <div className={`alert ${testResult.ok ? 'alert--success' : 'alert--danger'}`} role="status"
            style={{ marginBottom: 'var(--spacing-4)' }}>
            <div className="alert__content">
              <div className="alert__title">{testResult.ok ? 'Prueba exitosa' : 'La prueba falló'}</div>
              <div className="alert__message">{testResult.mensaje}</div>
            </div>
          </div>
        )}

        <div className="form-actions">
          <div className="form-actions__right" style={{ display: 'flex', gap: 'var(--spacing-2)' }}>
            <button type="button" className="btn btn--ghost" onClick={doTest} disabled={busy}>
              {testing ? <Spinner size={14} label="Enviando..." /> : <Send width={16} height={16} />}
              <span style={{ marginLeft: 8 }}>{testing ? 'Enviando...' : 'Enviar correo de prueba'}</span>
            </button>
            <button type="submit" className="btn btn--primary" disabled={busy}>
              {saving ? <Spinner size={14} label="Guardando..." /> : <Save width={16} height={16} />}
              <span style={{ marginLeft: 8 }}>{saving ? 'Guardando...' : 'Guardar'}</span>
            </button>
          </div>
        </div>
      </form>

      <Modal
        isOpen={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        title="Confirmar cambios de correo"
        footer={(
          <>
            <button type="button" className="btn btn--ghost" onClick={() => setConfirmOpen(false)}>Cancelar</button>
            <button type="button" className="btn btn--primary" onClick={doSave}>Guardar</button>
          </>
        )}
      >
        <p>
          Todas las notificaciones empezarán a salir con esta configuración en menos de un minuto.
          Te recomendamos enviar un correo de prueba antes de guardar.
        </p>
      </Modal>
    </div>
  );
}
```

> Verificar en `frontend/src/styles` (o donde esté el design system) que existen las clases `alert--info`, `alert--success` y `alert--danger`. Si alguna no existe, usar la más cercana que sí exista (`alert--warning` está confirmada) y anotarlo en el commit.

- [ ] **Step 3: `CorreoPage.jsx` (con la pestaña de notificaciones como marcador provisional)**

```jsx
import { useState } from 'react';
import { Server, BellRing } from 'lucide-react';
import CorreoServidorTab from './CorreoServidorTab.jsx';

/**
 * Configuración de correo: servidor/remitente/copias y reglas por tipo de notificación.
 * mail_sender toma los cambios en menos de un minuto (cache de 60 s).
 */
export default function CorreoPage() {
  const [tab, setTab] = useState('servidor');

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Configuración de correo</h1>
          <p className="page-header__subtitle">
            Cuenta desde la que salen las notificaciones, qué avisos se envían y con qué asunto.
          </p>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 'var(--spacing-2)', marginBottom: 'var(--spacing-4)', flexWrap: 'wrap' }}>
        {[
          { id: 'servidor', label: 'Servidor y remitente', icon: Server },
          { id: 'notificaciones', label: 'Notificaciones', icon: BellRing },
        ].map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            type="button"
            className={`btn ${tab === id ? 'btn--primary' : 'btn--ghost'}`}
            onClick={() => setTab(id)}
            aria-pressed={tab === id}
          >
            <Icon width={16} height={16} />
            <span>{label}</span>
          </button>
        ))}
      </div>

      {tab === 'servidor' && <CorreoServidorTab />}
      {tab === 'notificaciones' && <p className="text-secondary">Disponible en el siguiente paso.</p>}
    </div>
  );
}
```

- [ ] **Step 4: Ruta y menú**

`App.jsx`: junto al import de `NumeracionPage` agregar `import CorreoPage from './pages/configuracion/CorreoPage.jsx';`, en el comentario de rutas agregar `*   /configuracion/correo        → Configuración de correo`, y después de la ruta de numeración:

```jsx
          <Route path="/configuracion/correo" element={<CorreoPage />} />
```

`Sidebar.jsx`: agregar `Mail,` a los imports de `lucide-react` y, entre los NavLink de Numeración y Usuarios:

```jsx
                <li className="sidebar__nav-item">
                  <NavLink to="/configuracion/correo" title="Correo" className={navLinkClass}>
                    <Mail className="sidebar__nav-icon" />
                    <span>Correo</span>
                  </NavLink>
                </li>
```

- [ ] **Step 5: Verificar**

Run: `cd frontend; npm run lint; npm run build`
Expected: sin errores nuevos respecto a la línea base de la Task 0.

Manual (si hay backend local): `npm run dev` → entrar como admin → Configuración → Correo. Debe aparecer el banner ".env"; al guardar datos válidos desaparece; un correo inválido marca el campo en rojo.

- [ ] **Step 6: Commit**

```bash
git add frontend/src
git commit -m "feat(correo): pantalla Configuración → Correo con servidor, remitente, copias y prueba"
```

---

### Task 11: Frontend — pestaña "Notificaciones"

**Files:**
- Create: `frontend/src/pages/configuracion/CorreoNotificacionesTab.jsx`
- Modify: `frontend/src/pages/configuracion/CorreoPage.jsx`

**Interfaces:**
- Consumes: `configuracionService.getNotificaciones()` / `.updateNotificaciones(lista)` (Task 10).

- [ ] **Step 1: Implementar la pestaña**

```jsx
import { useCallback, useEffect, useState } from 'react';
import { Save } from 'lucide-react';
import configuracionService from '../../services/configuracion.js';
import Toggle from '../../components/Forms/Toggle.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';

/** Pestaña: activar/desactivar cada notificación y personalizar su asunto. */
export default function CorreoNotificacionesTab() {
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [items, setItems] = useState([]);
  const [saving, setSaving] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const response = await configuracionService.getNotificaciones();
      setItems((response || []).map((n) => ({ ...n, asuntoPersonalizado: n.asuntoPersonalizado || '' })));
    } catch (error) {
      toast.error(error?.message || 'No se pudieron cargar las notificaciones.');
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const update = (plantilla, patch) =>
    setItems((list) => list.map((n) => (n.plantilla === plantilla ? { ...n, ...patch } : n)));

  const doSave = async () => {
    setSaving(true);
    try {
      await configuracionService.updateNotificaciones(items.map((n) => ({
        plantilla: n.plantilla,
        activo: n.activo,
        asuntoPersonalizado: n.asuntoPersonalizado.trim() || null,
      })));
      toast.success('Notificaciones actualizadas. Se aplican en menos de un minuto.');
      await fetchData();
    } catch (error) {
      toast.error(error?.body?.message || error?.message || 'No se pudieron guardar las notificaciones.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div style={{ padding: 'var(--spacing-6)', display: 'flex', justifyContent: 'center' }}>
        <Spinner size={32} label="Cargando notificaciones..." />
      </div>
    );
  }

  return (
    <div className="card">
      <div className="card__header">
        <h3 className="card__title">Notificaciones</h3>
        <p className="card__subtitle">
          Un aviso desactivado no se envía (y no se reenvía al reactivarlo). Deja el asunto vacío para usar el de
          siempre; puedes usar variables como <code>{'{{ tema }}'}</code>.
        </p>
      </div>
      <div className="card__body" style={{ display: 'grid', gap: 'var(--spacing-4)' }}>
        {items.map((n) => (
          <div key={n.plantilla} style={{ display: 'grid', gap: 'var(--spacing-2)', paddingBottom: 'var(--spacing-3)', borderBottom: '1px solid var(--color-border, #e5e7eb)' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 'var(--spacing-3)' }}>
              <strong>{n.nombre}</strong>
              <Toggle
                label=""
                ariaLabel={`Activar ${n.nombre}`}
                name={`activo-${n.plantilla}`}
                checked={n.activo}
                onChange={(value) => update(n.plantilla, { activo: value })}
                disabled={saving}
              />
            </div>
            <input
              className="form-input"
              aria-label={`Asunto de ${n.nombre}`}
              value={n.asuntoPersonalizado}
              onChange={(e) => update(n.plantilla, { asuntoPersonalizado: e.target.value })}
              placeholder={n.asuntoActual}
              maxLength={500}
              disabled={saving || !n.activo}
            />
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              {n.variables.map((v) => (
                <code key={v} className="text-xs" style={{ background: 'var(--color-bg-main, #f3f4f6)', padding: '2px 6px', borderRadius: 4 }}>
                  {`{{ ${v} }}`}
                </code>
              ))}
            </div>
          </div>
        ))}
        <div className="form-actions">
          <div className="form-actions__right">
            <button type="button" className="btn btn--primary" onClick={doSave} disabled={saving}>
              {saving ? <Spinner size={14} label="Guardando..." /> : <Save width={16} height={16} />}
              <span style={{ marginLeft: 8 }}>{saving ? 'Guardando...' : 'Guardar cambios'}</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Conectar la pestaña**

En `CorreoPage.jsx`: `import CorreoNotificacionesTab from './CorreoNotificacionesTab.jsx';` y reemplazar el marcador provisional por `{tab === 'notificaciones' && <CorreoNotificacionesTab />}`.

- [ ] **Step 3: Verificar**

Run: `npm run lint; npm run build`
Expected: sin errores nuevos.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/pages/configuracion
git commit -m "feat(correo): pestaña de notificaciones con activación y asuntos personalizados"
```

---

### Task 12: Frontend — estado "Omitido" en certificados

**Files:**
- Modify: `frontend/src/pages/asistentes/AsistentesPage.jsx` (`renderEstadoEnvio`, ~línea 390-420, y el comentario ~línea 471)
- Modify: `frontend/src/pages/asistentes/AsistentesPage.module.css` (después de `.badgeCertError`)

- [ ] **Step 1: Implementar**

CSS:

```css
.badgeCertSkipped {
  background-color: var(--color-bg-main, #f3f4f6);
  color: var(--color-text-secondary, #6b7280);
  cursor: help;
}
```

En `renderEstadoEnvio`, después del bloque `if (estado === 'Error') {...}`:

```jsx
    if (estado === 'Omitido') {
      return (
        <span
          className={`${styles.badge} ${styles.badgeCertSkipped}`}
          title="No se envió: el aviso 'Certificado al participante' está desactivado en Configuración → Correo."
        >
          Omitido
        </span>
      );
    }
```

Actualizar el comentario `"Enviado" | "Pendiente" | "Error" | sin envío.` a `"Enviado" | "Pendiente" | "Error" | "Omitido" | sin envío.`

- [ ] **Step 2: Verificar y commit**

Run: `npm run lint; npm run build` → sin errores nuevos.

```bash
git add frontend/src/pages/asistentes
git commit -m "feat(certificados): muestra el estado Omitido cuando el aviso está desactivado"
```

---

### Task 13: Revisión final en la rama

- [ ] **Step 1: Todas las suites**

```powershell
cd backend; dotnet test
cd ..\mail_sender; .\.venv\Scripts\python -m pytest -v
cd ..\frontend; npm run lint; npm run build
```

Expected: backend todo PASS; mail_sender 13 PASS; frontend build OK.

- [ ] **Step 2: Cobertura del spec**

Recorrer las secciones 4–7 del spec y marcar cada requisito contra lo implementado. Cualquier diferencia se corrige o se anota para el usuario antes de desplegar.

- [ ] **Step 3: Pedir revisión de código**

Usar superpowers:requesting-code-review sobre `git diff main...feature/configuracion-correo`.

---

### Task 14: Despliegue a producción (10.1.1.174) — cada paso con confirmación del usuario

Conexión: `plink -batch -ssh -hostkey SHA256:j67scC3q77hZw3E8soPUowFf2/+NNDOZ+0KniU8TSe0 -pw <password> iados@10.1.1.174 "<cmd>"`. Transferencia de archivos: `pscp` con las mismas opciones. `P=/Proyectos/RegistroCapacitaciones`. `TS=$(date +%Y%m%d-%H%M%S)`.

- [ ] **Step 1: Comparar producción con el repositorio (solo lectura)**

Copiar a local `backend/`, `mail_sender/`, `docker-compose.yml` y `frontend/src` de producción (`pscp -r`) a la carpeta de scratchpad y comparar con `main` (no con la rama): `git diff --no-index <scratch>/backend backend`, etc. Mostrar al usuario cualquier diferencia. **Si producción tiene cambios que no están en `main`, detenerse y decidir con el usuario** antes de seguir.

- [ ] **Step 2: Respaldos** *(confirmar con el usuario)*

Los directorios son de `iados`; no hace falta root. Cada `plink` es una sesión nueva: fijar `TS` una sola vez (ej. `20260924-0900`) y escribirlo literal en todos los comandos de este paso y del rollback.

```bash
tar czf /tmp/capacitaciones-$TS.tgz -C /Proyectos RegistroCapacitaciones --exclude=RegistroCapacitaciones/output
tar czf /tmp/web-capacitados-$TS.tgz -C /Docker/web/html capacitados
cp /Docker/web/conf/locations-capacitados.inc /Docker/web/conf/locations-capacitados.inc.bak.$TS
docker exec capacitaciones-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "BACKUP DATABASE [<db>] TO DISK='/var/opt/mssql/backup/<db>-$TS.bak'"
```

Antes de ejecutar el backup de BD: leer el nombre de la base y la variable de la contraseña de `sa` en `$P/.env` / `docker-compose.yml`, y confirmar la ruta de `sqlcmd` (`mssql-tools18` o `mssql-tools`) con `docker exec capacitaciones-sqlserver ls /opt`. Verificar que los tres archivos existan y tengan tamaño > 0.

- [ ] **Step 3: Subir el código** *(confirmar)*

Copiar con `pscp -r` solo lo que cambia: `backend/src`, `mail_sender/app.py`, `mail_sender/config_provider.py`, `mail_sender/Dockerfile`, `mail_sender/plantillas/prueba_configuracion.html`, `docker-compose.yml`. No copiar `.env`, `output/`, `repository/`, `imagen_capacitaciones/` ni `convenios_anexos/`.

- [ ] **Step 4: Variables en `.env`** *(confirmar)*

Generar localmente dos valores (`python -c "import os,base64;print(base64.b64encode(os.urandom(32)).decode())"` y `python -c "import secrets;print(secrets.token_urlsafe(32))"`) y agregarlos al final de `$P/.env` como `CORREO_ENCRYPTION_KEY=` y `MAIL_CONFIG_API_KEY=`. No modificar las `SMTP_*`. No mostrar los valores en el chat.

- [ ] **Step 5: Reconstruir solo backend y mail_sender** *(confirmar)*

```bash
cd $P && docker compose up -d --build backend mail_sender
docker logs --tail 50 capacitaciones-backend   # buscar "Migraciones aplicadas correctamente."
docker logs --tail 20 capacitaciones-mail-sender
```

- [ ] **Step 6: Frontend** *(confirmar)*

Local: `cd frontend; npm run build`. Subir `dist/*` a `/Docker/web/html/capacitados/` con `pscp -r` (reemplaza `index.html` y agrega los nuevos `assets/`).

- [ ] **Step 7: Bloquear `/api/internal/` en nginx** *(confirmar)*

En `/Docker/web/conf/locations-capacitados.inc`, **antes** de `location /api/ {`:

```nginx
# Endpoints internos (mail_sender ↔ backend): nunca expuestos fuera de Docker.
location ^~ /api/internal/ {
    return 403;
}
```

```bash
docker exec web-nginx nginx -t && docker exec web-nginx nginx -s reload
```

- [ ] **Step 8: Verificación**

1. `curl -s -o /dev/null -w "%{http_code}" https://capacitados.dos.com.ec/api/internal/correo-config` → `403`.
2. `docker exec capacitaciones-mail-sender python -c "import config_provider as c; print(bool(c.get_remote_config()))"` → `True`.
3. En la app: Configuración → Correo muestra el banner ".env" (todavía sin guardar) y la pestaña Notificaciones lista 9 avisos.
4. Con el usuario: cargar los datos del `.env` actual en la pantalla, **Enviar correo de prueba** → llega; **Guardar**.
5. Revisar `docker logs -f capacitaciones-mail-sender` y `capacitaciones-event-monitor` por 5 minutos: sin errores nuevos.

- [ ] **Step 9: Rollback (solo si algo falla)**

```bash
cd /Proyectos && tar xzf /tmp/capacitaciones-$TS.tgz
cd $P && docker compose up -d --build backend mail_sender
cd /Docker/web/html && rm -rf capacitados && tar xzf /tmp/web-capacitados-$TS.tgz
cp /Docker/web/conf/locations-capacitados.inc.bak.$TS /Docker/web/conf/locations-capacitados.inc && docker exec web-nginx nginx -s reload
```

Las tablas nuevas quedan en la BD sin afectar a nada; no hace falta revertir la migración.
