/**
 * Helpers de búsqueda client-side para tablas/listas.
 *
 * `matchesSearch(item, query)` devuelve true si alguna de las propiedades
 * primitivas del objeto (strings, numbers, booleans, dates), a cualquier
 * nivel de anidamiento, contiene el query (case-insensitive, normalizado
 * sin acentos). Arrays también se recorren.
 *
 * No se pretende implementar full-text search ni búsqueda tokenizada —
 * un simple "contains" sobre la serialización textual es suficiente para
 * las vistas actuales (Repositorio, Capacitaciones).
 */

/**
 * Normaliza un string: lowercase + quita diacríticos. Devuelve '' si el input
 * no es un string.
 */
export function normalizeText(value) {
  if (value == null) return '';
  const s = String(value);
  // NFD separa base + diacríticos; el regex quita el rango combinante.
  return s.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
}

/**
 * Recorre recursivamente un valor y concatena su representación textual.
 * Evita ciclos vía un Set de visitados.
 */
function collectText(value, parts, visited) {
  if (value == null) return;
  const type = typeof value;
  if (type === 'string' || type === 'number' || type === 'boolean' || type === 'bigint') {
    parts.push(String(value));
    return;
  }
  if (value instanceof Date) {
    parts.push(value.toISOString());
    return;
  }
  if (Array.isArray(value)) {
    for (const v of value) collectText(v, parts, visited);
    return;
  }
  if (type === 'object') {
    if (visited.has(value)) return;
    visited.add(value);
    for (const k of Object.keys(value)) {
      collectText(value[k], parts, visited);
    }
  }
}

/**
 * Devuelve true si cualquier propiedad "textualizable" del objeto contiene el query.
 * Case-insensitive y sin acentos.
 *
 * @param {unknown} item
 * @param {string} query
 * @returns {boolean}
 */
export function matchesSearch(item, query) {
  const q = normalizeText(query).trim();
  if (!q) return true;
  const parts = [];
  collectText(item, parts, new Set());
  return normalizeText(parts.join(' ')).includes(q);
}

export default { matchesSearch, normalizeText };
