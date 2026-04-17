/**
 * Servicio para la Página del Capacitador (acceso por link firmado, sin login).
 *
 * Diseño clave — aislamiento del token admin:
 *  - El capacitador recibe un JWT específico por querystring (`?token=...`).
 *  - Estas llamadas NO usan `http.js`: `http.js` lee el token del admin en
 *    `localStorage` y dispara el evento global `auth:expired` en 401, lo que
 *    provocaría que un admin logueado cerrara sesión por un 401 del
 *    capacitador. Para evitar eso, usamos `fetch` directo, pasando el Bearer
 *    recibido por parámetro, sin tocar `localStorage` ni emitir eventos.
 *
 * Contrato:
 *   GET  /api/capacitador/capacitacion          -> 200 CapacitadorCapacitacionDto
 *   PUT  /api/capacitador/capacitacion          -> 200 CapacitadorCapacitacionDto
 *     body: { descripcion?, firmaCapacitador?, cargoCapacitador?, empresaCapacitador? }
 *     Semántica "replace": cualquier campo presente sobrescribe (incluso con null),
 *     cualquier campo omitido se deja tal cual. Enviar siempre los 4 campos para
 *     evitar sobrescrituras accidentales.
 *
 *  Errores: 401/403 el token no corresponde o expiró; 403 también si la
 *  capacitación ya está Finalizada.
 */

import { API_BASE, HttpError } from './http.js';

function buildUrl(path) {
  if (!path) return API_BASE;
  if (path.startsWith('http://') || path.startsWith('https://')) return path;
  const suffix = path.startsWith('/') ? path : `/${path}`;
  return `${API_BASE}${suffix}`;
}

async function parseBody(response) {
  const contentType = response.headers.get('content-type') || '';
  if (contentType.includes('application/json')) {
    try {
      return await response.json();
    } catch {
      return null;
    }
  }
  if (contentType.startsWith('text/')) {
    return response.text();
  }
  return null;
}

/**
 * Ejecuta una request autenticada con el token del capacitador.
 * No usa `http.js` para no disparar `auth:expired` del admin ni usar su token.
 */
async function requestWithToken(path, token, { method = 'GET', body } = {}) {
  if (!token) {
    throw new HttpError('Token del capacitador requerido.', { status: 401 });
  }

  const headers = {
    Accept: 'application/json',
    Authorization: `Bearer ${token}`,
    ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
  };

  const response = await fetch(buildUrl(path), {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  const parsed = await parseBody(response);

  if (!response.ok) {
    const message =
      (parsed && typeof parsed === 'object' && (parsed.message || parsed.title)) ||
      `HTTP ${response.status} en ${method} ${path}`;
    throw new HttpError(message, { status: response.status, body: parsed });
  }

  return parsed;
}

/**
 * GET /capacitador/capacitacion — datos de la capacitación asociada al token.
 * @param {string} token
 */
export function getCapacitacion(token) {
  return requestWithToken('/capacitador/capacitacion', token, { method: 'GET' });
}

/**
 * PUT /capacitador/capacitacion — actualiza descripción + firma + cargo + empresa.
 * Enviar siempre los 4 campos para evitar borrado accidental por omisión.
 *
 * @param {string} token
 * @param {{ descripcion?: string|null, firmaCapacitador?: string|null,
 *           cargoCapacitador?: string|null, empresaCapacitador?: string|null }} payload
 */
export function updateCapacitacion(token, payload) {
  return requestWithToken('/capacitador/capacitacion', token, {
    method: 'PUT',
    body: payload ?? {},
  });
}

export default {
  getCapacitacion,
  updateCapacitacion,
};
