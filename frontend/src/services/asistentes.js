/**
 * Servicio admin para la gestión de Asistentes de una capacitación.
 *
 * Contrato API (ver instrucciones.md §7.2.5 y §7.2.6):
 *   GET /api/capacitaciones/{capId}/asistentes
 *     -> 200 [{ id, nombres, apellidos, identificacion, email, area:{id,nombre}, fechaInscripcion }]
 *   GET /api/capacitaciones/{capId}/asistentes/{asistenteId}/certificado
 *     -> 200 application/pdf con Content-Disposition: attachment; filename="{codigo}_{identificacion}.pdf"
 *     -> 409 { error: 'CAPACITACION_NO_FINALIZADA', message }
 *     -> 409 { error: 'FIRMAS_FALTANTES', message, faltantes: [string,...] }
 *     -> 503 { error: 'SERVICIO_EMISOR_NO_DISPONIBLE', message }
 *     -> 404 si el asistente o capacitación no existe.
 *
 * Todos los endpoints requieren Bearer (policy Admin) — usan `http.js`.
 */

import http from './http.js';

const BASE = '/capacitaciones';

/**
 * Lista los asistentes inscritos a una capacitación.
 *
 * @param {string} capacitacionId
 * @returns {Promise<Array>}
 */
export function listByCapacitacion(capacitacionId) {
  return http.get(`${BASE}/${capacitacionId}/asistentes`);
}

/**
 * Marca (o corrige) la asistencia de un asistente desde el panel admin.
 *
 * PUT /api/capacitaciones/{capacitacionId}/asistentes/{asistenteId}/asistencia
 *   body: { estadoAsistencia: "Presente" | "Ausente" | null }
 *   → 200 { id, estadoAsistencia, fechaMarcacionAsistencia }
 *
 * @param {string} capacitacionId
 * @param {string} asistenteId
 * @param {'Presente' | 'Ausente' | null} estadoAsistencia
 * @returns {Promise<{ id: string, estadoAsistencia: 'Presente' | 'Ausente' | null, fechaMarcacionAsistencia: string | null }>}
 */
export function marcarAsistenciaAdmin(capacitacionId, asistenteId, estadoAsistencia) {
  return http.put(
    `${BASE}/${capacitacionId}/asistentes/${asistenteId}/asistencia`,
    { estadoAsistencia },
  );
}

/**
 * Registra (o actualiza) la calificación de un asistente desde el panel admin (Fase 11).
 *
 * PUT /api/capacitaciones/{capacitacionId}/asistentes/{asistenteId}/calificacion
 *   body: { calificacion: number | null }
 *   → 200 { id, calificacion }
 *   → 400 CALIFICACION_FUERA_DE_RANGO
 *   → 409 ASISTENTE_NO_PRESENTE
 *   → 409 CALIFICACIONES_NO_APLICA (la capacitación no es Aprobación)
 *
 * @param {string} capacitacionId
 * @param {string} asistenteId
 * @param {number | null} calificacion
 * @returns {Promise<{ id: string, calificacion: number | null }>}
 */
export function calificarAsistenteAdmin(capacitacionId, asistenteId, calificacion) {
  return http.put(
    `${BASE}/${capacitacionId}/asistentes/${asistenteId}/calificacion`,
    { calificacion },
  );
}

/**
 * Descarga el certificado del asistente.
 *
 * Usa `http.downloadBlob`, que:
 *  - Respeta `Content-Disposition` del backend (filename sugerido).
 *  - Dispara el click en `<a download>` automáticamente (el navegador
 *    abre el diálogo de guardado).
 *  - Relanza `HttpError` con `body.error`, `body.message` y —si aplica—
 *    `body.faltantes` para que el caller pueda distinguir entre
 *    `FIRMAS_FALTANTES`, `CAPACITACION_NO_FINALIZADA`,
 *    `SERVICIO_EMISOR_NO_DISPONIBLE`, etc.
 *
 * @param {string} capacitacionId
 * @param {string} asistenteId
 * @param {string} [fallbackFilename='certificado.pdf'] - Nombre a usar
 *   si el backend no envía Content-Disposition. Recomendado:
 *   `${codigo}_${identificacion}.pdf`.
 * @returns {Promise<{ blob: Blob, filename: string }>}
 */
export function descargarCertificado(
  capacitacionId,
  asistenteId,
  fallbackFilename = 'certificado.pdf',
) {
  return http.downloadBlob(
    `${BASE}/${capacitacionId}/asistentes/${asistenteId}/certificado`,
    { fallbackFilename },
  );
}

export default {
  listByCapacitacion,
  marcarAsistenciaAdmin,
  calificarAsistenteAdmin,
  descargarCertificado,
};
