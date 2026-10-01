# Registro de personas inscritas y reuso de firma — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Guardar un registro por cédula de quien se inscribe (con su firma), autocompletar el formulario público de Inscripción al escribir la cédula y permitir reusar la firma guardada sin exponerla.

**Architecture:** Nueva entidad `PersonaInscrita` (tabla `dbo.PersonaInscrita`, única por `Identificacion`) con backfill desde `dbo.Asistente`. `InscribirAsistenteUseCase` resuelve la firma (nueva o registrada) y deja la persona "staged" en el mismo `AppDbContext` scoped, de modo que el `SaveChanges` de `AsistenteRepository.AddAsync` persiste asistente + persona atómicamente. Endpoint público nuevo `GET /api/inscripcion/capacitacion/persona/{identificacion}` (policy `Inscripcion`) que nunca devuelve la firma.

**Tech Stack:** .NET 8, EF Core 8.0.10 (SQL Server; InMemory en tests), xUnit, React 18 + Vite, CSS del design system `./style/`.

**Spec:** `docs/superpowers/specs/2026-09-30-persona-inscrita-design.md`

## Global Constraints

- Rama: `feature/persona-inscrita` (ya creada desde `main`). No mezclar con `feature/configuracion-correo`.
- Arquitectura hexagonal: Domain ← Application (puertos + casos de uso) ← Infrastructure (EF) / Api. Application no referencia EF.
- `Identificacion`: `Trim()`, **sin** normalizar mayúsculas. Máx. 30. `Nombres`/`Apellidos` máx. 120. `EmailUsuario` máx. 255, persistido con sufijo `@dos.com.ec`.
- La API pública **nunca** devuelve la firma; solo `tieneFirma`.
- `emailUsuario` en la respuesta de búsqueda va **sin** `@dos.com.ec`.
- Frontend: solo clases de `./style/` y el CSS module existente; nada de librerías de estilo nuevas. Llamadas públicas vía `requestWithToken` de `services/inscripcion.js` (no `http.js`).
- Textos de UI en español, exactamente:
  - "Encontramos tus datos. Revísalos antes de inscribirte."
  - "Usar mi firma registrada"
  - "No encontramos una firma registrada para esta identificación. Dibuja o sube tu firma."
- Commits terminan con `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`.
- **Baseline de pruebas:** en `main` el suite completo da **31 fallas preexistentes** (login admin de `InMemoryWebAppFactory` → 401 y 5 tests unitarios de responsables/capacitador). No se arreglan en este plan. Cada tarea corre sus pruebas con `--filter`; la verificación final exige que el total de fallas **no suba de 31**.

Comandos (desde `C:\Capacitados\capacitaciones\backend`):
- Pruebas filtradas: `dotnet test --nologo --filter "FullyQualifiedName~<Clase>"`
- Suite completo: `dotnet test --nologo`

## Review Focus

1. **Cambiar la cédula después de un autocompletado** → la casilla "Usar mi firma registrada" debe desaparecer; si no, se inscribe a la persona B con la firma de A. Cubierto en Task 6 (lógica de `onChange`) y en la verificación manual.
2. **`usarFirmaRegistrada = true` con una firma nueva también enviada** → gana la firma registrada y la guardada **no** se reemplaza. Test en Task 3.
3. **Área guardada que hoy está inactiva** → la búsqueda devuelve `areaId: null` y el formulario no preselecciona nada. Test en Task 4; lógica en Task 6.
4. **Inscripción duplicada a la misma capacitación** → no modifica la persona (nombres/firma quedan como estaban). Test en Task 3.
5. **Respuesta JSON de búsqueda** → no debe contener ninguna propiedad `firma` (ni siquiera `null`). Test de endpoint en Task 5.

---

## File Structure

**Crear**
- `backend/src/Capacitaciones.Domain/Entities/PersonaInscrita.cs` — entidad.
- `backend/src/Capacitaciones.Application/Ports/IPersonaInscritaRepository.cs` — puerto.
- `backend/src/Capacitaciones.Application/Dtos/Inscripcion/PersonaInscritaLookupDto.cs` — respuesta de búsqueda.
- `backend/src/Capacitaciones.Application/UseCases/Inscripcion/BuscarPersonaInscritaUseCase.cs` — caso de uso de búsqueda.
- `backend/src/Capacitaciones.Infrastructure/Persistence/Configurations/PersonaInscritaConfiguration.cs` — mapeo EF.
- `backend/src/Capacitaciones.Infrastructure/Persistence/Repositories/PersonaInscritaRepository.cs` — adaptador EF.
- `backend/src/Capacitaciones.Infrastructure/Persistence/Migrations/<timestamp>_AddPersonaInscrita.cs` (+ `.Designer.cs`, snapshot) — generada por `dotnet ef`, con backfill agregado a mano.
- `backend/tests/Capacitaciones.Tests/BuscarPersonaInscritaUseCaseTests.cs`
- `backend/tests/Capacitaciones.Tests/InscripcionPersonaEndpointTests.cs`

**Modificar**
- `backend/src/Capacitaciones.Application/Dtos/Inscripcion/CreateInscripcionDto.cs` — `UsarFirmaRegistrada`, `Firma` nullable.
- `backend/src/Capacitaciones.Application/UseCases/Inscripcion/InscripcionExceptions.cs` — 2 excepciones nuevas.
- `backend/src/Capacitaciones.Application/UseCases/Inscripcion/InscribirAsistenteUseCase.cs` — resolver firma + upsert persona.
- `backend/src/Capacitaciones.Infrastructure/Persistence/AppDbContext.cs` — `DbSet`.
- `backend/src/Capacitaciones.Infrastructure/Persistence/Repositories/AsistenteRepository.cs` — traducir choque del índice de persona.
- `backend/src/Capacitaciones.Api/Controllers/InscripcionController.cs` — endpoint + códigos.
- `backend/src/Capacitaciones.Api/Program.cs` — DI.
- `backend/tests/Capacitaciones.Tests/InscribirAsistenteUseCaseTests.cs` — nuevo parámetro + tests.
- `frontend/src/services/inscripcion.js` — `buscarPersona`, contrato.
- `frontend/src/pages/inscripcion/InscripcionPage.jsx` — autocompletado + casilla.
- `instrucciones.md` — documentar el módulo.

---

### Task 1: Entidad, puerto, excepciones y DTOs (Domain + Application)

Sin comportamiento nuevo todavía: tipos que las tareas siguientes consumen. Se valida compilando.

**Files:**
- Create: `backend/src/Capacitaciones.Domain/Entities/PersonaInscrita.cs`
- Create: `backend/src/Capacitaciones.Application/Ports/IPersonaInscritaRepository.cs`
- Create: `backend/src/Capacitaciones.Application/Dtos/Inscripcion/PersonaInscritaLookupDto.cs`
- Modify: `backend/src/Capacitaciones.Application/UseCases/Inscripcion/InscripcionExceptions.cs` (agregar al final)
- Modify: `backend/src/Capacitaciones.Application/Dtos/Inscripcion/CreateInscripcionDto.cs`

**Interfaces:**
- Produces:
  - `class PersonaInscrita { Guid Id; string Identificacion; string Nombres; string Apellidos; Guid? AreaId; Area? Area; string EmailUsuario; string? Firma; DateTime FechaCreacion; DateTime FechaActualizacion; }`
  - `interface IPersonaInscritaRepository { Task<PersonaInscrita?> GetByIdentificacionAsync(string identificacion, CancellationToken ct = default); void Agregar(PersonaInscrita entity); }`
  - `class PersonaInscritaLookupDto { string Nombres; string Apellidos; Guid? AreaId; string EmailUsuario; bool TieneFirma; }`
  - `FirmaRegistradaNoDisponibleException` (código `FIRMA_REGISTRADA_NO_DISPONIBLE`), `InscripcionConcurrenteException` (código `INSCRIPCION_CONCURRENTE`), ambas `: CapacitacionServiceException`.
  - `CreateInscripcionDto.Firma` pasa a `string?`; nuevo `bool UsarFirmaRegistrada`.

- [ ] **Step 1: Crear la entidad**

`backend/src/Capacitaciones.Domain/Entities/PersonaInscrita.cs`:

