/**
 * Servicio de Catálogos (Modalidades, Tipos de Actividad, Áreas).
 *
 * Todos los catálogos exponen el mismo contrato, parametrizado por `slug`:
 *   - modalidades
 *   - tipos-actividad
 *   - areas
 *
 * Contrato API (ver instrucciones.md §7.1 y §7.5):
 *   GET    /api/catalogos/{slug}?includeInactive=true|false
 *   GET    /api/catalogos/{slug}/{id}
 *   POST   /api/catalogos/{slug}              body: { nombre, activo }
 *   PUT    /api/catalogos/{slug}/{id}         body: { nombre, activo }
 *   DELETE /api/catalogos/{slug}/{id}         -> 204 (eliminación lógica)
 *   GET    /api/catalogos/{slug}/plantilla    -> XLSX binario
 *   POST   /api/catalogos/{slug}/importar     multipart: field "file"
 */

import http from './http.js';

/**
 * Slugs válidos. El caller debe pasar uno de estos para que no se formen
 * URLs arbitrarias.
 */
export const CATALOGO_SLUGS = Object.freeze({
  MODALIDADES: 'modalidades',
  TIPOS_ACTIVIDAD: 'tipos-actividad',
  AREAS: 'areas',
});

function basePath(slug) {
  return `/catalogos/${slug}`;
}

/** Lista los registros del catálogo. */
export function list(slug, { includeInactive = false } = {}) {
  const query = includeInactive ? '?includeInactive=true' : '?includeInactive=false';
  return http.get(`${basePath(slug)}${query}`);
}

/** Obtiene un registro por id. */
export function get(slug, id) {
  return http.get(`${basePath(slug)}/${id}`);
}

/** Crea un nuevo registro. */
export function create(slug, { nombre, activo = true }) {
  return http.post(basePath(slug), { nombre, activo });
}

/** Actualiza un registro existente. */
export function update(slug, id, { nombre, activo }) {
  return http.put(`${basePath(slug)}/${id}`, { nombre, activo });
}

/** Eliminación lógica del registro. */
export function remove(slug, id) {
  return http.del(`${basePath(slug)}/${id}`);
}

/** Descarga la plantilla XLSX vacía. */
export function downloadTemplate(slug) {
  return http.downloadBlob(`${basePath(slug)}/plantilla`, {
    fallbackFilename: `plantilla_${slug}.xlsx`,
  });
}

/**
 * Sube un archivo XLSX para importación masiva.
 * @param {string} slug
 * @param {File}   file
 * @returns {Promise<{ totalFilas:number, filasValidas:number, errores: {fila:number, campo:string, mensaje:string}[] }>}
 */
export function uploadTemplate(slug, file) {
  const formData = new FormData();
  formData.append('file', file, file.name);
  return http.uploadForm(`${basePath(slug)}/importar`, formData);
}

export default {
  SLUGS: CATALOGO_SLUGS,
  list,
  get,
  create,
  update,
  remove,
  downloadTemplate,
  uploadTemplate,
};
