/**
 * Servicio de Recursos (repositorio de archivos compartidos, admin).
 *
 * Los Recursos son archivos que el admin sube y comparte mediante enlaces
 * públicos de descarga. La API maneja el almacenamiento y devuelve un link
 * relativo (`/api/publico/recursos/{id}/descargar`) que el caller concatena
 * con `window.location.origin` al copiar al portapapeles.
 *
 * Contrato API:
 *   GET    /api/recursos?includeInactive=false|true
 *     -> RecursoSummaryDto[]
 *   GET    /api/recursos/{id}
 *     -> RecursoDetailDto
 *   POST   /api/recursos   (multipart/form-data: archivo, nombre?, descripcion)
 *     -> 201 Detail
 *   PUT    /api/recursos/{id}   (multipart/form-data: nombreOriginal, descripcion, archivo?)
 *     -> 200 Detail   // si llega `archivo`, reemplaza el binario físico
 *   DELETE /api/recursos/{id}   -> 204 (baja lógica)
 *   POST   /api/recursos/{id}/link
 *     -> { url, recursoId, nombreOriginal, tamanoBytes, contentType }
 *
 * Todos los endpoints requieren Bearer (policy Admin).
 */

import http from './http.js';

const BASE = '/recursos';

/**
 * Extensiones prohibidas por política de seguridad (lowercase, sin punto).
 * Incluye ejecutables y scripts comunes.
 */
export const BLOCKED_EXTENSIONS = new Set([
  // Ejecutables
  'exe', 'msi', 'com', 'scr', 'dll', 'bat', 'cmd', 'bin',
  'apk', 'app', 'dmg', 'deb', 'rpm', 'jar', 'war',
  // Scripts
  'sh', 'bash', 'zsh', 'ksh', 'ps1', 'psm1', 'psd1',
  'vbs', 'vbe', 'wsf', 'wsh',
  'js', 'jse', 'mjs', 'cjs', 'ts',
  'py', 'pyc', 'pyw',
  'rb', 'pl', 'php', 'phtml',
  'reg', 'lnk', 'htaccess',
]);

/**
 * Tamaño máximo permitido (100 MB).
 */
export const MAX_SIZE_BYTES = 100_000_000;

/**
 * Extrae la extensión de un nombre de archivo.
 * Retorna lowercase sin punto, o cadena vacía si no tiene extensión.
 *
 * @param {string} filename
 * @returns {string}
 */
export function getExtension(filename) {
  if (!filename || typeof filename !== 'string') return '';
  const idx = filename.lastIndexOf('.');
  if (idx < 0 || idx === filename.length - 1) return '';
  return filename.slice(idx + 1).toLowerCase();
}

/**
 * Indica si la extensión del archivo está en la lista de bloqueados.
 *
 * @param {string} filename
 * @returns {boolean}
 */
export function isBlockedExtension(filename) {
  const ext = getExtension(filename);
  if (!ext) return false;
  return BLOCKED_EXTENSIONS.has(ext);
}

/**
 * Formatea un tamaño en bytes a una representación humana con unidades
 * `B`/`KB`/`MB`/`GB`. Usa 1-2 decimales según la magnitud.
 *
 * @param {number} n
 * @returns {string}
 */
export function formatBytes(n) {
  if (!Number.isFinite(n) || n < 0) return '0 B';
  if (n < 1024) return `${n} B`;
  const kb = n / 1024;
  if (kb < 1024) return `${kb.toFixed(kb < 10 ? 2 : 1)} KB`;
  const mb = kb / 1024;
  if (mb < 1024) return `${mb.toFixed(mb < 10 ? 2 : 1)} MB`;
  const gb = mb / 1024;
  return `${gb.toFixed(gb < 10 ? 2 : 1)} GB`;
}

/**
 * Lista recursos (resumen).
 *
 * @param {boolean} [includeInactive=false]
 * @returns {Promise<Array>}
 */
export function listRecursos(includeInactive = false) {
  const query = includeInactive ? '?includeInactive=true' : '?includeInactive=false';
  return http.get(`${BASE}${query}`);
}

/**
 * Obtiene el detalle completo de un recurso.
 *
 * @param {string} id
 * @returns {Promise<object>}
 */
export function getRecurso(id) {
  return http.get(`${BASE}/${id}`);
}

/**
 * Sube un nuevo recurso al repositorio.
 *
 * @param {{ archivo: File, nombre?: string, descripcion: string }} payload
 * @returns {Promise<object>}
 */
export function uploadRecurso({ archivo, nombre, descripcion }) {
  const formData = new FormData();
  formData.append('archivo', archivo);
  if (nombre !== undefined && nombre !== null && String(nombre).trim() !== '') {
    formData.append('nombre', nombre);
  }
  if (descripcion !== undefined && descripcion !== null) {
    formData.append('descripcion', descripcion);
  }
  return http.uploadForm(BASE, formData);
}

/**
 * Actualiza un recurso existente. Permite editar nombre y descripción, y de forma
 * opcional reemplazar el archivo físico pasando `archivo` (File). Si se omite,
 * el binario no se toca.
 *
 * @param {string} id
 * @param {{ nombreOriginal: string, descripcion: string, archivo?: File|null }} payload
 * @returns {Promise<object>}
 */
export function updateRecurso(id, { nombreOriginal, descripcion, archivo }) {
  const formData = new FormData();
  formData.append('nombreOriginal', nombreOriginal ?? '');
  formData.append('descripcion', descripcion ?? '');
  if (archivo) {
    formData.append('archivo', archivo);
  }
  return http.uploadForm(`${BASE}/${id}`, formData, { method: 'PUT' });
}

/**
 * @deprecated Usar `updateRecurso` (soporta archivo opcional). Se mantiene como alias.
 */
export function updateRecursoMetadata(id, payload) {
  return updateRecurso(id, payload);
}

/**
 * Eliminación lógica de un recurso.
 *
 * @param {string} id
 * @returns {Promise<void>}
 */
export function deleteRecurso(id) {
  return http.del(`${BASE}/${id}`);
}

/**
 * Genera un enlace público de descarga para un recurso.
 *
 * El campo `url` es relativo (ej. `/api/publico/recursos/{id}/descargar`).
 * El caller es responsable de concatenarlo con `window.location.origin`
 * al momento de copiarlo al portapapeles.
 *
 * @param {string} id
 * @returns {Promise<{ url: string, recursoId: string, nombreOriginal: string, tamanoBytes: number, contentType: string }>}
 */
export function getDownloadLink(id) {
  return http.post(`${BASE}/${id}/link`);
}

export default {
  BASE,
  BLOCKED_EXTENSIONS,
  MAX_SIZE_BYTES,
  getExtension,
  isBlockedExtension,
  formatBytes,
  listRecursos,
  getRecurso,
  uploadRecurso,
  updateRecurso,
  updateRecursoMetadata,
  deleteRecurso,
  getDownloadLink,
};
