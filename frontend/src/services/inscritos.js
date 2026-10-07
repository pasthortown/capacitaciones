/**
 * Servicio de la pantalla admin "Inscritos" (policy Admin, usa `http.js`).
 *
 * Contrato:
 *   GET /api/inscritos?capacitacionId=&buscar=  -> 200 InscritoDto[] (sin firma; `tieneFirma`)
 *   GET /api/inscritos/{id}                     -> 200 InscritoDetalleDto (incluye `firma` y `emailUsuario`)
 *   PUT /api/inscritos/{id}                     -> 200 InscritoDetalleDto
 *       body: { nombres, apellidos, identificacion, areaId, emailUsuario, firma|null }
 *       - 400 CAMPO_REQUERIDO | EMAIL_INVALIDO | AREA_INVALIDA
 *       - 404 INSCRITO_NO_ENCONTRADO
 *       - 409 INSCRIPCION_DUPLICADA (cédula ya inscrita en esa capacitación)
 *   POST /api/inscritos                         -> 201 InscritoDto (también en capacitaciones finalizadas)
 *       body: { capacitacionId, nombres, apellidos, identificacion, areaId, emailUsuario,
 *               firma|null, usarFirmaRegistrada, asistio }
 *       - 400 CAMPO_REQUERIDO | EMAIL_INVALIDO | AREA_INVALIDA | FIRMA_REGISTRADA_NO_DISPONIBLE
 *       - 404 NOT_FOUND (capacitación inexistente o inactiva)
 *       - 409 INSCRIPCION_DUPLICADA | INSCRIPCION_CONCURRENTE
 *   GET /api/inscritos/persona/{identificacion} -> 200 { nombres, apellidos, areaId, emailUsuario, tieneFirma } | 404
 */

import http from './http.js';

const BASE = '/inscritos';

/**
 * @param {{ capacitacionId?: string, buscar?: string }} [filtros]
 */
export function listInscritos({ capacitacionId, buscar } = {}) {
  const params = new URLSearchParams();
  if (capacitacionId) params.set('capacitacionId', capacitacionId);
  if (buscar && buscar.trim()) params.set('buscar', buscar.trim());
  const qs = params.toString();
  return http.get(qs ? `${BASE}?${qs}` : BASE);
}

export function getInscrito(id) {
  return http.get(`${BASE}/${id}`);
}

/**
 * @param {string} id
 * @param {{ nombres: string, apellidos: string, identificacion: string, areaId: string,
 *           emailUsuario: string, firma: string|null }} payload  firma null = conservar la actual
 */
export function updateInscrito(id, payload) {
  return http.put(`${BASE}/${id}`, payload);
}

/**
 * @param {{ capacitacionId: string, nombres: string, apellidos: string, identificacion: string,
 *           areaId: string, emailUsuario: string, firma: string|null, usarFirmaRegistrada: boolean,
 *           asistio: boolean }} payload
 */
export function createInscrito(payload) {
  return http.post(BASE, payload);
}

/** Datos del registro de personas para autocompletar (sin firma). Rechaza con 404 si no existe. */
export function buscarPersonaInscrito(identificacion) {
  return http.get(`${BASE}/persona/${encodeURIComponent(identificacion)}`);
}

export default { listInscritos, getInscrito, updateInscrito, createInscrito, buscarPersonaInscrito };
