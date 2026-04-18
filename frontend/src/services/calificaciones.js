/**
 * Servicio para la pantalla pública de Calificaciones (acceso por link firmado, sin login).
 *
 * Diseño: réplica del patrón de `paseLista.js` — aislamiento del token admin.
 *  - Usa `fetch` directo (NO `http.js`) para no leer el token de localStorage
 *    ni emitir `auth:expired` en 401, lo que cerraría la sesión del admin
 *    si está logueado en otra pestaña.
 *
 * Contrato (ver instrucciones.md §7.10 — Fase 11):
 *   GET  /api/capacitador/calificaciones                                 -> 200 CalificacionesDto
 *     (backend ya filtra sólo Presente y ordena alfabéticamente.)
 *   PUT  /api/capacitador/calificaciones/asistentes/{asistenteId}        -> 200 { id, calificacion }
 *     body: { calificacion: number | null }
 *
 * Errores: 401/403 → token inválido o expirado.
 *          409 CALIFICACIONES_NO_APLICA → la capacitación no es Aprobación.
 *          409 ASISTENTE_NO_PRESENTE, 400 CALIFICACION_FUERA_DE_RANGO → errores
 *          puntuales al guardar; el caller decide cómo mostrarlos.
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
 * Ejecuta una request autenticada con el token `Calificaciones`.
 * Réplica del helper de `paseLista.js`.
 */
async function requestWithToken(path, token, { method = 'GET', body } = {}) {
  if (!token) {
    throw new HttpError('Token de calificaciones requerido.', { status: 401 });
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
 * GET /capacitador/calificaciones — capacitación + asistentes Presentes
 * (orden alfabético apellidos/nombres garantizado por backend).
 *
 * @param {string} token
 * @returns {Promise<{
 *   capacitacion: { id: string, codigo: string, tema: string,
 *                   fechaHoraInicio: string, duracionMinutos: number,
 *                   estado: string, tipoCertificacion: string,
 *                   puntajeMinimo: number | null },
 *   asistentes: Array<{ id: string, nombres: string, apellidos: string,
 *                       identificacion: string,
 *                       estadoAsistencia: 'Presente' | 'Ausente' | null,
 *                       calificacion: number | null }>
 * }>}
 */
export function getCalificaciones(token) {
  return requestWithToken('/capacitador/calificaciones', token, { method: 'GET' });
}

/**
 * PUT /capacitador/calificaciones/asistentes/{asistenteId} — registra la
 * calificación del asistente. `null` limpia la calificación.
 *
 * @param {string} token
 * @param {string} asistenteId
 * @param {number | null} calificacion
 * @returns {Promise<{ id: string, calificacion: number | null }>}
 */
export function calificarAsistente(token, asistenteId, calificacion) {
  return requestWithToken(
    `/capacitador/calificaciones/asistentes/${asistenteId}`,
    token,
    { method: 'PUT', body: { calificacion } },
  );
}

export default {
  getCalificaciones,
  calificarAsistente,
};