```csharp
namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Registro de una persona que se inscribió alguna vez por la página pública de inscripción,
/// independiente de cada capacitación. Clave natural: <see cref="Identificacion"/> (única).
///
/// Se crea o actualiza en cada inscripción (<c>InscribirAsistenteUseCase</c>) y alimenta el
/// autocompletado por cédula del formulario público. La <see cref="Firma"/> se reusa en nuevas
/// inscripciones pero <b>nunca</b> se devuelve por la API pública.
/// </summary>
public class PersonaInscrita
{
    public Guid Id { get; set; }

    /// <summary>Cédula o pasaporte, con Trim y sin normalizar mayúsculas (mismo criterio que <see cref="Asistente"/>).</summary>
    public string Identificacion { get; set; } = string.Empty;

    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;

    /// <summary>Última área usada. Nullable por si el área deja de existir en el catálogo.</summary>
    public Guid? AreaId { get; set; }
    public Area? Area { get; set; }

    /// <summary>Email completo ya con sufijo <c>@dos.com.ec</c>.</summary>
    public string EmailUsuario { get; set; } = string.Empty;

    /// <summary>Firma base64 (data URL). Null = sin firma guardada.</summary>
    public string? Firma { get; set; }

    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}
```

- [ ] **Step 2: Crear el puerto**

`backend/src/Capacitaciones.Application/Ports/IPersonaInscritaRepository.cs`:

```csharp
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de persistencia para <see cref="PersonaInscrita"/>.
///
/// Importante: este repositorio <b>no</b> llama a SaveChanges. <see cref="GetByIdentificacionAsync"/>
/// devuelve la entidad <i>tracked</i> y <see cref="Agregar"/> solo la deja pendiente; ambos cambios se
/// persisten en el mismo SaveChanges de <see cref="IAsistenteRepository.AddAsync"/> (mismo DbContext
/// por scope). Así la inscripción y la persona se guardan de forma atómica.
/// </summary>
public interface IPersonaInscritaRepository
{
    /// <summary>Busca por identificación exacta (ya trimeada por el caller). Entidad tracked.</summary>
    Task<PersonaInscrita?> GetByIdentificacionAsync(string identificacion, CancellationToken ct = default);

    /// <summary>Deja una persona nueva pendiente de guardar (sin SaveChanges).</summary>
    void Agregar(PersonaInscrita entity);
}
```

- [ ] **Step 3: Crear el DTO de búsqueda**

`backend/src/Capacitaciones.Application/Dtos/Inscripcion/PersonaInscritaLookupDto.cs`:

```csharp
namespace Capacitaciones.Application.Dtos.Inscripcion;

/// <summary>
/// Respuesta de <c>GET /api/inscripcion/capacitacion/persona/{identificacion}</c>.
/// No incluye la firma: solo <see cref="TieneFirma"/>.
/// </summary>
public class PersonaInscritaLookupDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;

    /// <summary>Área guardada si sigue activa; null en otro caso.</summary>
    public Guid? AreaId { get; set; }

    /// <summary>Solo la parte local del correo (sin <c>@dos.com.ec</c>).</summary>
    public string EmailUsuario { get; set; } = string.Empty;

    public bool TieneFirma { get; set; }
}
```

- [ ] **Step 4: Agregar las excepciones**

Al final de `backend/src/Capacitaciones.Application/UseCases/Inscripcion/InscripcionExceptions.cs`:

```csharp

/// <summary>
/// Se lanza cuando la inscripción pide usar la firma registrada pero la identificación no tiene
/// persona registrada o no tiene firma guardada. El controlador la traduce a 400.
/// </summary>
public class FirmaRegistradaNoDisponibleException : CapacitacionServiceException
{
    public FirmaRegistradaNoDisponibleException()
        : base("FIRMA_REGISTRADA_NO_DISPONIBLE", "No hay una firma registrada para esta identificación.")
    {
    }
}

/// <summary>
/// Se lanza cuando dos inscripciones simultáneas intentan crear la misma <c>PersonaInscrita</c>
/// (choque con <c>UX_PersonaInscrita_Identificacion</c>). No se guarda nada; el controlador responde
/// 409 y el cliente puede reintentar (la persona ya existirá y se actualizará).
/// </summary>
public class InscripcionConcurrenteException : CapacitacionServiceException
{
    public InscripcionConcurrenteException()
        : base("INSCRIPCION_CONCURRENTE", "Se registró otra inscripción con esta identificación al mismo tiempo. Vuelve a intentarlo.")
    {
    }
}
```

- [ ] **Step 5: Actualizar `CreateInscripcionDto`**

Reemplazar la propiedad `Firma` y su comentario por:

```csharp
    /// <summary>
    /// Firma base64 (data URL aceptada). Requerida salvo que <see cref="UsarFirmaRegistrada"/> sea true;
    /// en ese caso se ignora.
    /// </summary>
    public string? Firma { get; set; }

    /// <summary>
    /// true = usar la firma guardada en <c>PersonaInscrita</c> para esta identificación.
    /// </summary>
    public bool UsarFirmaRegistrada { get; set; }
```

Y en el `<summary>` de la clase cambiar "La <see cref="Firma"/> es base64 (data URL o cadena pura) y es requerida." por "La <see cref="Firma"/> es base64 (data URL o cadena pura); es requerida salvo que se use la firma registrada."

- [ ] **Step 6: Compilar**

Run: `dotnet build --nologo src/Capacitaciones.Api` (desde `backend`)
Expected: `Compilación correcta` / `Build succeeded`, 0 errores.

- [ ] **Step 7: Commit**

```bash
git add backend/src/Capacitaciones.Domain/Entities/PersonaInscrita.cs backend/src/Capacitaciones.Application/Ports/IPersonaInscritaRepository.cs backend/src/Capacitaciones.Application/Dtos/Inscripcion/PersonaInscritaLookupDto.cs backend/src/Capacitaciones.Application/UseCases/Inscripcion/InscripcionExceptions.cs backend/src/Capacitaciones.Application/Dtos/Inscripcion/CreateInscripcionDto.cs
git commit -m "feat(inscripcion): entidad PersonaInscrita, puerto y contratos

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Persistencia EF + migración con backfill (Infrastructure)

**Files:**
- Create: `backend/src/Capacitaciones.Infrastructure/Persistence/Configurations/PersonaInscritaConfiguration.cs`
- Create: `backend/src/Capacitaciones.Infrastructure/Persistence/Repositories/PersonaInscritaRepository.cs`
- Modify: `backend/src/Capacitaciones.Infrastructure/Persistence/AppDbContext.cs:37` (después del `DbSet<ConvenioNumeracion>`)
- Modify: `backend/src/Capacitaciones.Infrastructure/Persistence/Repositories/AsistenteRepository.cs` (`AddAsync`)
- Modify: `backend/src/Capacitaciones.Api/Program.cs:202` (registro del repo)
- Create (generado): `backend/src/Capacitaciones.Infrastructure/Persistence/Migrations/<timestamp>_AddPersonaInscrita.cs`, `.Designer.cs`; Modify: `AppDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: `PersonaInscrita`, `IPersonaInscritaRepository`, `InscripcionConcurrenteException` (Task 1).
- Produces: `AppDbContext.PersonasInscritas` (`DbSet<PersonaInscrita>`), `PersonaInscritaRepository : IPersonaInscritaRepository`, registro DI scoped, índice `UX_PersonaInscrita_Identificacion`.

- [ ] **Step 1: Configuración EF**

`backend/src/Capacitaciones.Infrastructure/Persistence/Configurations/PersonaInscritaConfiguration.cs`:

```csharp
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Capacitaciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de <see cref="PersonaInscrita"/>. Identificación única: es la clave natural
/// del autocompletado por cédula de la página pública de inscripción.
/// </summary>
public class PersonaInscritaConfiguration : IEntityTypeConfiguration<PersonaInscrita>
{
    public void Configure(EntityTypeBuilder<PersonaInscrita> builder)
    {
        builder.ToTable("PersonaInscrita", "dbo");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Identificacion).HasMaxLength(30).IsRequired();
        builder.Property(p => p.Nombres).HasMaxLength(120).IsRequired();
        builder.Property(p => p.Apellidos).HasMaxLength(120).IsRequired();
        builder.Property(p => p.EmailUsuario).HasMaxLength(255).IsRequired();

        // Firma: base64, sin límite (nvarchar(max)). Null = sin firma guardada.
        builder.Property(p => p.Firma);

        builder.Property(p => p.FechaCreacion).IsRequired();
        builder.Property(p => p.FechaActualizacion).IsRequired();

        // FK a Area: Restrict, igual que Asistente — no se borra un área referenciada.
        builder.HasOne(p => p.Area)
            .WithMany()
            .HasForeignKey(p => p.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Identificacion)
            .IsUnique()
            .HasDatabaseName("UX_PersonaInscrita_Identificacion");
    }
}
```

- [ ] **Step 2: DbSet**

