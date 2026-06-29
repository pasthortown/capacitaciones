/**
 * Servicio de Colaboradores (módulo Entrenamiento, admin).
 *
 * Dos orígenes:
 *  - DOS: colaboradores internos traídos del API de ControlTareas (solo lectura).
 *  - Externos: personas ajenas a DOS, administradas localmente (CRUD completo).
 *
 * Contrato API (todos requieren Bearer, policy Admin):
 *   GET    /api/colaboradores/dos?buscar=&incluirInactivos=
 *     -> { integracionDisponible: bool, items: EmpleadoDosDto[] }
 *   GET    /api/colaboradores/externos?buscar=&incluirInactivos=
 *     -> ColaboradorDto[]
 *   GET    /api/colaboradores/externos/{id}        -> ColaboradorDto
 *   POST   /api/colaboradores/externos             -> 201 ColaboradorDto
 *   PUT    /api/colaboradores/externos/{id}        -> 200 ColaboradorDto
 *   DELETE /api/colaboradores/externos/{id}        -> 204 (baja lógica)
 */

import http from './http.js';

const BASE = '/colaboradores';

function qs({ buscar, incluirInactivos } = {}) {
  const p = new URLSearchParams();
  if (buscar && buscar.trim()) p.set('buscar', buscar.trim());
  if (incluirInactivos) p.set('incluirInactivos', 'true');
  const s = p.toString();
  return s ? `?${s}` : '';
}

/** Colaboradores internos de DOS (ControlTareas). Devuelve { integracionDisponible, items }. */
export function listDos(opts) {
  return http.get(`${BASE}/dos${qs(opts)}`);
}

/** Colaboradores externos (locales). Devuelve un array. */
export function listExternos(opts) {
  return http.get(`${BASE}/externos${qs(opts)}`);
}

export function getExterno(id) {
  return http.get(`${BASE}/externos/${id}`);
}

export function createExterno(payload) {
  return http.post(`${BASE}/externos`, payload);
}

export function updateExterno(id, payload) {
  return http.put(`${BASE}/externos/${id}`, payload);
}

export function deleteExterno(id) {
  return http.del(`${BASE}/externos/${id}`);
}

export default { listDos, listExternos, getExterno, createExterno, updateExterno, deleteExterno };
