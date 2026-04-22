/**
 * Servicio CRUD del catálogo de preguntas de encuesta de satisfacción.
 * Admin: policy "Admin". El texto de la pregunta se asocia a un TipoActividad.
 */
import http from './http.js';

const BASE = '/preguntas-encuesta';

export function list({ tipoActividadId, includeInactive = false } = {}) {
  const params = new URLSearchParams();
  if (tipoActividadId) params.set('tipoActividadId', tipoActividadId);
  if (includeInactive) params.set('includeInactive', 'true');
  const qs = params.toString();
  return http.get(qs ? `${BASE}?${qs}` : BASE);
}

export function get(id) {
  return http.get(`${BASE}/${id}`);
}

export function create({ tipoActividadId, texto, activo = true }) {
  return http.post(BASE, { tipoActividadId, texto, activo });
}

export function update(id, { tipoActividadId, texto, activo }) {
  return http.put(`${BASE}/${id}`, { tipoActividadId, texto, activo });
}

export function remove(id) {
  return http.del(`${BASE}/${id}`);
}

export default { list, get, create, update, remove };
