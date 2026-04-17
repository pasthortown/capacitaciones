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

export default {
  listCapacitaciones,
  getCapacitacion,
  createCapacitacion,
  updateCapacitacion,
  deleteCapacitacion,
};
