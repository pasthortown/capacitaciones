/**
 * Cliente HTTP centralizado. Todos los consumos del backend deben pasar por
 * este módulo (ver §3.2 de instrucciones.md).
 *
 * - Base URL: `VITE_API_BASE` si está definida, sino `/api`
 *   (ambos casos funcionan: dev server usa proxy, nginx en prod hace lo mismo).
 * - JSON por defecto.
 * - Soporte futuro para JWT: lee el token desde localStorage bajo
 *   `AUTH_STORAGE_KEY` y lo adjunta como `Authorization: Bearer <token>`.
 *   El login se implementará en una fase posterior; por ahora simplemente
 *   si existe el token lo envía.
 */

export const API_BASE = import.meta.env.VITE_API_BASE || '/api';
export const AUTH_STORAGE_KEY = 'capacitaciones.authToken';

/**
 * Error HTTP estructurado para facilitar `try/catch` en los callers.
 */
export class HttpError extends Error {
  constructor(message, { status, body } = {}) {
    super(message);
    this.name = 'HttpError';
    this.status = status;
    this.body = body;
  }
}

function getAuthToken() {
  try {
    return localStorage.getItem(AUTH_STORAGE_KEY);
  } catch {
    // SSR / storage bloqueado
    return null;
  }
}

function buildUrl(path) {
  if (!path) return API_BASE;
  if (path.startsWith('http://') || path.startsWith('https://')) return path;
  const suffix = path.startsWith('/') ? path : `/${path}`;
  return `${API_BASE}${suffix}`;
}

async function parseBody(response) {
  const contentType = response.headers.get('content-type') || '';
  if (contentType.includes('application/json')) {
    try {
      return await response.json();
    } catch {
      return null;
    }
  }
  if (contentType.startsWith('text/')) {
    return response.text();
  }
  return null;
}

async function request(path, { method = 'GET', body, headers, signal } = {}) {
  const token = getAuthToken();
  const finalHeaders = {
    Accept: 'application/json',
    ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...headers,
  };

  const response = await fetch(buildUrl(path), {
    method,
    headers: finalHeaders,
    body: body !== undefined ? JSON.stringify(body) : undefined,
    signal,
  });

  const parsed = await parseBody(response);

  if (!response.ok) {
    const message =
      (parsed && typeof parsed === 'object' && (parsed.message || parsed.title)) ||
      `HTTP ${response.status} en ${method} ${path}`;
    throw new HttpError(message, { status: response.status, body: parsed });
  }

  return parsed;
}

export const http = {
  get: (path, options) => request(path, { ...options, method: 'GET' }),
  post: (path, body, options) => request(path, { ...options, method: 'POST', body }),
  put: (path, body, options) => request(path, { ...options, method: 'PUT', body }),
  del: (path, options) => request(path, { ...options, method: 'DELETE' }),
};

export default http;
