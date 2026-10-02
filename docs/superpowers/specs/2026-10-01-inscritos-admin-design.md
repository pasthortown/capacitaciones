# Pantalla de administración "Inscritos" — Diseño

**Fecha:** 2026-10-01 · **Rama:** `feature/inscritos-admin` · **Estado:** aprobado en conversación ("implementa y despliega")

## Objetivo
Que un admin vea a las personas inscritas a las capacitaciones, las filtre por capacitación, las edite y vea la firma para saber si quedó bien subida.

## Decisiones
| Tema | Decisión |
| ---- | -------- |
| Ubicación | Pantalla nueva **Inscritos** (`/inscritos`) en el menú lateral, debajo de Capacitaciones. |
| Editables | Nombres, apellidos, cédula, área, correo (parte local) y firma. |
| Registro de personas | Al guardar se actualiza también `PersonaInscrita` de la cédula final. |

## Backend (policy `Admin`, `api/inscritos`)
- `GET /api/inscritos?capacitacionId=&buscar=` → `InscritoDto[]` **sin firma** (`tieneFirma`). `buscar` filtra por cédula, nombres, apellidos o correo (contiene). Orden: fecha de inscripción descendente. Solo capacitaciones activas (filtro global existente).
- `GET /api/inscritos/{id}` → `InscritoDetalleDto` = `InscritoDto` + `firma` + `emailUsuario` (parte local). 404 si no existe.
- `PUT /api/inscritos/{id}` body `{ nombres, apellidos, identificacion, areaId, emailUsuario, firma? }` → `InscritoDetalleDto`.
  - Trim; requeridos → 400 `CAMPO_REQUERIDO`; `@` en emailUsuario → 400 `EMAIL_INVALIDO`; área inexistente/inactiva → 400 `AREA_INVALIDA`; cédula repetida en la misma capacitación (otro asistente) → 409 `INSCRIPCION_DUPLICADA`; no existe → 404.
  - `firma` null/vacía = conservar la actual.
  - `PersonaInscrita` de la cédula final: si no existe se crea con los datos y la firma final; si existe se actualizan nombres/apellidos/área/correo/fecha, y la firma solo si se envió una nueva o la persona no tenía.
  - Asistente + persona en un solo `SaveChanges`.
- Nuevo puerto `IInscritoRepository` (lista proyectada sin firma, `GetForEditAsync` tracked, `ExistsOtroConIdentificacionAsync`, `SaveChangesAsync`).

## Frontend
- `services/inscritos.js` (`listInscritos`, `getInscrito`, `updateInscrito`).
- `pages/inscritos/InscritosPage.jsx`: toolbar con combo de capacitación ("Todas") y buscador; `DataTable` con cédula, nombres, apellidos, área, correo, capacitación, fecha, firma (Sí/Falta); acciones **Ver firma** (modal con la imagen sobre fondo blanco + dimensiones y peso) y **Editar** (modal con campos, `EmailConSufijo`, vista previa de firma y "Reemplazar firma" → `SignaturePad`).
- Ruta `/inscritos` en `App.jsx` y enlace en `Sidebar`.

## Pruebas
Unitarias de `EditarInscritoUseCase` (happy path, conservar/reemplazar firma, duplicado, área inválida, email con @, alta/actualización de persona) y de endpoint (`GET` sin firma y filtrado por capacitación, `GET {id}` con firma, `PUT` 409 por duplicado, 401 sin token). Verificación manual en navegador y despliegue con `desplegar.sh`.
