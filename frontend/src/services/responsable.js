/**
 * Servicio para la Página pública del Responsable (acceso por link firmado, sin login).
 *
 * Diseño clave — aislamiento del token admin (mismo patrón que `capacitador.js`):
 *  - El responsable recibe un JWT específico por querystring (`?token=...`).
 *  - Estas llamadas NO usan `http.js`: `http.js` lee el token del admin en
 *    `localStorage` y dispara el evento global `auth:expired` en 401, lo que
 *    provocaría que un admin logueado cerrara sesión por un 401 del responsable.
 *    Para evitar eso, usamos `fetch` directo, pasando el Bearer recibido por
 *    parámetro, sin tocar `localStorage` ni emitir eventos.
 *
 * Contrato:
 *   GET  /api/responsable/perfil                 -> 200 { id, nombres, cargo, empresa, firma? }
 *   PUT  /api/responsable/perfil                 -> 200 (firma OBLIGATORIA)
 *     body: { nombres, cargo, empresa, firma }
 *
 *  Errores: 401/403 si el token no corresponde o expiró; 404 si el responsable
 *  no existe.
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
 * Ejecuta una request autenticada con el token del responsable.
 * No usa `http.js` para no disparar `auth:expired` del admin ni usar su token.
 */
async function requestWithToken(path, token, { method = 'GET', body } = {}) {
  if (!token) {
    throw new HttpError('Token del responsable requerido.', { status: 401 });
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
 * GET /responsable/perfil — datos del responsable asociado al token.
 * @param {string} token
 * @returns {Promise<{id:string, nombres:string, cargo:string, empresa:string, firma?:string|null}>}
 */
export function getPerfil(token) {
  return requestWithToken('/responsable/perfil', token, { method: 'GET' });
}

/**
 * PUT /responsable/perfil — actualiza nombres, cargo, empresa y firma.
 * La firma es OBLIGATORIA en el backend.
 *
 * @param {string} token
 * @param {{ nombres:string, cargo:string, empresa:string, firma:string }} payload
 */
export function updatePerfil(token, payload) {
  return requestWithToken('/responsable/perfil', token, {
    method: 'PUT',
    body: payload ?? {},
  });
}

export default {
  getPerfil,
  updatePerfil,
};
