/**
 * Servicio para la pantalla pública de Pase de Lista (acceso por link firmado, sin login).
 *
 * Diseño: réplica del patrón de `capacitador.js` — aislamiento del token admin.
 *  - Usa `fetch` directo (NO `http.js`) para no leer el token de localStorage
 *    ni emitir `auth:expired` en 401, lo que cerraría la sesión del admin
 *    si está logueado en otra pestaña.
 *
 * Contrato (ver instrucciones.md §7.9 — Fase 10):
 *   GET  /api/capacitador/pase-lista                                        -> 200 PaseListaDto
 *   PUT  /api/capacitador/pase-lista/asistentes/{asistenteId}               -> 200 { id, estadoAsistencia, fechaMarcacionAsistencia }
 *     body: { estadoAsistencia: 'Presente' | 'Ausente' | null }
 *
 * Errores: 401/403 → token inválido o expirado.
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
 * Ejecuta una request autenticada con el token `PaseLista`.
 * Réplica del helper de `capacitador.js`.
 */
async function requestWithToken(path, token, { method = 'GET', body } = {}) {
  if (!token) {
    throw new HttpError('Token de pase de lista requerido.', { status: 401 });
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
 * GET /capacitador/pase-lista — capacitación + asistentes (orden alfabético
 * apellidos/nombres) con su estado de asistencia actual.
 *
 * @param {string} token
 * @returns {Promise<{
 *   capacitacion: { id: string, codigo: string, tema: string,
 *                   fechaHoraInicio: string, duracionMinutos: number, estado: string },
 *   asistentes: Array<{ id: string, nombres: string, apellidos: string,
 *                       identificacion: string,
 *                       estadoAsistencia: 'Presente' | 'Ausente' | null,
 *                       fechaMarcacionAsistencia: string | null }>
 * }>}
 */
export function getPaseLista(token) {
  return requestWithToken('/capacitador/pase-lista', token, { method: 'GET' });
}

/**
 * PUT /capacitador/pase-lista/asistentes/{asistenteId} — marca asistencia.
 *
 * @param {string} token
 * @param {string} asistenteId
 * @param {'Presente' | 'Ausente' | null} estadoAsistencia
 * @returns {Promise<{ id: string, estadoAsistencia: 'Presente' | 'Ausente' | null, fechaMarcacionAsistencia: string | null }>}
 */
export function marcarAsistencia(token, asistenteId, estadoAsistencia) {
  return requestWithToken(
    `/capacitador/pase-lista/asistentes/${asistenteId}`,
    token,
    { method: 'PUT', body: { estadoAsistencia } },
  );
}

export default {
  getPaseLista,
  marcarAsistencia,
};
