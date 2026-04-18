/**
 * Servicio de Capacitaciones.
 *
 * Contrato API (ver instrucciones.md §7.1 y fase 3):
 *   GET    /api/capacitaciones?includeInactive=false|true  -> CapacitacionSummary[]
 *   GET    /api/capacitaciones/{id}                        -> CapacitacionDetail
 *   POST   /api/capacitaciones                             -> 201 CapacitacionDetail
 *   PUT    /api/capacitaciones/{id}                        -> 200 CapacitacionDetail
 *   DELETE /api/capacitaciones/{id}                        -> 204 (lógico)
 *
 * Todos los endpoints requieren Bearer (policy Admin).
 */

import http from './http.js';

const BASE = '/capacitaciones';

/**
 * Lista capacitaciones (resumen).
 *
 * @param {boolean} [includeInactive=false]
 * @returns {Promise<Array>}
 */
export function listCapacitaciones(includeInactive = false) {
  const query = includeInactive ? '?includeInactive=true' : '?includeInactive=false';
  return http.get(`${BASE}${query}`);
}

/**
 * Obtiene el detalle completo de una capacitación, incluyendo responsables.
 *
 * @param {string} id
 * @returns {Promise<object>}
 */
export function getCapacitacion(id) {
  return http.get(`${BASE}/${id}`);
}

/**
 * Crea una nueva capacitación.
 *
 * @param {object} payload
 * @returns {Promise<object>}
 */
export function createCapacitacion(payload) {
  return http.post(BASE, payload);
}

/**
 * Actualiza una capacitación existente.
 *
 * @param {string} id
 * @param {object} payload
 * @returns {Promise<object>}
 */
export function updateCapacitacion(id, payload) {
  return http.put(`${BASE}/${id}`, payload);
}

/**
 * Elimina lógicamente una capacitación (marcar como inactiva).
 *
 * @param {string} id
 * @returns {Promise<void>}
 */
export function deleteCapacitacion(id) {
  return http.del(`${BASE}/${id}`);
}

/**
 * Genera (o regenera) el link firmado para la página del capacitador.
 *
 * POST /api/capacitaciones/{id}/link-capacitador
 *   → 200 { url, token, expiresAt }
 *
 * `url` es relativa (ej. `/capacitador?token=...`). El caller la
 * concatena con `window.location.origin` para obtener la URL completa.
 *
 * @param {string} id
 * @returns {Promise<{ url: string, token: string, expiresAt: string }>}
 */
export function generateLinkCapacitador(id) {
  return http.post(`${BASE}/${id}/link-capacitador`);
}

/**
 * Genera (o regenera) el link firmado para la página pública de inscripción.
 *
 * POST /api/capacitaciones/{id}/link-inscripcion
 *   → 200 { url, token, expiresAt }
 *
 * `url` es relativa (ej. `/inscripcion?token=...`). El caller la
 * concatena con `window.location.origin` para obtener la URL completa.
 *
 * @param {string} id
 * @returns {Promise<{ url: string, token: string, expiresAt: string }>}
 */
export function generateLinkInscripcion(id) {
  return http.post(`${BASE}/${id}/link-inscripcion`);
}

/**
 * Genera (en lote) los certificados de todos los asistentes de una capacitación.
 *
 * POST /api/capacitaciones/{id}/certificados/generar
 *   → 200 { total, emitidos, errores: [{ asistenteId, codigo, mensaje }] }
 *   → 409 { error: 'CAPACITACION_NO_FINALIZADA', message } si la capacitación
 *     aún no está finalizada.
 *
 * El backend responde 200 incluso cuando hay errores parciales; el caller
 * debe inspeccionar `errores.length` para mostrar el detalle.
 *
 * @param {string} capacitacionId
 * @returns {Promise<{ total: number, emitidos: number, errores: Array<{ asistenteId?: string, codigo?: string, mensaje: string }> }>}
 */
export function generarCertificados(capacitacionId) {
  return http.post(`${BASE}/${capacitacionId}/certificados/generar`);
}

export default {
  listCapacitaciones,
  getCapacitacion,
  createCapacitacion,
  updateCapacitacion,
  deleteCapacitacion,
  generateLinkCapacitador,
  generateLinkInscripcion,
  generarCertificados,
};
