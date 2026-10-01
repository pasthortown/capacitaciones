/**
 * Traduce el error técnico del servidor SMTP (tal como lo devuelve mail_sender, ej.
 * "554 5.2.252 SendAsDenied; ...") a un resumen en español con una sugerencia.
 * El texto original se conserva para mostrarlo en "Ver detalle".
 */

const MAX_RESUMEN = 160;

const PATRONES = [
  {
    test: /SendAsDenied|not allowed to send as/i,
    resumen: 'La cuenta de usuario no tiene permiso para enviar como el correo del remitente.',
    sugerencia: 'Usa como remitente el mismo correo del usuario, o pide a un administrador de Microsoft 365 el permiso "Enviar como" sobre ese buzón.',
  },
  {
    test: /\b535\b|authentication unsuccessful|authentication failed|Username and Password not accepted/i,
    resumen: 'El servidor rechazó el usuario o la contraseña.',
    sugerencia: 'Revisa el usuario y vuelve a escribir la contraseña. En Office 365 la cuenta debe tener habilitado "SMTP autenticado".',
  },
  {
    test: /no respondió a tiempo|timed out|timeout/i,
    resumen: 'El servidor SMTP no respondió a tiempo.',
    sugerencia: 'Revisa el servidor y el puerto (Office 365: smtp.office365.com, puerto 587).',
  },
  {
    test: /Name or service not known|getaddrinfo|nodename nor servname|Temporary failure in name resolution/i,
    resumen: 'No se encontró el servidor SMTP.',
    sugerencia: 'Revisa que el nombre del servidor esté bien escrito.',
  },
  {
    test: /Connection refused|Connection unexpectedly closed/i,
    resumen: 'El servidor rechazó la conexión en ese puerto.',
    sugerencia: 'Revisa el puerto y la opción "Usar TLS".',
  },
];

/**
 * @param {string} mensaje - texto de error devuelto por el backend.
 * @returns {{ resumen: string, sugerencia: string|null, detalle: string|null }}
 *          `detalle` es null cuando el resumen ya muestra el mensaje completo.
 */
export function resumirErrorSmtp(mensaje) {
  const texto = (mensaje || '').trim();
  const conocido = PATRONES.find((p) => p.test.test(texto));
  if (conocido) {
    return { resumen: conocido.resumen, sugerencia: conocido.sugerencia, detalle: texto || null };
  }

  const primeraLinea = texto.split(/\r?\n/)[0];
  if (primeraLinea === texto && texto.length <= MAX_RESUMEN) {
    return { resumen: texto || 'Error desconocido del servidor SMTP.', sugerencia: null, detalle: null };
  }
  const corto = primeraLinea.length > MAX_RESUMEN ? `${primeraLinea.slice(0, MAX_RESUMEN)}…` : primeraLinea;
  return { resumen: corto, sugerencia: null, detalle: texto };
}

export default resumirErrorSmtp;
