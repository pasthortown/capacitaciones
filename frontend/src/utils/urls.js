/**
 * Convierte una URL relativa emitida por el backend (ej. "/capacitador?token=...")
 * en absoluta respetando el base path con el que se montó el SPA.
 *
 * El backend no conoce el prefijo de despliegue (ej. `/capacitados/`) y emite
 * rutas desde la raíz del dominio; aquí se prefija con `import.meta.env.BASE_URL`
 * (que Vite inyecta según `vite.config.js`) para que los enlaces copiados al
 * portapapeles funcionen en cualquier sub-path.
 */
export function buildPublicUrl(relativePath) {
  const base = import.meta.env.BASE_URL.replace(/\/$/, '');
  const path = relativePath ?? '';
  return `${window.location.origin}${base}${path}`;
}
