# Registro de personas inscritas y reuso de firma — Diseño

**Fecha:** 2026-09-30
**Rama:** `feature/persona-inscrita` (desde `main`)
**Estado:** aprobado en conversación, pendiente de revisión escrita

## 1. Objetivo

En la página pública de **Inscripción** (`/inscripcion?token=...`):

1. Guardar un **registro de cada persona** que se inscribe (cédula, nombres, apellidos, área, correo, firma), independiente de cada capacitación.
2. Al **escribir la cédula**, autocompletar nombres, apellidos, área y correo si la persona ya está registrada.
3. **Reusar la firma guardada** en nuevas inscripciones sin tener que dibujarla o subirla otra vez.

La subida de firma como imagen (PNG/JPG) **ya existe** en `SignaturePad` (pestaña "Subir archivo"); no se modifica.

### Contexto de uso

El formulario lo llenan tanto cada asistente desde su equipo como un organizador que inscribe a varias personas desde el mismo equipo ("Inscribir a otra persona"). La página es pública: cualquiera con el enlace puede usarla.

### Decisiones tomadas

| Tema | Decisión |
| ---- | -------- |
| Firma guardada | Se **reusa sin mostrarse**: la API nunca devuelve la imagen, solo `tieneFirma`. El servidor la copia a la inscripción. |
| Origen de datos del autocompletado | **Solo el registro propio** (`PersonaInscrita`). No se consulta DOS/ControlTareas ni Externos. |
| Enfoque | **Tabla nueva** `dbo.PersonaInscrita`, con backfill desde las inscripciones existentes. |

## 2. Fuera de alcance

- Pantalla de administración de personas registradas.
- Autocompletar desde ControlTareas o desde Colaboradores Externos.
- Actualizar `PersonaInscrita` cuando un admin cambia el correo de un asistente (`ActualizarEmailAsistenteUseCase`).
- Cambios en `SignaturePad`.

## 3. Modelo de datos

### Entidad `PersonaInscrita` (Domain)

| Campo | Tipo | Notas |
| ----- | ---- | ----- |
| `Id` | `Guid` | PK |
| `Identificacion` | `string` (30) | Requerido. **Único** (`UX_PersonaInscrita_Identificacion`). Trim, sin normalizar mayúsculas (mismo criterio que `Asistente`). |
| `Nombres` | `string` (120) | Requerido |
| `Apellidos` | `string` (120) | Requerido |
| `AreaId` | `Guid?` | FK a `Area`, `Restrict`. Nullable por si el área se da de baja lógica. |
| `EmailUsuario` | `string` (255) | Email completo con `@dos.com.ec`, igual que `Asistente`. |
| `Firma` | `string?` (max) | Base64/data URL. Null = sin firma guardada. |
| `FechaCreacion` | `DateTime` | UTC |
| `FechaActualizacion` | `DateTime` | UTC, se actualiza en cada inscripción |

Tabla `dbo.PersonaInscrita`, configuración EF en `Persistence/Configurations/PersonaInscritaConfiguration.cs`.

### Migración `AddPersonaInscrita`

1. Crea la tabla y el índice único.
2. **Backfill**: inserta una fila por `Identificacion` distinta de `dbo.Asistente`, tomando la inscripción más reciente:

```sql
INSERT INTO dbo.PersonaInscrita (Id, Identificacion, Nombres, Apellidos, AreaId, EmailUsuario, Firma, FechaCreacion, FechaActualizacion)
SELECT NEWID(), a.Identificacion, a.Nombres, a.Apellidos, a.AreaId, a.EmailUsuario, NULLIF(a.Firma, ''), a.FechaInscripcion, a.FechaInscripcion
FROM (
  SELECT *, ROW_NUMBER() OVER (PARTITION BY Identificacion ORDER BY FechaInscripcion DESC) AS rn
  FROM dbo.Asistente
) a
WHERE a.rn = 1;
```

`Down` elimina la tabla (los datos de `Asistente` no se tocan).

## 4. Backend

### Puerto `IPersonaInscritaRepository` (Application/Ports)

- `Task<PersonaInscrita?> GetByIdentificacionAsync(string identificacion, CancellationToken ct)`
- Alta/actualización integrada en la misma operación que guarda el `Asistente` (ver §4.3).

### 4.1 Consulta por cédula — `BuscarPersonaInscritaUseCase`

`GET /api/inscripcion/capacitacion/persona/{identificacion}` en `InscripcionController` (policy `Inscripcion`, mismo token del enlace).

- Trim de la identificación; vacía → `400`.
- No existe → `404`.
- Existe → `200`:

```json
{
  "nombres": "María Fernanda",
  "apellidos": "Pérez Torres",
  "areaId": "guid-o-null",
  "emailUsuario": "maria.perez",
  "tieneFirma": true
}
```