En `AppDbContext.cs`, debajo de `public DbSet<ConvenioNumeracion> ConvenioNumeracion => Set<ConvenioNumeracion>();`:

```csharp
    public DbSet<PersonaInscrita> PersonasInscritas => Set<PersonaInscrita>();
```

- [ ] **Step 3: Repositorio**

`backend/src/Capacitaciones.Infrastructure/Persistence/Repositories/PersonaInscritaRepository.cs`:

```csharp
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capacitaciones.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador EF Core de <see cref="IPersonaInscritaRepository"/>. No llama a SaveChanges: los cambios
/// se persisten junto con el asistente en <see cref="AsistenteRepository.AddAsync"/> (mismo DbContext).
/// </summary>
public class PersonaInscritaRepository : IPersonaInscritaRepository
{
    private readonly AppDbContext _db;

    public PersonaInscritaRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<PersonaInscrita?> GetByIdentificacionAsync(string identificacion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(identificacion)) return Task.FromResult<PersonaInscrita?>(null);
        var normalized = identificacion.Trim();
        return _db.PersonasInscritas.FirstOrDefaultAsync(p => p.Identificacion == normalized, ct);
    }

    public void Agregar(PersonaInscrita entity)
    {
        _db.PersonasInscritas.Add(entity);
    }
}
```

- [ ] **Step 4: Traducir el choque del índice de persona en `AsistenteRepository.AddAsync`**

El método actual captura **cualquier** 2601/2627 como `InscripcionDuplicadaException`. Ahora el mismo `SaveChanges` también inserta la persona, así que hay que distinguirlo **antes**. Reemplazar `AddAsync` completo por:

```csharp
    public async Task AddAsync(Asistente entity, CancellationToken ct = default)
    {
        await _db.Asistentes.AddAsync(entity, ct);
        try
        {
            // Este SaveChanges también persiste la PersonaInscrita que el caso de uso dejó
            // pendiente (tracked) en el mismo DbContext: inscripción + persona son atómicas.
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsIndexViolation(ex, "UX_PersonaInscrita_Identificacion"))
        {
            // Dos inscripciones simultáneas de una cédula nueva a capacitaciones distintas.
            throw new InscripcionConcurrenteException();
        }
        catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
        {
            // Carrera contra UX_Asistente_Capacitacion_Identificacion: dos requests concurrentes
            // que pasaron la pre-check del caso de uso. Re-lanzamos como excepción de dominio
            // para que el controller traduzca a 409 Conflict de forma consistente.
            throw new InscripcionDuplicadaException();
        }
    }

    private static bool IsIndexViolation(DbUpdateException ex, string indexName)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains(indexName, StringComparison.OrdinalIgnoreCase);
    }
```

(`IsUniqueIndexViolation` existente se deja igual.)

- [ ] **Step 5: Registrar en DI**

En `Program.cs`, debajo de `builder.Services.AddScoped<IAsistenteRepository, AsistenteRepository>();`:

```csharp
builder.Services.AddScoped<IPersonaInscritaRepository, PersonaInscritaRepository>();
```

- [ ] **Step 6: Generar la migración**

Run (desde `backend`):

```bash
dotnet tool restore
dotnet ef migrations add AddPersonaInscrita -p src/Capacitaciones.Infrastructure -s src/Capacitaciones.Infrastructure -o Persistence/Migrations
```

Expected: `Done.` y tres archivos cambiados en `Persistence/Migrations` (`*_AddPersonaInscrita.cs`, `*_AddPersonaInscrita.Designer.cs`, `AppDbContextModelSnapshot.cs`). Si el comando falla por el startup project, reintentar con `-s src/Capacitaciones.Api`.

Revisar el `Up` generado: debe crear `dbo.PersonaInscrita` con FK a `dbo.Area` y el índice único `UX_PersonaInscrita_Identificacion`, **sin** tocar otras tablas. Si aparecen cambios ajenos, detenerse y reportar (el snapshot estaría desfasado).

- [ ] **Step 7: Agregar el backfill al `Up`**

Al **final** del método `Up` (después del `CreateIndex`):

```csharp
            // Backfill: una persona por identificación, tomando su inscripción más reciente.
            migrationBuilder.Sql(@"
INSERT INTO dbo.PersonaInscrita (Id, Identificacion, Nombres, Apellidos, AreaId, EmailUsuario, Firma, FechaCreacion, FechaActualizacion)
SELECT NEWID(), a.Identificacion, a.Nombres, a.Apellidos, a.AreaId, a.EmailUsuario, NULLIF(a.Firma, ''), a.FechaInscripcion, a.FechaInscripcion
FROM (
    SELECT Identificacion, Nombres, Apellidos, AreaId, EmailUsuario, Firma, FechaInscripcion,
           ROW_NUMBER() OVER (PARTITION BY Identificacion ORDER BY FechaInscripcion DESC) AS rn
    FROM dbo.Asistente
) a
WHERE a.rn = 1;");
```

El `Down` generado (`DropTable`) queda igual.

- [ ] **Step 8: Compilar y validar el script SQL**

Run:

```bash
dotnet build --nologo src/Capacitaciones.Api
dotnet ef migrations script AdminUserUsuarioRed AddPersonaInscrita -p src/Capacitaciones.Infrastructure -s src/Capacitaciones.Infrastructure
```

Expected: build sin errores; el script muestra `CREATE TABLE [dbo].[PersonaInscrita]`, `CREATE UNIQUE INDEX [UX_PersonaInscrita_Identificacion]` y el `INSERT INTO dbo.PersonaInscrita ... ROW_NUMBER()`.

- [ ] **Step 9: Suites existentes de inscripción siguen verdes**

Run: `dotnet test --nologo --filter "FullyQualifiedName~Inscrip"`
Expected: PASS (mismas pruebas que antes; aún no hay tests nuevos).

- [ ] **Step 10: Commit**

```bash
git add backend/src/Capacitaciones.Infrastructure backend/src/Capacitaciones.Api/Program.cs
git commit -m "feat(inscripcion): tabla PersonaInscrita con backfill desde Asistente

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: `InscribirAsistenteUseCase` — firma registrada + upsert de persona

**Files:**
- Modify: `backend/src/Capacitaciones.Application/UseCases/Inscripcion/InscribirAsistenteUseCase.cs`
- Test: `backend/tests/Capacitaciones.Tests/InscribirAsistenteUseCaseTests.cs`

**Interfaces:**
- Consumes: `IPersonaInscritaRepository`, `PersonaInscrita`, `FirmaRegistradaNoDisponibleException`, `CreateInscripcionDto.UsarFirmaRegistrada` (Task 1).
- Produces: constructor `InscribirAsistenteUseCase(ICapacitacionRepository, IAreaRepository, IAsistenteRepository, IPersonaInscritaRepository)` — el orden importa para Task 5 (DI lo resuelve solo).

- [ ] **Step 1: Agregar el fake y el nuevo argumento a los tests existentes**

En `InscribirAsistenteUseCaseTests.cs`, en la sección `// ----- Fakes -----` agregar:

```csharp
    private sealed class FakePersonaInscritaRepo : IPersonaInscritaRepository
    {
        public Dictionary<string, PersonaInscrita> Store { get; } = new();
        public List<PersonaInscrita> Agregadas { get; } = new();

        public FakePersonaInscritaRepo(params PersonaInscrita[] existentes)
        {
            foreach (var p in existentes) Store[p.Identificacion] = p;
        }

        public Task<PersonaInscrita?> GetByIdentificacionAsync(string identificacion, CancellationToken ct = default)
            => Task.FromResult(Store.TryGetValue(identificacion.Trim(), out var p) ? p : null);

        public void Agregar(PersonaInscrita entity)
        {
            Agregadas.Add(entity);
            Store[entity.Identificacion] = entity;
        }
    }

    private static PersonaInscrita BuildPersona(string identificacion = "1712345678", string? firma = "data:image/png;base64,GUARDADA==") => new()
    {
        Id = Guid.NewGuid(),
        Identificacion = identificacion,
        Nombres = "Nombre Viejo",
        Apellidos = "Apellido Viejo",
        AreaId = null,
        EmailUsuario = "viejo@dos.com.ec",
        Firma = firma,
        FechaCreacion = DateTime.UtcNow.AddDays(-30),
        FechaActualizacion = DateTime.UtcNow.AddDays(-30)
    };
```

