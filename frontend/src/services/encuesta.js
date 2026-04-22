/**
 * Servicio público de la encuesta de satisfacción (sin auth). El asistente
 * abre el link que incluye el id de la capacitación y se autoidentifica con
 * su cédula al enviar respuestas.
 */
import http from './http.js';

const BASE = '/publico/encuesta';

export function obtener(capacitacionId) {
  return http.get(`${BASE}/${capacitacionId}`);
}

export function responder(capacitacionId, { identificacion, respuestas }) {
  return http.post(`${BASE}/${capacitacionId}/responder`, {
    identificacion,
    respuestas,
  });
}

export default { obtener, responder };