- `emailUsuario` se devuelve **sin** el sufijo `@dos.com.ec` (el formulario captura solo la parte local).
- `areaId` se devuelve solo si el área sigue activa; si no, `null`.
- **Nunca** se devuelve la firma.

### 4.2 Cambios en `CreateInscripcionDto`

Nuevo campo `bool UsarFirmaRegistrada` (default `false`). `Firma` pasa a ser opcional cuando `UsarFirmaRegistrada = true`.

### 4.3 Cambios en `InscribirAsistenteUseCase`

Después de las validaciones actuales:

1. **Resolver la firma:**
   - Si `UsarFirmaRegistrada = true`: busca `PersonaInscrita` por identificación; si no existe o no tiene firma → `400 FIRMA_REGISTRADA_NO_DISPONIBLE`. Si existe, usa su firma.
   - Si no: `Firma` es requerida como hoy (`CAMPO_REQUERIDO`).
2. Crea el `Asistente` como hoy, con la firma resuelta.
3. **Alta o actualización de `PersonaInscrita`** con nombres, apellidos, área, correo y fecha actualizados. La firma solo se reemplaza si llegó una firma nueva.
4. Los pasos 2 y 3 se guardan **atómicamente** (un solo `SaveChanges`/transacción). Si la inscripción es duplicada, no se toca la persona.
5. Carrera por el índice único de `PersonaInscrita` (dos inscripciones simultáneas de una cédula nueva a capacitaciones distintas): no se reintenta automáticamente. El repositorio la traduce a `409 INSCRIPCION_CONCURRENTE` ("Vuelve a intentarlo") y no se guarda nada; al reenviar, la persona ya existe y se actualiza.

Nuevos códigos en `ToProblem` del controlador: `FIRMA_REGISTRADA_NO_DISPONIBLE` → `400`, `INSCRIPCION_CONCURRENTE` → `409`.

## 5. Frontend

### `services/inscripcion.js`

- `buscarPersona(token, identificacion)` → `GET /inscripcion/capacitacion/persona/{id}` (usa `requestWithToken`; `404` se traduce a `null`).
- `inscribir` acepta `usarFirmaRegistrada`.

### `InscripcionPage.jsx`

- **Orden de campos:** `Identificación` pasa a ser el **primer campo** del formulario, para que el autocompletado ocurra antes de escribir el nombre.
- **Al salir del campo** (`onBlur`) con identificación no vacía y distinta a la última consultada: llama `buscarPersona`.
  - Encontrada → completa `nombres`, `apellidos`, `emailUsuario` y `areaId` (este último solo si está en la lista de áreas). Los campos siguen editables. Mensaje discreto: "Encontramos tus datos. Revísalos antes de inscribirte."
  - `tieneFirma = true` → muestra la casilla **"Usar mi firma registrada"** marcada, y oculta `SignaturePad`. Al desmarcar, reaparece `SignaturePad` para dibujar o subir otra.
  - No encontrada → no cambia nada.
  - Error de red → se ignora silenciosamente (el formulario funciona como hoy).
- Si el usuario **cambia la identificación** después de un autocompletado, se desmarca y oculta "Usar mi firma registrada" hasta la siguiente consulta.
- `validate()`: la firma es obligatoria salvo que "Usar mi firma registrada" esté marcada.
- `resetForm()` ("Inscribir a otra persona") limpia también el estado de autocompletado y la casilla.
- Mensaje para `FIRMA_REGISTRADA_NO_DISPONIBLE`: "No encontramos una firma registrada para esta identificación. Dibuja o sube tu firma."

Estilos solo con las clases del design system (`./style/`) y el CSS module existente.

## 6. Seguridad y privacidad

- La consulta requiere un token de inscripción válido; no hay endpoint anónimo.
- La firma **nunca** sale del servidor por la API pública.
- Riesgo aceptado: quien tenga un enlace y conozca una cédula ya registrada puede ver nombres, área y correo de esa persona e inscribirla con su firma guardada. Es coherente con el caso de uso del organizador.

## 7. Pruebas

Backend (`backend/tests/Capacitaciones.Tests`, xUnit, mismo estilo que `InscribirAsistenteUseCaseTests`):

- `BuscarPersonaInscritaUseCase`: encontrada (sin firma en DTO, `tieneFirma`, email sin sufijo), no encontrada, área inactiva → `areaId` null.
- `InscribirAsistenteUseCase`:
  - Primera inscripción crea `PersonaInscrita` con firma.
  - Segunda inscripción actualiza datos y conserva la firma si se usó `UsarFirmaRegistrada`.
  - Firma nueva reemplaza la guardada.
  - `UsarFirmaRegistrada` sin persona o sin firma → `FIRMA_REGISTRADA_NO_DISPONIBLE`.
  - Inscripción duplicada no modifica la persona.
- Endpoint: `GET .../persona/{id}` con token válido (200/404) y sin token (401).

Frontend: verificación manual en el navegador (flujo nuevo, flujo con cédula existente, desmarcar firma, "Inscribir a otra persona").
