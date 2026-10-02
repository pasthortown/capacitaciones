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

export default { listInscritos, getInscrito, updateInscrito };
