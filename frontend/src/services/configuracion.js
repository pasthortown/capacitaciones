/**
 * Servicio Configuración — contador de numeración `CAP-PC-REG-###`.
 *
 * Contrato API (ver instrucciones.md §7.4 y §7.3.1):
 *   GET /api/configuracion/numeracion   -> { siguienteNumero, ultimaActualizacion, formato }
 *   PUT /api/configuracion/numeracion   body: { siguienteNumero: int (1..999) }
 *                                       -> { siguienteNumero, ultimaActualizacion, formato }
 *
 * Ambos endpoints requieren Bearer (policy Admin).
 */

import http from './http.js';

const BASE = '/configuracion/numeracion';

/** Obtiene la configuración actual de numeración. */
export function getNumeracion() {
  return http.get(BASE);
}

/**
 * Actualiza el siguiente número a asignar.
 * @param {number} siguienteNumero - entero entre 1 y 999.
 */
export function updateNumeracion(siguienteNumero) {
  return http.put(BASE, { siguienteNumero });
}

export default {
  getNumeracion,
  updateNumeracion,
};
