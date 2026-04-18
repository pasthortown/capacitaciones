/**
 * Cliente HTTP centralizado. Todos los consumos del backend deben pasar por
 * este módulo (ver §3.2 de instrucciones.md).
 *
 * - Base URL: `VITE_API_BASE` si está definida, sino `/api`
 *   (ambos casos funcionan: dev server usa proxy, nginx en prod hace lo mismo).
 * - JSON por defecto.
 * - JWT: lee el token desde localStorage bajo `AUTH_STORAGE_KEY` y lo
 *   adjunta como `Authorization: Bearer <token>`.
 * - Interceptor global de 401: cuando una request **autenticada** (es decir,
 *   que llevaba token) responde 401, se dispara el evento `auth:expired` en
 *   `window`. `AuthProvider` lo escucha para limpiar la sesión. Se usa un
 *   CustomEvent en lugar de acoplar este módulo a React Router, para
 *   mantenerlo framework-agnóstico.
 *
 * Retrocompat:
 *  - `http.get/post/put/del` mantienen la firma original (paths + body JSON).
 *  - Se agregan helpers `downloadBlob` y `uploadForm` para flujos binarios
 *    (descarga de plantilla XLSX / import multipart).
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

/**
 * Notifica al resto de la app que el token dejó de ser válido.
 * Se dispara solo para requests que iban autenticadas (llevaban Bearer).
 * Un request sin token que recibe 401 no es una sesión expirada, es
 * simplemente una ruta protegida — no tiene sentido "desloguear".
 */
function notifyAuthExpired() {
  if (typeof window !== 'undefined' && typeof window.dispatchEvent === 'function') {
    window.dispatchEvent(new CustomEvent('auth:expired'));
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
    if (response.status === 401 && token) {
      notifyAuthExpired();
    }
    const message =
      (parsed && typeof parsed === 'object' && (parsed.message || parsed.title)) ||
      `HTTP ${response.status} en ${method} ${path}`;
    throw new HttpError(message, { status: response.status, body: parsed });
  }

  return parsed;
}

/**
 * Extrae el nombre de archivo sugerido del header `Content-Disposition`.
 * Soporta tanto `filename="..."` como `filename*=UTF-8''...`.
 */
function parseFilenameFromDisposition(disposition) {
  if (!disposition) return null;
  const utf8Match = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(disposition);
  if (utf8Match && utf8Match[1]) {
    try {
      return decodeURIComponent(utf8Match[1].trim().replace(/^"|"$/g, ''));
    } catch {
      return utf8Match[1].trim().replace(/^"|"$/g, '');
    }
  }
  const simpleMatch = /filename="?([^";]+)"?/i.exec(disposition);
  if (simpleMatch && simpleMatch[1]) {
    return simpleMatch[1].trim();
  }
  return null;
}

/**
 * Descarga un blob desde el backend y dispara el diálogo de guardado
 * en el navegador. Retorna `{ blob, filename }` por si el caller quiere
 * manipular el archivo.
 *
 * @param {string} path          - Path relativo al API_BASE.
 * @param {object} [options]
 * @param {string} [options.fallbackFilename] - Nombre por defecto si el
 *   servidor no envía `Content-Disposition`.
 */
async function downloadBlob(path, { fallbackFilename = 'download' } = {}) {
  const token = getAuthToken();
  const response = await fetch(buildUrl(path), {
    method: 'GET',
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });

  if (!response.ok) {
    if (response.status === 401 && token) {
      notifyAuthExpired();
    }
    const parsed = await parseBody(response);
    const message =
      (parsed && typeof parsed === 'object' && (parsed.message || parsed.title)) ||
      `HTTP ${response.status} en GET ${path}`;
    throw new HttpError(message, { status: response.status, body: parsed });
  }

  const blob = await response.blob();
  const disposition = response.headers.get('content-disposition');
  const filename = parseFilenameFromDisposition(disposition) || fallbackFilename;

  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  // Liberar el objeto en el siguiente tick para que Firefox alcance a disparar la descarga.
  setTimeout(() => URL.revokeObjectURL(url), 0);

  return { blob, filename };
}

/**
 * Envía un `FormData` (multipart) y devuelve el JSON parseado.
 * No setea Content-Type manualmente: el navegador lo arma con el boundary.
 * Soporta POST (default) y PUT para edición con reemplazo opcional de archivo.
 *
 * @param {string}   path
 * @param {FormData} formData
 * @param {object}   [options]
 * @param {'POST'|'PUT'} [options.method='POST']
 */
async function uploadForm(path, formData, { method = 'POST' } = {}) {
  const token = getAuthToken();
  const response = await fetch(buildUrl(path), {
    method,
    headers: {
      Accept: 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: formData,
  });

  const parsed = await parseBody(response);

  if (!response.ok) {
    if (response.status === 401 && token) {
      notifyAuthExpired();
    }
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
  downloadBlob,
  uploadForm,
};

export default http;