Luego, en **cada** `new InscribirAsistenteUseCase(...)` existente (7 ocurrencias) agregar `new FakePersonaInscritaRepo()` como cuarto argumento:
- `new InscribirAsistenteUseCase(capRepo, areaRepo, asisRepo);` → `new InscribirAsistenteUseCase(capRepo, areaRepo, asisRepo, new FakePersonaInscritaRepo());`
- líneas que terminan en `            asisRepo);` dentro del constructor → `            asisRepo,\n            new FakePersonaInscritaRepo());`
- `new FakeAsistenteRepo(out var asisRepo));` → `new FakeAsistenteRepo(out var asisRepo),\n            new FakePersonaInscritaRepo());`

Verificar: `grep -c "new FakePersonaInscritaRepo" backend/tests/Capacitaciones.Tests/InscribirAsistenteUseCaseTests.cs` → `7`.

- [ ] **Step 2: Escribir los tests nuevos**

Agregar antes de `// ----- Fakes -----`:

```csharp
    [Fact]
    public async Task ExecuteAsync_PrimeraInscripcion_CreaPersonaConFirma()
    {
        var capacitacion = BuildCapacitacion();
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var personas = new FakePersonaInscritaRepo();
        var useCase = new InscribirAsistenteUseCase(
            new FakeCapacitacionRepo(capacitacion), new FakeAreaRepo(area), new FakeAsistenteRepo(), personas);

        await useCase.ExecuteAsync(capacitacion.Id, BuildInput(area.Id));

        var p = Assert.Single(personas.Agregadas);
        Assert.Equal("1712345678", p.Identificacion);
        Assert.Equal("Juan", p.Nombres);
        Assert.Equal("Perez", p.Apellidos);
        Assert.Equal(area.Id, p.AreaId);
        Assert.Equal("juan.perez@dos.com.ec", p.EmailUsuario);
        Assert.Equal("data:image/png;base64,AAA==", p.Firma);
        Assert.Equal(p.FechaCreacion, p.FechaActualizacion);
    }

    [Fact]
    public async Task ExecuteAsync_UsarFirmaRegistrada_CopiaFirmaYActualizaDatosSinReemplazarla()
    {
        var capacitacion = BuildCapacitacion();
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var persona = BuildPersona();
        var personas = new FakePersonaInscritaRepo(persona);
        var asisRepo = new FakeAsistenteRepo();
        var useCase = new InscribirAsistenteUseCase(
            new FakeCapacitacionRepo(capacitacion), new FakeAreaRepo(area), asisRepo, personas);

        var input = BuildInput(area.Id);
        input.UsarFirmaRegistrada = true;
        input.Firma = "data:image/png;base64,IGNORADA==";

        await useCase.ExecuteAsync(capacitacion.Id, input);

        Assert.Equal("data:image/png;base64,GUARDADA==", asisRepo.Added.Single().Firma);
        Assert.Empty(personas.Agregadas);
        Assert.Equal("data:image/png;base64,GUARDADA==", persona.Firma);
        Assert.Equal("Juan", persona.Nombres);
        Assert.Equal("Perez", persona.Apellidos);
        Assert.Equal(area.Id, persona.AreaId);
        Assert.Equal("juan.perez@dos.com.ec", persona.EmailUsuario);
        Assert.True(persona.FechaActualizacion > persona.FechaCreacion);
    }

    [Fact]
    public async Task ExecuteAsync_FirmaNueva_ReemplazaLaGuardada()
    {
        var capacitacion = BuildCapacitacion();
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var persona = BuildPersona();
        var useCase = new InscribirAsistenteUseCase(
            new FakeCapacitacionRepo(capacitacion), new FakeAreaRepo(area), new FakeAsistenteRepo(), new FakePersonaInscritaRepo(persona));

        await useCase.ExecuteAsync(capacitacion.Id, BuildInput(area.Id)); // Firma = AAA==, UsarFirmaRegistrada = false

        Assert.Equal("data:image/png;base64,AAA==", persona.Firma);
    }

    [Fact]
    public async Task ExecuteAsync_UsarFirmaRegistrada_SinPersona_LanzaFirmaNoDisponible()
    {
        var capacitacion = BuildCapacitacion();
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var asisRepo = new FakeAsistenteRepo();
        var useCase = new InscribirAsistenteUseCase(
            new FakeCapacitacionRepo(capacitacion), new FakeAreaRepo(area), asisRepo, new FakePersonaInscritaRepo());

        var input = BuildInput(area.Id);
        input.UsarFirmaRegistrada = true;
        input.Firma = null;

        var ex = await Assert.ThrowsAsync<FirmaRegistradaNoDisponibleException>(() => useCase.ExecuteAsync(capacitacion.Id, input));
        Assert.Equal("FIRMA_REGISTRADA_NO_DISPONIBLE", ex.Codigo);
        Assert.Empty(asisRepo.Added);
    }

    [Fact]
    public async Task ExecuteAsync_UsarFirmaRegistrada_PersonaSinFirma_LanzaFirmaNoDisponible()
    {
        var capacitacion = BuildCapacitacion();
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var asisRepo = new FakeAsistenteRepo();
        var useCase = new InscribirAsistenteUseCase(
            new FakeCapacitacionRepo(capacitacion), new FakeAreaRepo(area), asisRepo, new FakePersonaInscritaRepo(BuildPersona(firma: null)));

        var input = BuildInput(area.Id);
        input.UsarFirmaRegistrada = true;

        await Assert.ThrowsAsync<FirmaRegistradaNoDisponibleException>(() => useCase.ExecuteAsync(capacitacion.Id, input));
        Assert.Empty(asisRepo.Added);
    }

    [Fact]
    public async Task ExecuteAsync_SinFirmaYSinUsarRegistrada_LanzaCampoRequerido()
    {
        var capacitacion = BuildCapacitacion();
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var useCase = new InscribirAsistenteUseCase(
            new FakeCapacitacionRepo(capacitacion), new FakeAreaRepo(area), new FakeAsistenteRepo(), new FakePersonaInscritaRepo(BuildPersona()));

        var input = BuildInput(area.Id);
        input.Firma = "   ";

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() => useCase.ExecuteAsync(capacitacion.Id, input));
        Assert.Equal("CAMPO_REQUERIDO", ex.Codigo);
    }

    [Fact]
    public async Task ExecuteAsync_Duplicado_NoModificaPersona()
    {
        var capacitacion = BuildCapacitacion();
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var persona = BuildPersona();
        var asisRepo = new FakeAsistenteRepo();
        asisRepo.PreExistingDupes.Add((capacitacion.Id, "1712345678"));
        var useCase = new InscribirAsistenteUseCase(
            new FakeCapacitacionRepo(capacitacion), new FakeAreaRepo(area), asisRepo, new FakePersonaInscritaRepo(persona));

        await Assert.ThrowsAsync<InscripcionDuplicadaException>(() => useCase.ExecuteAsync(capacitacion.Id, BuildInput(area.Id)));

        Assert.Equal("Nombre Viejo", persona.Nombres);
        Assert.Equal("data:image/png;base64,GUARDADA==", persona.Firma);
    }
```

- [ ] **Step 3: Correr los tests — deben fallar**

Run: `dotnet test --nologo --filter "FullyQualifiedName~InscribirAsistenteUseCaseTests"`
Expected: FAIL de compilación (`InscribirAsistenteUseCase` no tiene un constructor de 4 argumentos).

- [ ] **Step 4: Implementar en el caso de uso**

En `InscribirAsistenteUseCase.cs`:

1. Campo y constructor:

```csharp
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAreaRepository _areas;
    private readonly IAsistenteRepository _asistentes;
    private readonly IPersonaInscritaRepository _personas;

    public InscribirAsistenteUseCase(
        ICapacitacionRepository capacitaciones,
        IAreaRepository areas,
        IAsistenteRepository asistentes,
        IPersonaInscritaRepository personas)
    {
        _capacitaciones = capacitaciones;
        _areas = areas;
        _asistentes = asistentes;
        _personas = personas;
    }
```

2. Borrar la línea `var firma = RequireTrimmed(input.Firma, "firma");`.

3. Reemplazar desde `var entity = new Asistente` hasta `await _asistentes.AddAsync(entity, ct);` (inclusive) por:

