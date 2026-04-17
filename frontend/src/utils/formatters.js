/**
 * Helpers de formato para UI. Puras, sin dependencias externas.
 *
 *  - formatFechaHora(iso)  -> "DD/MM/YYYY HH:mm" (zona local del navegador)
 *  - formatDuracion(mins)  -> "Xh Ym" / "Xh" / "Ymin" según corresponda
 *  - formatCodigo(n)       -> "CAP-PC-REG-001" con 3 dígitos
 */

/**
 * Formatea una fecha ISO a `DD/MM/YYYY HH:mm`. Usa la zona local.
 * Si el input es inválido/nulo retorna cadena vacía.
 *
 * @param {string|Date|null|undefined} iso
 * @returns {string}
 */
export function formatFechaHora(iso) {
  if (!iso) return '';
  const date = iso instanceof Date ? iso : new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (n) => String(n).padStart(2, '0');
  const dd = pad(date.getDate());
  const mm = pad(date.getMonth() + 1);
  const yyyy = date.getFullYear();
  const hh = pad(date.getHours());
  const mi = pad(date.getMinutes());
  return `${dd}/${mm}/${yyyy} ${hh}:${mi}`;
}

/**
 * Formatea duración en minutos a texto corto.
 *  - 0   -> "0min"
 *  - 30  -> "30min"
 *  - 60  -> "1h"
 *  - 90  -> "1h 30min"
 *
 * @param {number} mins
 * @returns {string}
 */
export function formatDuracion(mins) {
  const total = Number.isFinite(mins) ? Math.max(0, Math.floor(mins)) : 0;
  const horas = Math.floor(total / 60);
  const minutos = total % 60;
  if (horas === 0 && minutos === 0) return '0min';
  if (horas > 0 && minutos === 0) return `${horas}h`;
  if (horas === 0 && minutos > 0) return `${minutos}min`;
  return `${horas}h ${minutos}min`;
}

/**
 * Formatea un número entero al código `CAP-PC-REG-###` (3 dígitos).
 * Si `n` no es un número válido, retorna cadena vacía.
 *
 * @param {number} n
 * @returns {string}
 */
export function formatCodigo(n) {
  if (!Number.isFinite(n)) return '';
  const padded = String(Math.max(0, Math.floor(n))).padStart(3, '0');
  return `CAP-PC-REG-${padded}`;
}

export default {
  formatFechaHora,
  formatDuracion,
  formatCodigo,
};
