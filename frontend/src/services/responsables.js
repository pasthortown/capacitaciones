/**
 * Servicio de Responsables (catálogo global, admin).
 *
 * Los Responsables pasaron a ser un catálogo global, con su propio CRUD y link
 * firmado para que el responsable cargue su firma sin login. En el modal de
 * capacitación sólo se seleccionan y se ordenan (ver `services/capacitaciones.js`).
 *
 * Contrato API:
 *   GET    /api/responsables?includeInactive=false|true
 *     -> ResponsableSummaryDto[]
 *        { id, nombres, cargo, empresa, tieneFirma, activo, fechaCreacion, fechaActualizacion }
 *   GET    /api/responsables/{id}
 *     -> ResponsableDetailDto (summary + `firma` base64 nullable)
 *   POST   /api/responsables     body: { nombres, cargo, empresa, firma? }
 *     -> 201 Detail
 *   PUT    /api/responsables/{id} body: { nombres, cargo, empresa, firma? }
 *     -> 200 Detail
 *   DELETE /api/responsables/{id} -> 204 (baja lógica: activo=false)
 *   POST   /api/responsables/{id}/link
 *     -> { url: "/responsable?token=...", token, expiresAt }
 *
 * Todos los endpoints requieren Bearer (policy Admin).
 */

import http from './http.js';

const BASE = '/responsables';

/**
 * Lista responsables (resumen).
 *
 * @param {boolean} [includeInactive=false]
 * @returns {Promise<Array>}
 */
export function list(includeInactive = false) {
  const query = includeInactive ? '?includeInactive=true' : '?includeInactive=false';
  return http.get(`${BASE}${query}`);
}

/**
 * Obtiene el detalle completo de un responsable (incluye firma base64).
 *
 * @param {string} id
 * @returns {Promise<object>}
 */
export function get(id) {
  return http.get(`${BASE}/${id}`);
}

/**
 * Crea un nuevo responsable.
 *
 * @param {{ nombres: string, cargo: string, empresa: string, firma?: string|null }} payload
 * @returns {Promise<object>}
 */
export function create(payload) {
  return http.post(BASE, payload);
}

/**
 * Actualiza un responsable existente.
 *
 * @param {string} id
 * @param {{ nombres: string, cargo: string, empresa: string, firma?: string|null }} payload
 * @returns {Promise<object>}
 */
export function update(id, payload) {
  return http.put(`${BASE}/${id}`, payload);
}

/**
 * Eliminación lógica (marca `activo=false`).
 *
 * @param {string} id
 * @returns {Promise<void>}
 */
export function del(id) {
  return http.del(`${BASE}/${id}`);
}

/**
 * Genera (o regenera) el link firmado para la página pública del responsable.
 *
 * POST /api/responsables/{id}/link
 *   → 200 { url, token, expiresAt }
 *
 * `url` es relativa (ej. `/responsable?token=...`). El caller concatena con
 * `window.location.origin` para obtener la URL completa.
 *
 * @param {string} id
 * @returns {Promise<{ url: string, token: string, expiresAt: string }>}
 */
export function generateLink(id) {
  return http.post(`${BASE}/${id}/link`);
}

export default {
  list,
  get,
  create,
  update,
  del,
  generateLink,
};