```csharp
        // Firma: la registrada (si se pidió) o la nueva. Se resuelve después del chequeo de
        // duplicado para no tocar la persona en una inscripción que igual se va a rechazar.
        var persona = await _personas.GetByIdentificacionAsync(identificacion, ct);
        string firma;
        if (input.UsarFirmaRegistrada)
        {
            if (persona is null || string.IsNullOrWhiteSpace(persona.Firma))
            {
                throw new FirmaRegistradaNoDisponibleException();
            }
            firma = persona.Firma;
        }
        else
        {
            firma = RequireTrimmed(input.Firma, "firma");
        }

        var ahora = DateTime.UtcNow;
        var entity = new Asistente
        {
            Id = Guid.NewGuid(),
            CapacitacionId = capacitacionId,
            Nombres = nombres,
            Apellidos = apellidos,
            Identificacion = identificacion,
            AreaId = area.Id,
            EmailUsuario = emailUsuario + EmailDomain,
            Firma = firma,
            FechaInscripcion = ahora
        };

        // Alta/actualización de la persona. Queda pendiente (tracked) y se persiste en el mismo
        // SaveChanges de AddAsync: inscripción + persona son atómicas.
        if (persona is null)
        {
            _personas.Agregar(new PersonaInscrita
            {
                Id = Guid.NewGuid(),
                Identificacion = identificacion,
                Nombres = nombres,
                Apellidos = apellidos,
                AreaId = area.Id,
                EmailUsuario = entity.EmailUsuario,
                Firma = firma,
                FechaCreacion = ahora,
                FechaActualizacion = ahora
            });
        }
        else
        {
            persona.Nombres = nombres;
            persona.Apellidos = apellidos;
            persona.AreaId = area.Id;
            persona.EmailUsuario = entity.EmailUsuario;
            persona.FechaActualizacion = ahora;
            if (!input.UsarFirmaRegistrada)
            {
                persona.Firma = firma;
            }
        }

        // El repositorio traduce la violación del UNIQUE INDEX (carrera contra pre-check) a
        // InscripcionDuplicadaException (o InscripcionConcurrenteException si el choque es en
        // PersonaInscrita) para mantener Application desacoplado de EF.
        await _asistentes.AddAsync(entity, ct);
```

4. En el `<summary>` de la clase, cambiar la línea de validaciones de `Firma` a: `<c>Nombres</c>, <c>Apellidos</c>, <c>Identificacion</c>, <c>EmailUsuario</c> se trimean y no pueden quedar vacíos; <c>Firma</c> también, salvo que <c>UsarFirmaRegistrada</c> sea true (se copia de <c>PersonaInscrita</c>).` y agregar al final: `Crea o actualiza la <c>PersonaInscrita</c> de la identificación en la misma operación.`

- [ ] **Step 5: Correr los tests — deben pasar**

Run: `dotnet test --nologo --filter "FullyQualifiedName~InscribirAsistenteUseCaseTests"`
Expected: PASS, todos (los 7 existentes + 7 nuevos).

- [ ] **Step 6: Commit**

```bash
git add backend/src/Capacitaciones.Application/UseCases/Inscripcion/InscribirAsistenteUseCase.cs backend/tests/Capacitaciones.Tests/InscribirAsistenteUseCaseTests.cs
git commit -m "feat(inscripcion): guarda la persona inscrita y permite reusar su firma

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: `BuscarPersonaInscritaUseCase`

**Files:**
- Create: `backend/src/Capacitaciones.Application/UseCases/Inscripcion/BuscarPersonaInscritaUseCase.cs`
- Test: `backend/tests/Capacitaciones.Tests/BuscarPersonaInscritaUseCaseTests.cs`

**Interfaces:**
- Consumes: `IPersonaInscritaRepository`, `IAreaRepository` (`Task<Area?> GetByIdAsync(Guid id, CancellationToken ct = default)`), `PersonaInscritaLookupDto`.
- Produces: `BuscarPersonaInscritaUseCase(IPersonaInscritaRepository personas, IAreaRepository areas)` con `Task<PersonaInscritaLookupDto?> ExecuteAsync(string identificacion, CancellationToken ct = default)` — `null` si no existe o la identificación viene vacía.

- [ ] **Step 1: Escribir los tests**

`backend/tests/Capacitaciones.Tests/BuscarPersonaInscritaUseCaseTests.cs`:

```csharp
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Inscripcion;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>Tests unitarios de <see cref="BuscarPersonaInscritaUseCase"/> (autocompletado por cédula).</summary>
public class BuscarPersonaInscritaUseCaseTests
{
    private static PersonaInscrita BuildPersona(Guid? areaId, string? firma = "data:image/png;base64,AAA==") => new()
    {
        Id = Guid.NewGuid(),
        Identificacion = "1712345678",
        Nombres = "María Fernanda",
        Apellidos = "Pérez Torres",
        AreaId = areaId,
        EmailUsuario = "maria.perez@dos.com.ec",
        Firma = firma,
        FechaCreacion = DateTime.UtcNow,
        FechaActualizacion = DateTime.UtcNow
    };

    [Fact]
    public async Task Encontrada_DevuelveDatosSinSufijoYTieneFirma()
    {
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var useCase = new BuscarPersonaInscritaUseCase(new FakePersonas(BuildPersona(area.Id)), new FakeAreas(area));

        var dto = await useCase.ExecuteAsync(" 1712345678 ");

        Assert.NotNull(dto);
        Assert.Equal("María Fernanda", dto!.Nombres);
        Assert.Equal("Pérez Torres", dto.Apellidos);
        Assert.Equal(area.Id, dto.AreaId);
        Assert.Equal("maria.perez", dto.EmailUsuario);
        Assert.True(dto.TieneFirma);
    }

    [Fact]
    public async Task SinFirma_TieneFirmaFalse()
    {
        var useCase = new BuscarPersonaInscritaUseCase(new FakePersonas(BuildPersona(null, firma: null)), new FakeAreas(null));

        var dto = await useCase.ExecuteAsync("1712345678");

        Assert.False(dto!.TieneFirma);
        Assert.Null(dto.AreaId);
    }

    [Fact]
    public async Task AreaInactiva_DevuelveAreaIdNull()
    {
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = false, FechaCreacion = DateTime.UtcNow };
        var useCase = new BuscarPersonaInscritaUseCase(new FakePersonas(BuildPersona(area.Id)), new FakeAreas(area));

        var dto = await useCase.ExecuteAsync("1712345678");

