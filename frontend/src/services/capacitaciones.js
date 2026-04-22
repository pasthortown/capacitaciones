/**
 * Servicio de Capacitaciones.
 *
 * Contrato API (ver instrucciones.md §7.1 y fase 3):
 *   GET    /api/capacitaciones             -> CapacitacionSummary[] (solo Activo=true)
 *   GET    /api/capacitaciones/{id}        -> CapacitacionDetail
 *   POST   /api/capacitaciones             -> 201 CapacitacionDetail
 *   PUT    /api/capacitaciones/{id}        -> 200 CapacitacionDetail
 *   DELETE /api/capacitaciones/{id}        -> 204 (soft-delete — las filas con
 *                                              Activo=false solo son visibles vía BDD)
 *
 * Todos los endpoints requieren Bearer (policy Admin).
 */

import http from './http.js';

const BASE = '/capacitaciones';

/**
 * Extensiones permitidas para el logo de capacitación (whitelist,
 * lowercase, sin punto). Coincide con la política del backend (ver
 * instrucciones.md §7.8).
 */
export const LOGO_ALLOWED_EXTENSIONS = new Set(['png', 'jpg', 'jpeg', 'webp', 'svg']);

/**
 * MIMEs aceptados por el `<input type="file">` del logo.
 * Se pasan tal cual al atributo `accept`.
 */
export const LOGO_ACCEPT_MIMES = 'image/png,image/jpeg,image/webp,image/svg+xml';

/**
 * Tamaño máximo del logo (2 MB) — alineado con backend.
 */
export const LOGO_MAX_SIZE_BYTES = 2 * 1024 * 1024;

/**
 * Devuelve la extensión lowercase sin punto, o cadena vacía.
 *
 * @param {string} filename
 * @returns {string}
 */
export function getLogoExtension(filename) {
  if (!filename || typeof filename !== 'string') return '';
  const idx = filename.lastIndexOf('.');
  if (idx < 0 || idx === filename.length - 1) return '';
  return filename.slice(idx + 1).toLowerCase();
}

/**
 * Lista capacitaciones (resumen). El backend aplica un filter global y solo
 * devuelve las que tienen Activo=true; las soft-deleted son invisibles desde la API.
 *
 * @returns {Promise<Array>}
 */
export function listCapacitaciones() {
  return http.get(BASE);
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
 * Genera (o regenera) el link firmado para la pantalla pública de pase de lista.
 *
 * POST /api/capacitaciones/{id}/link-pase-lista
 *   → 200 { url, token, expiresAt }
 *
 * `url` es relativa (ej. `/capacitador/pase-lista?token=...`). El caller la
 * concatena con `window.location.origin` para obtener la URL completa.
 *
 * @param {string} id
 * @returns {Promise<{ url: string, token: string, expiresAt: string }>}
 */
export function generateLinkPaseLista(id) {
  return http.post(`${BASE}/${id}/link-pase-lista`);
}

/**
 * Genera (o regenera) el link firmado para la pantalla pública de calificaciones.
 *
 * POST /api/capacitaciones/{id}/link-calificaciones
 *   → 200 { url, token, expiresAt }
 *   → 409 { error: 'CALIFICACIONES_NO_APLICA', message } si la capacitación
 *     no es `TipoCertificacion == Aprobacion`.
 *
 * `url` es relativa (ej. `/capacitador/calificaciones?token=...`). El caller la
 * concatena con `window.location.origin` para obtener la URL completa.
 *
 * @param {string} id
 * @returns {Promise<{ url: string, token: string, expiresAt: string }>}
 */
export function generateLinkCalificaciones(id) {
  return http.post(`${BASE}/${id}/link-calificaciones`);
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

/**
 * Sube (o reemplaza) el logo de una capacitación.
 *
 * POST /api/capacitaciones/{id}/logo (multipart, campo `archivo`)
 *   → 201 { logoPath, logoContentType, logoUrl }
 *
 * El backend valida whitelist de extensiones y tamaño (≤2 MB); si ya había
 * un logo previo, lo borra físicamente y reemplaza.
 *
 * @param {string} id
 * @param {File}   file
 * @returns {Promise<{ logoPath: string, logoContentType: string, logoUrl: string }>}
 */
export function uploadLogoCapacitacion(id, file) {
  const formData = new FormData();
  formData.append('archivo', file);
  return http.uploadForm(`${BASE}/${id}/logo`, formData);
}

/**
 * Elimina el logo físico y limpia las columnas de la capacitación.
 *
 * DELETE /api/capacitaciones/{id}/logo → 204
 *
 * @param {string} id
 * @returns {Promise<void>}
 */
export function deleteLogoCapacitacion(id) {
  return http.del(`${BASE}/${id}/logo`);
}

export default {
  listCapacitaciones,
  getCapacitacion,
  createCapacitacion,
  updateCapacitacion,
  deleteCapacitacion,
  generateLinkCapacitador,
  generateLinkInscripcion,
  generateLinkPaseLista,
  generateLinkCalificaciones,
  generarCertificados,
  uploadLogoCapacitacion,
  deleteLogoCapacitacion,
  LOGO_ALLOWED_EXTENSIONS,
  LOGO_ACCEPT_MIMES,
  LOGO_MAX_SIZE_BYTES,
  getLogoExtension,
};
