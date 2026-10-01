/**
 * Servicio Configuración — contador de numeración `CAP-PC-REG-###` y
 * configuración de correo (servidor SMTP, remitente y avisos por tipo).
 *
 * Contrato API (ver instrucciones.md §7.4 y §7.3.1):
 *   GET /api/configuracion/numeracion   -> { siguienteNumero, ultimaActualizacion, formato }
 *   PUT /api/configuracion/numeracion   body: { siguienteNumero: int (1..999) }
 *                                       -> { siguienteNumero, ultimaActualizacion, formato }
 *   GET /api/configuracion/correo       -> { configurado, smtpHost, smtpPort, smtpUser,
 *                                             tienePassword, passwordInvalida, usarTls,
 *                                             remitenteCorreo, remitenteNombre, ccGlobal,
 *                                             bccGlobal, actualizadoPor, actualizadoEn }
 *   PUT /api/configuracion/correo       body: { smtpHost, smtpPort, smtpUser, password,
 *                                             quitarPassword, usarTls, remitenteCorreo,
 *                                             remitenteNombre, ccGlobal, bccGlobal }
 *   POST /api/configuracion/correo/prueba          -> { ok, mensaje }
 *   GET  /api/configuracion/correo/notificaciones  -> lista de avisos
 *   PUT  /api/configuracion/correo/notificaciones  body: lista de avisos
 *
 * Todos los endpoints requieren Bearer (policy Admin).
 */

import http from './http.js';

const BASE = '/configuracion/numeracion';
const CORREO = '/configuracion/correo';

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

/** Configuración SMTP (nunca trae la contraseña; `tienePassword` indica si hay una guardada). */
export function getCorreo() {
  return http.get(CORREO);
}

/**
 * Guarda la configuración SMTP.
 * @param {object} payload - { smtpHost, smtpPort, smtpUser, password, quitarPassword, usarTls,
 *                             remitenteCorreo, remitenteNombre, ccGlobal, bccGlobal }.
 *                           `password` vacío = conservar la guardada.
 */
export function updateCorreo(payload) {
  return http.put(CORREO, payload);
}

/** Envía un correo de prueba con los datos del formulario al admin logueado. -> { ok, mensaje } */
export function probarCorreo(payload) {
  return http.post(`${CORREO}/prueba`, payload);
}

/** Lista de avisos: [{ plantilla, nombre, activo, asuntoPersonalizado, asuntoActual, variables }]. */
export function getNotificaciones() {
  return http.get(`${CORREO}/notificaciones`);
}

/** @param {Array<{plantilla: string, activo: boolean, asuntoPersonalizado: string|null}>} lista */
export function updateNotificaciones(lista) {
  return http.put(`${CORREO}/notificaciones`, lista);
}

export default {
  getNumeracion,
  updateNumeracion,
  getCorreo,
  updateCorreo,
  probarCorreo,
  getNotificaciones,
  updateNotificaciones,
};