        Assert.Null(dto!.AreaId);
    }

    [Fact]
    public async Task NoEncontrada_DevuelveNull()
    {
        var useCase = new BuscarPersonaInscritaUseCase(new FakePersonas(), new FakeAreas(null));

        Assert.Null(await useCase.ExecuteAsync("0999999999"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IdentificacionVacia_DevuelveNull(string identificacion)
    {
        var useCase = new BuscarPersonaInscritaUseCase(new FakePersonas(BuildPersona(null)), new FakeAreas(null));

        Assert.Null(await useCase.ExecuteAsync(identificacion));
    }

    private sealed class FakePersonas : IPersonaInscritaRepository
    {
        private readonly Dictionary<string, PersonaInscrita> _store = new();
        public FakePersonas(params PersonaInscrita[] existentes)
        {
            foreach (var p in existentes) _store[p.Identificacion] = p;
        }
        public Task<PersonaInscrita?> GetByIdentificacionAsync(string identificacion, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(identificacion, out var p) ? p : null);
        public void Agregar(PersonaInscrita entity) => throw new NotImplementedException();
    }

    private sealed class FakeAreas : IAreaRepository
    {
        private readonly Area? _area;
        public FakeAreas(Area? area) { _area = area; }
        public Task<Area?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_area is not null && _area.Id == id ? _area : null);
        public Task<IEnumerable<Area>> ListAsync(bool includeInactive = false, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Area?> GetByNombreAsync(string nombre, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(Area entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Area> entities, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(Area entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
```

Nota: el fake de personas busca por la clave exacta; el test `Encontrada_...` pasa `" 1712345678 "`, así que el caso de uso **debe** hacer Trim antes de llamar al repositorio.

- [ ] **Step 2: Correr — debe fallar**

Run: `dotnet test --nologo --filter "FullyQualifiedName~BuscarPersonaInscritaUseCaseTests"`
Expected: FAIL de compilación (`BuscarPersonaInscritaUseCase` no existe).

- [ ] **Step 3: Implementar**

`backend/src/Capacitaciones.Application/UseCases/Inscripcion/BuscarPersonaInscritaUseCase.cs`:

```csharp
using Capacitaciones.Application.Dtos.Inscripcion;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Inscripcion;

/// <summary>
/// Autocompletado por cédula de la página pública de inscripción. Busca solo en el registro
/// propio (<c>PersonaInscrita</c>) — no consulta DOS/ControlTareas ni Externos.
///
/// Nunca devuelve la firma: solo <see cref="PersonaInscritaLookupDto.TieneFirma"/>. El correo va
/// sin el sufijo <c>@dos.com.ec</c> (el formulario captura solo la parte local) y el área solo si
/// sigue activa.
/// </summary>
public class BuscarPersonaInscritaUseCase
{
    private const string EmailDomain = "@dos.com.ec";

    private readonly IPersonaInscritaRepository _personas;
    private readonly IAreaRepository _areas;

    public BuscarPersonaInscritaUseCase(IPersonaInscritaRepository personas, IAreaRepository areas)
    {
        _personas = personas;
        _areas = areas;
    }

    public async Task<PersonaInscritaLookupDto?> ExecuteAsync(string identificacion, CancellationToken ct = default)
    {
        var id = (identificacion ?? string.Empty).Trim();
        if (id.Length == 0) return null;

        var persona = await _personas.GetByIdentificacionAsync(id, ct);
        if (persona is null) return null;

        Guid? areaId = null;
        if (persona.AreaId is Guid guardada)
        {
            var area = await _areas.GetByIdAsync(guardada, ct);
            if (area is not null && area.Activo) areaId = area.Id;
        }

        var email = persona.EmailUsuario ?? string.Empty;
        if (email.EndsWith(EmailDomain, StringComparison.OrdinalIgnoreCase))
        {
            email = email[..^EmailDomain.Length];
        }

        return new PersonaInscritaLookupDto
        {
            Nombres = persona.Nombres,
            Apellidos = persona.Apellidos,
            AreaId = areaId,
            EmailUsuario = email,
            TieneFirma = !string.IsNullOrWhiteSpace(persona.Firma)
        };
    }
}
```

- [ ] **Step 4: Correr — debe pasar**

Run: `dotnet test --nologo --filter "FullyQualifiedName~BuscarPersonaInscritaUseCaseTests"`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add backend/src/Capacitaciones.Application/UseCases/Inscripcion/BuscarPersonaInscritaUseCase.cs backend/tests/Capacitaciones.Tests/BuscarPersonaInscritaUseCaseTests.cs
git commit -m "feat(inscripcion): caso de uso de búsqueda de persona por cédula

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: Endpoint público + DI + tests de integración

**Files:**
- Modify: `backend/src/Capacitaciones.Api/Controllers/InscripcionController.cs`
- Modify: `backend/src/Capacitaciones.Api/Program.cs:285` (registro del caso de uso)
- Test: `backend/tests/Capacitaciones.Tests/InscripcionPersonaEndpointTests.cs`

**Interfaces:**
- Consumes: `BuscarPersonaInscritaUseCase.ExecuteAsync(string, CancellationToken)` (Task 4); `IJwtTokenGenerator.GenerateInscripcionToken(Guid capacitacionId)` → `JwtTokenResult.Token`; `AppDbContext.PersonasInscritas` (Task 2).
- Produces: `GET /api/inscripcion/capacitacion/persona/{identificacion}` → 200 `{ nombres, apellidos, areaId, emailUsuario, tieneFirma }` | 404 | 401. `POST /api/inscripcion/capacitacion` acepta `usarFirmaRegistrada`. Códigos `FIRMA_REGISTRADA_NO_DISPONIBLE` → 400, `INSCRIPCION_CONCURRENTE` → 409, cuerpo `{ error, message }`.

- [ ] **Step 1: Escribir los tests de integración**

Estos tests **no** usan el login admin (roto en el baseline): generan el token de inscripción directo con `IJwtTokenGenerator`.

`backend/tests/Capacitaciones.Tests/InscripcionPersonaEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Capacitaciones.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Capacitaciones.Tests;

/// <summary>
/// Integración del autocompletado por cédula y el reuso de firma en la inscripción pública:
///   - GET /api/inscripcion/capacitacion/persona/{id} → 200 sin firma / 404 / 401.
///   - POST /api/inscripcion/capacitacion con usarFirmaRegistrada → 201 y copia la firma guardada.
/// </summary>
public class InscripcionPersonaEndpointTests : IClassFixture<InMemoryWebAppFactory>
{
    private readonly InMemoryWebAppFactory _factory;

    private static readonly Guid SeededModalidadId = new("11111111-1111-1111-1111-111111110020");
    private static readonly Guid SeededTipoActividadId = new("22222222-2222-2222-2222-222222220020");
    private static readonly Guid SeededAreaId = new("44444444-4444-4444-4444-444444440020");
    private const string FirmaGuardada = "data:image/png;base64,GUARDADA==";

    public InscripcionPersonaEndpointTests(InMemoryWebAppFactory factory)
    {
        _factory = factory;
        EnsureSeeded();
    }

    private void EnsureSeeded()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        if (!db.Modalidades.Any(m => m.Id == SeededModalidadId))
            db.Modalidades.Add(new Modalidad { Id = SeededModalidadId, Nombre = "Presencial", Activo = true, FechaCreacion = t0 });
        if (!db.TiposActividad.Any(t => t.Id == SeededTipoActividadId))
            db.TiposActividad.Add(new TipoActividad { Id = SeededTipoActividadId, Nombre = "Charla", Activo = true, FechaCreacion = t0 });
        if (!db.Areas.Any(a => a.Id == SeededAreaId))
            db.Areas.Add(new Area { Id = SeededAreaId, Nombre = "Area persona", Activo = true, FechaCreacion = t0 });
        db.SaveChanges();
    }

    private Guid CrearCapacitacion()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cap = new Capacitacion
        {
            Id = Guid.NewGuid(),
            Codigo = $"CAP-PC-REG-{Random.Shared.Next(100, 999):000}",
            Tema = "Persona inscrita test",
            Capacitador = "Ana",
            ModalidadId = SeededModalidadId,
            TipoActividadId = SeededTipoActividadId,
            TipoCertificacion = TipoCertificacion.Participacion,
            FechaHoraInicio = DateTime.UtcNow.AddDays(1),
            DuracionMinutos = 60,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        db.Capacitaciones.Add(cap);
        db.SaveChanges();
        return cap.Id;
    }

    private void CrearPersona(string identificacion)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PersonasInscritas.Add(new PersonaInscrita
        {
            Id = Guid.NewGuid(),
            Identificacion = identificacion,
            Nombres = "María Fernanda",
            Apellidos = "Pérez Torres",
            AreaId = SeededAreaId,
            EmailUsuario = "maria.perez@dos.com.ec",
            Firma = FirmaGuardada,
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private HttpClient ClienteConToken(Guid capacitacionId)
    {
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt.GenerateInscripcionToken(capacitacionId).Token);
        return client;
    }

    [Fact]
    public async Task BuscarPersona_Existente_Devuelve200SinFirma()
    {
        var capId = CrearCapacitacion();
        CrearPersona("1700000001");
        var client = ClienteConToken(capId);

        var resp = await client.GetAsync("/api/inscripcion/capacitacion/persona/1700000001");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("María Fernanda", root.GetProperty("nombres").GetString());
        Assert.Equal("Pérez Torres", root.GetProperty("apellidos").GetString());
        Assert.Equal("maria.perez", root.GetProperty("emailUsuario").GetString());
        Assert.Equal(SeededAreaId, root.GetProperty("areaId").GetGuid());
        Assert.True(root.GetProperty("tieneFirma").GetBoolean());
        Assert.False(root.TryGetProperty("firma", out _));
        Assert.DoesNotContain("GUARDADA", root.GetRawText());
    }

    [Fact]
    public async Task BuscarPersona_Inexistente_Devuelve404()
    {
        var client = ClienteConToken(CrearCapacitacion());

        var resp = await client.GetAsync("/api/inscripcion/capacitacion/persona/0999999999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task BuscarPersona_SinToken_Devuelve401()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/inscripcion/capacitacion/persona/1700000001");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Inscribir_UsandoFirmaRegistrada_Devuelve201YCopiaLaFirma()
    {
        var capId = CrearCapacitacion();
        CrearPersona("1700000002");
        var client = ClienteConToken(capId);

        var resp = await client.PostAsJsonAsync("/api/inscripcion/capacitacion", new
        {
            nombres = "María Fernanda",
            apellidos = "Pérez Torres",
            identificacion = "1700000002",
            areaId = SeededAreaId,
            emailUsuario = "maria.perez",
            firma = (string?)null,
            usarFirmaRegistrada = true
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asistente = await db.Asistentes.AsNoTracking().SingleAsync(a => a.CapacitacionId == capId && a.Identificacion == "1700000002");
        Assert.Equal(FirmaGuardada, asistente.Firma);
    }

    [Fact]
    public async Task Inscribir_PrimeraVez_CreaPersona()
    {
        var capId = CrearCapacitacion();
        var client = ClienteConToken(capId);

        var resp = await client.PostAsJsonAsync("/api/inscripcion/capacitacion", new
        {
            nombres = "Juan",
            apellidos = "Perez",
            identificacion = "1700000003",
            areaId = SeededAreaId,
            emailUsuario = "juan.perez",
            firma = "data:image/png;base64,NUEVA==",
            usarFirmaRegistrada = false
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persona = await db.PersonasInscritas.AsNoTracking().SingleAsync(p => p.Identificacion == "1700000003");
        Assert.Equal("data:image/png;base64,NUEVA==", persona.Firma);
        Assert.Equal("juan.perez@dos.com.ec", persona.EmailUsuario);
    }

    [Fact]
    public async Task Inscribir_UsarFirmaRegistradaSinPersona_Devuelve400ConCodigo()
    {
        var client = ClienteConToken(CrearCapacitacion());

        var resp = await client.PostAsJsonAsync("/api/inscripcion/capacitacion", new
        {
            nombres = "Ana",
            apellidos = "Lopez",
            identificacion = "1700000004",
            areaId = SeededAreaId,
            emailUsuario = "ana.lopez",
            usarFirmaRegistrada = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("FIRMA_REGISTRADA_NO_DISPONIBLE", doc.RootElement.GetProperty("error").GetString());
    }
}
```

- [ ] **Step 2: Correr — debe fallar**

Run: `dotnet test --nologo --filter "FullyQualifiedName~InscripcionPersonaEndpointTests"`
Expected: FAIL — los GET de persona devuelven 404/405 en vez de 200 (el endpoint no existe). Los POST pueden pasar ya (Task 3).

Si `BuscarPersona_SinToken_Devuelve401` o los POST fallan por 401 con token, detenerse: el entorno `Testing` no está validando los tokens emitidos por `IJwtTokenGenerator`; reportar antes de seguir.

- [ ] **Step 3: Implementar el endpoint**

En `InscripcionController.cs`:

1. Campo + constructor (agregar `BuscarPersonaInscritaUseCase buscarPersona`):

```csharp
    private readonly ObtenerInscripcionPublicaUseCase _obtener;
    private readonly InscribirAsistenteUseCase _inscribir;
    private readonly BuscarPersonaInscritaUseCase _buscarPersona;

    public InscripcionController(
        ObtenerInscripcionPublicaUseCase obtener,
        InscribirAsistenteUseCase inscribir,
        BuscarPersonaInscritaUseCase buscarPersona)
    {
        _obtener = obtener;
        _inscribir = inscribir;
        _buscarPersona = buscarPersona;
    }
```

2. Nueva acción, después de `Get`:

```csharp
    /// <summary>
    /// Autocompletado por cédula: datos de la persona si ya se inscribió alguna vez.
    /// Nunca incluye la firma (solo <c>tieneFirma</c>).
    /// </summary>
    [HttpGet("persona/{identificacion}")]
    public async Task<IActionResult> BuscarPersona(string identificacion, CancellationToken ct)
    {
        if (!TryGetCapacitacionId(out _))
        {
            return Unauthorized();
        }

        var dto = await _buscarPersona.ExecuteAsync(identificacion, ct);
        return dto is null ? NotFound() : Ok(dto);
    }
```

3. En `ToProblem`, agregar al `switch`:

```csharp
            "INSCRIPCION_CONCURRENTE" => StatusCodes.Status409Conflict,
            "FIRMA_REGISTRADA_NO_DISPONIBLE" => StatusCodes.Status400BadRequest,
```

- [ ] **Step 4: Registrar el caso de uso**

En `Program.cs`, debajo de `builder.Services.AddScoped<InscribirAsistenteUseCase>();`:

```csharp
builder.Services.AddScoped<BuscarPersonaInscritaUseCase>();
```

- [ ] **Step 5: Correr — debe pasar**

Run: `dotnet test --nologo --filter "FullyQualifiedName~InscripcionPersonaEndpointTests|FullyQualifiedName~Inscrip"`
Expected: PASS, todos.

- [ ] **Step 6: Commit**

```bash
git add backend/src/Capacitaciones.Api/Controllers/InscripcionController.cs backend/src/Capacitaciones.Api/Program.cs backend/tests/Capacitaciones.Tests/InscripcionPersonaEndpointTests.cs
git commit -m "feat(inscripcion): endpoint público de búsqueda de persona por cédula

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: Frontend — autocompletado y "Usar mi firma registrada"

No hay suite de tests de frontend en el repo; se verifica con `npm run lint`, `npm run build` y prueba manual.

**Files:**
- Modify: `frontend/src/services/inscripcion.js`
- Modify: `frontend/src/pages/inscripcion/InscripcionPage.jsx`

**Interfaces:**
- Consumes: endpoints de Task 5.
- Produces: `buscarPersona(token: string, identificacion: string): Promise<{nombres, apellidos, areaId: string|null, emailUsuario, tieneFirma: boolean} | null>`; `inscribir(token, { ..., firma: string|null, usarFirmaRegistrada: boolean })`.

- [ ] **Step 1: Servicio**

En `frontend/src/services/inscripcion.js`:

1. En el comentario `Contrato:` agregar:

```
 *   GET  /api/inscripcion/capacitacion/persona/{identificacion}
 *       -> 200 { nombres, apellidos, areaId, emailUsuario, tieneFirma }  (nunca la firma)
 *       - 404 no registrada.
```

y cambiar la línea del body del POST a:

```
 *       body: { nombres, apellidos, identificacion, areaId, emailUsuario, firma, usarFirmaRegistrada }
 *       - 400 FIRMA_REGISTRADA_NO_DISPONIBLE si se pidió la firma registrada y no existe.
 *       - 409 INSCRIPCION_CONCURRENTE (reintentar).
```

2. Después de `getCapacitacion`, agregar:

```js
/**
 * GET /inscripcion/capacitacion/persona/{identificacion} — autocompletado por cédula.
 * @param {string} token
 * @param {string} identificacion
 * @returns {Promise<{ nombres: string, apellidos: string, areaId: string|null, emailUsuario: string, tieneFirma: boolean } | null>}
 *   null si la persona no está registrada (404).
 */
export async function buscarPersona(token, identificacion) {
  try {
    return await requestWithToken(
      `/inscripcion/capacitacion/persona/${encodeURIComponent(identificacion)}`,
      token,
      { method: 'GET' },
    );
  } catch (err) {
    if (err instanceof HttpError && err.status === 404) return null;
    throw err;
  }
}
```

3. En el JSDoc de `inscribir`, reemplazar `firma: string, // dataURL PNG` por:

```
 *   firma: string|null,          // dataURL PNG; null si usarFirmaRegistrada
 *   usarFirmaRegistrada: boolean,
```

4. Agregar `buscarPersona` al `export default { ... }`.

- [ ] **Step 2: Página — imports, estado y helpers**

En `InscripcionPage.jsx`:

1. Imports:

```js
import { useCallback, useEffect, useRef, useState } from 'react';
```

```js
import { buscarPersona, getCapacitacion, inscribir } from '../../services/inscripcion.js';
```

2. Después de `const [success, setSuccess] = useState(null); // { nombres, apellidos }`:

```js
  // Autocompletado por cédula (registro propio de personas inscritas).
  const [personaEncontrada, setPersonaEncontrada] = useState(false);
  const [tieneFirmaRegistrada, setTieneFirmaRegistrada] = useState(false);
  const [usarFirmaRegistrada, setUsarFirmaRegistrada] = useState(false);
  // Última identificación consultada: evita repetir la búsqueda y descarta respuestas viejas.
  const ultimaIdentificacionRef = useRef('');

  const limpiarAutocompletado = () => {
    setPersonaEncontrada(false);
    setTieneFirmaRegistrada(false);
    setUsarFirmaRegistrada(false);
  };
```

3. En `mapSubmitError`, reemplazar el bloque `if (err.status === 409) { ... }` por:

```js
      const codigo = err.body && typeof err.body === 'object' ? err.body.error : null;
      if (codigo === 'FIRMA_REGISTRADA_NO_DISPONIBLE') {
        return 'No encontramos una firma registrada para esta identificación. Dibuja o sube tu firma.';
      }
      if (codigo === 'INSCRIPCION_CONCURRENTE') {
        return err.body.message || 'Vuelve a intentarlo.';
      }
      if (err.status === 409) {
        return 'Ya existe una inscripción con esa identificación.';
      }
```

4. Reemplazar `resetForm` por:

```js
  const resetForm = () => {
    setForm(INITIAL_FORM);
    setFormError('');
    setSuccess(null);
    limpiarAutocompletado();
    ultimaIdentificacionRef.current = '';
  };
```

5. Agregar después de `resetForm`:

```js
  const handleIdentificacionChange = (value) => {
    setForm((prev) => ({ ...prev, identificacion: value }));
    // Si cambia la cédula, lo autocompletado ya no aplica: nunca usar la firma de otra persona.
    if (value.trim() !== ultimaIdentificacionRef.current) {
      ultimaIdentificacionRef.current = '';
      limpiarAutocompletado();
    }
  };

  const handleIdentificacionBlur = async () => {
    const identificacion = form.identificacion.trim();
    if (!identificacion || identificacion === ultimaIdentificacionRef.current) return;
    ultimaIdentificacionRef.current = identificacion;
    try {
      const persona = await buscarPersona(token, identificacion);
      // Descarta la respuesta si el usuario cambió la cédula mientras se consultaba.
      if (ultimaIdentificacionRef.current !== identificacion || !persona) return;
      const areaValida = Boolean(persona.areaId) && areas.some((a) => a.id === persona.areaId);
      setForm((prev) => ({
        ...prev,
        nombres: persona.nombres || prev.nombres,
        apellidos: persona.apellidos || prev.apellidos,
        emailUsuario: persona.emailUsuario || prev.emailUsuario,
        areaId: areaValida ? persona.areaId : prev.areaId,
      }));
      setPersonaEncontrada(true);
      setTieneFirmaRegistrada(Boolean(persona.tieneFirma));
      setUsarFirmaRegistrada(Boolean(persona.tieneFirma));
    } catch {
      // Silencioso: si la búsqueda falla, el formulario funciona como siempre.
    }
  };
```

6. En `validate()`, reemplazar `if (!firma) return 'La firma es obligatoria.';` por:

```js
    if (!firma && !usarFirmaRegistrada) return 'La firma es obligatoria.';
```

7. En `handleSubmit`, reemplazar `firma: form.firma,` del payload por:

```js
        firma: usarFirmaRegistrada ? null : form.firma,
        usarFirmaRegistrada,
```

y en el `catch`, antes de `const message = mapSubmitError(error);`:

```js
      if (error instanceof HttpError && error.body?.error === 'FIRMA_REGISTRADA_NO_DISPONIBLE') {
        setTieneFirmaRegistrada(false);
        setUsarFirmaRegistrada(false);
      }
```

- [ ] **Step 3: Página — render del formulario**

1. Intercambiar los dos primeros bloques `<div className={styles.twoCols}>` del formulario para que **Identificación + Área** quede primero y **Nombres + Apellidos** segundo. En el `<input id="identificacion">` reemplazar el `onChange` por:

```jsx
                  onChange={(e) => handleIdentificacionChange(e.target.value)}
                  onBlur={handleIdentificacionBlur}
```

2. Inmediatamente después del bloque Identificación + Área:

```jsx
            {personaEncontrada && (
              <p className="form-helper" role="status">
                Encontramos tus datos. Revísalos antes de inscribirte.
              </p>
            )}
```

3. Reemplazar el bloque de la firma (`<div className={styles.formRow}>` con `<label className={styles.formLabel}>Firma</label>` y el `<SignaturePad ... />`) por:

```jsx
            <div className={styles.formRow}>
              <label className={styles.formLabel}>Firma</label>
              {tieneFirmaRegistrada && (
                <label className="form-checkbox">
                  <input
                    type="checkbox"
                    className="form-checkbox__input"
                    checked={usarFirmaRegistrada}
                    onChange={(e) => setUsarFirmaRegistrada(e.target.checked)}
                    disabled={submitting}
                  />
                  <span className="form-checkbox__label">Usar mi firma registrada</span>
                </label>
              )}
              {!usarFirmaRegistrada && (
                <SignaturePad
                  value={form.firma}
                  onChange={(dataUrl) =>
                    setForm((prev) => ({ ...prev, firma: dataUrl }))
                  }
                  width={400}
                  height={150}
                  disabled={submitting}
                />
              )}
            </div>
```

4. Actualizar el comentario de cabecera del componente, en `Flujo:`, agregando:

```
 *  - Al salir del campo Identificación: GET /inscripcion/capacitacion/persona/{id}
 *    → autocompleta nombres/apellidos/área/correo y ofrece "Usar mi firma registrada"
 *    (la firma nunca viaja al navegador; el servidor la copia).
```

- [ ] **Step 4: Lint y build**

Run (desde `frontend`):

```bash
npm run lint
npm run build
```

Expected: lint sin errores ni warnings; build `✓ built in ...`.

- [ ] **Step 5: Verificación manual**

1. Backend local contra el SQL Server local (`MSSQLSERVER`): en `backend/src/Capacitaciones.Api`, `dotnet run` con `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_URLS=http://localhost:8080` y `ConnectionStrings__Default` apuntando a una BD de pruebas (en Development se aplican migraciones y se siembra el admin).
2. Frontend: `npm run dev` en `frontend` (proxy `/api` → `:8080`).
3. Generar un link de inscripción desde el admin y abrirlo. Comprobar:
   - Cédula nueva → nada se autocompleta; inscribir con firma dibujada → éxito.
   - "Inscribir a otra persona" → formulario y casilla limpios.
   - Otro link (otra capacitación), misma cédula → al salir del campo se completan nombres/apellidos/área/correo, aparece "Encontramos tus datos…" y "Usar mi firma registrada" marcada, sin recuadro de firma. Inscribir → éxito.
   - Desmarcar la casilla → aparece `SignaturePad`; sin firmar, "Inscribirme" → "La firma es obligatoria."
   - Autocompletar y luego **editar la cédula** → desaparecen el aviso y la casilla.
   - DevTools → Network: la respuesta de `/persona/...` no contiene `firma`.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/services/inscripcion.js frontend/src/pages/inscripcion/InscripcionPage.jsx
git commit -m "feat(inscripcion): autocompleta por cédula y permite usar la firma registrada

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: Documentación y verificación final

**Files:**
- Modify: `instrucciones.md` (sección 7, después de la subsección de inscripción pública; si no hay una clara, al final de la sección 7 antes de `## 8. Estado actual`)

- [ ] **Step 1: Documentar**

Agregar:

```markdown
### 7.15 Registro de personas inscritas y reuso de firma

- Tabla `dbo.PersonaInscrita` (única por `Identificacion`): nombres, apellidos, área, correo (`@dos.com.ec`), firma. Migración `AddPersonaInscrita` con backfill desde la inscripción más reciente de cada cédula en `dbo.Asistente`.
- `InscribirAsistenteUseCase` crea/actualiza la persona en el mismo `SaveChanges` que el asistente. `usarFirmaRegistrada: true` copia la firma guardada (400 `FIRMA_REGISTRADA_NO_DISPONIBLE` si no hay); una firma nueva reemplaza la guardada. Choque concurrente del índice → 409 `INSCRIPCION_CONCURRENTE`.
- `GET /api/inscripcion/capacitacion/persona/{identificacion}` (policy `Inscripcion`): `{ nombres, apellidos, areaId (solo si activa), emailUsuario (sin sufijo), tieneFirma }`. **Nunca** devuelve la firma. Busca solo en el registro propio (no DOS/Externos).
- Página pública: Identificación es el primer campo; al salir del campo autocompleta y ofrece "Usar mi firma registrada". Cambiar la cédula limpia el autocompletado.
- Riesgo aceptado: con un link válido y una cédula registrada se ven nombres/área/correo y se puede inscribir con la firma guardada (caso organizador).
```

- [ ] **Step 2: Suite completo**

Run (desde `backend`): `dotnet test --nologo`
Expected: `Con error: 31` (o menos) — **no más** que el baseline — y los nuevos tests (`BuscarPersonaInscritaUseCaseTests`, `InscripcionPersonaEndpointTests`, `InscribirAsistenteUseCaseTests`) todos en verde. Si el número de fallas sube, identificar cuáles son nuevas con `dotnet test --nologo 2>&1 | grep -oE "Con error Capacitaciones\.Tests\.[A-Za-z]+\.[A-Za-z_]+" | sort` y corregir antes de cerrar.

- [ ] **Step 3: Commit**

```bash
git add instrucciones.md
git commit -m "docs(inscripcion): documenta registro de personas y reuso de firma

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

## Despliegue (fuera de este plan)

El despliegue a producción (`iados@10.1.1.174`) queda pendiente del acceso SSH por llave y se hace aparte: backend/rebuild + `npm run build` → `/Docker/web/html/capacitados/`. La migración corre sola al arrancar solo en `Development`; en el servidor verificar cómo se aplican migraciones antes de desplegar. **No** copiar el `.env` local.
