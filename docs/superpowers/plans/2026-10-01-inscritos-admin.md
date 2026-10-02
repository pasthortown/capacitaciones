# Inscritos (admin) — Implementation Plan

> Ejecución inline (el usuario pidió "implementa y despliega"). Spec: `docs/superpowers/specs/2026-10-01-inscritos-admin-design.md`.

**Goal:** pantalla admin para listar/filtrar/editar inscritos y ver su firma.

**Baseline de pruebas:** 31 fallas preexistentes en el suite backend (login AD). Las tareas usan `--filter` por clase; el total no debe subir.

## Task 1: Contratos (Application)
- `Ports/IInscritoRepository.cs`, `Dtos/Inscritos/InscritoDto.cs` (+ `InscritoDetalleDto`, `EditarInscritoDto`), `UseCases/Inscritos/InscritoExceptions.cs` (`InscritoNotFoundException`, código `INSCRITO_NO_ENCONTRADO`).
- Verificación: `dotnet build`.

## Task 2: `EditarInscritoUseCase` (TDD)
- Tests `EditarInscritoUseCaseTests` con fakes: happy path; firma null conserva; firma nueva reemplaza (asistente y persona); duplicado → `InscripcionDuplicadaException` sin cambios; área inválida; email con @; persona inexistente se crea; persona existente sin firma recibe la firma actual; no existe → `InscritoNotFoundException`.
- Implementación + `ObtenerInscritoUseCase` y `ListarInscritosUseCase` (mapeo).

## Task 3: Infraestructura + API
- `InscritoRepository` (EF, proyección sin firma, traducción de índice único), DI en `Program.cs`, `InscritosController` (`api/inscritos`, Admin).
- Tests `InscritosEndpointTests` (token admin vía `IJwtTokenGenerator.Generate(AdminUser)`): lista sin firma, filtro por capacitación, búsqueda, detalle con firma, PUT 200 y 409, 401 sin token.

## Task 4: Frontend
- `services/inscritos.js`, `pages/inscritos/InscritosPage.jsx`, ruta y enlace en Sidebar.
- Verificación: eslint de archivos tocados + build + prueba manual en navegador contra backend local.

## Task 5: Cierre
- `instrucciones.md` §7.16, suite completo (≤ 31 fallas), revisión final, merge a `main`, push, despliegue con `desplegar.sh` (comparar → respaldar → aplicar → web → verificar).
