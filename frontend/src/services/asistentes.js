/**
 * Servicio admin para la gestión de Asistentes de una capacitación.
 *
 * Contrato API (ver instrucciones.md §7.2.5 y §7.2.6, fase 5):
 *   GET /api/capacitaciones/{capId}/asistentes
 *     -> 200 [{ id, nombres, apellidos, identificacion, email, area:{id,nombre}, fechaInscripcion }]
 *   GET /api/capacitaciones/{capId}/asistentes/{asistenteId}/certificado
 *     -> (Fase 6) 200 application/pdf
 *     -> (Fase 5 stub) 501 "pendiente" si backend aún no emite PDF, o
 *        409 si la capacitación no está Finalizada.
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
 * IMPORTANTE — Stub de Fase 5:
 *  El backend aún no emite PDFs en Fase 5; responderá 501 "pendiente" (o 409
 *  si la capacitación no está Finalizada). Por eso actualmente usamos
 *  `http.get` para que el error se propague como `HttpError` al caller y
 *  éste muestre el toast informativo correspondiente.
 *
 *  TODO Fase 6: cuando el backend emita blob PDF real, cambiar esta función
 *  a `http.downloadBlob(...)` con un `fallbackFilename` basado en el código
 *  de la capacitación + identificación del asistente. Ver `http.js`:
 *  `downloadBlob` ya se encarga del `Content-Disposition` y el click en
 *  `<a download>`.
 *
 * @param {string} capacitacionId
 * @param {string} asistenteId
 * @returns {Promise<any>}
 */
export function descargarCertificado(capacitacionId, asistenteId) {
  return http.get(
    `${BASE}/${capacitacionId}/asistentes/${asistenteId}/certificado`,
  );
}

export default {
  listByCapacitacion,
  descargarCertificado,
};
