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
  descargarCertificado,
};
