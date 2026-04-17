/**
 * Servicio para la Página pública de Inscripción (acceso por link firmado, sin login).
 *
 * Diseño clave — aislamiento del token admin:
 *  - El inscrito recibe un JWT específico por querystring (`?token=...`).
 *  - Estas llamadas NO usan `http.js`: `http.js` lee el token del admin en
 *    `localStorage` y dispara el evento global `auth:expired` en 401, lo que
 *    provocaría que un admin logueado cerrara sesión por un 401 de un link
 *    público. Para evitar eso, usamos `fetch` directo, pasando el Bearer
 *    recibido por parámetro, sin tocar `localStorage` ni emitir eventos.
 *
 * Contrato:
 *   GET  /api/inscripcion/capacitacion     -> 200 { capacitacion, areas }
 *       - 401/403 token inválido/expirado.
 *       - 409 capacitación Finalizada → inscripciones cerradas.
 *   POST /api/inscripcion/capacitacion     -> 201 AsistenteSummaryDto
 *       body: { nombres, apellidos, identificacion, areaId, emailUsuario, firma }
 *       - 400 validación.
 *       - 409 duplicado por identificación (o finalizada).
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
 * Ejecuta una request autenticada con el token de inscripción.
 * No usa `http.js` para no disparar `auth:expired` del admin ni usar su token.
 */
async function requestWithToken(path, token, { method = 'GET', body } = {}) {
  if (!token) {
    throw new HttpError('Token de inscripción requerido.', { status: 401 });
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
 * GET /inscripcion/capacitacion — datos de la capacitación + áreas disponibles.
 * @param {string} token
 * @returns {Promise<{ capacitacion: object, areas: Array<{id:string,nombre:string}> }>}
 */
export function getCapacitacion(token) {
  return requestWithToken('/inscripcion/capacitacion', token, { method: 'GET' });
}

/**
 * POST /inscripcion/capacitacion — registra al asistente.
 *
 * @param {string} token
 * @param {{
 *   nombres: string,
 *   apellidos: string,
 *   identificacion: string,
 *   areaId: string,
 *   emailUsuario: string, // solo la parte local (sin @dos.com.ec)
 *   firma: string,        // dataURL PNG
 * }} payload
 * @returns {Promise<object>} AsistenteSummaryDto
 */
export function inscribir(token, payload) {
  return requestWithToken('/inscripcion/capacitacion', token, {
    method: 'POST',
    body: payload ?? {},
  });
}

export default {
  getCapacitacion,
  inscribir,
};
