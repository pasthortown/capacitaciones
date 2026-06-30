/**
 * Servicio de Convenios (módulo Entrenamiento, admin). Todos requieren Bearer (policy Admin).
 *
 *   GET    /api/convenios?buscar=&incluirInactivos=         -> ConvenioDto[]
 *   GET    /api/convenios/{id}                              -> ConvenioDto
 *   POST   /api/convenios                                   -> 201 ConvenioDto
 *   PUT    /api/convenios/{id}                              -> 200 ConvenioDto
 *   DELETE /api/convenios/{id}                              -> 204 (baja lógica)
 *   GET    /api/convenios/colaborador/{cedula}?soloVigentes -> ConvenioDto[] (historial)
 *   POST   /api/convenios/{id}/anexo  (multipart archivo)   -> ConvenioDto
 *   DELETE /api/convenios/{id}/anexo                        -> 204
 *   GET    /api/convenios/{id}/anexo/descargar              -> archivo
 */

import http, { API_BASE, AUTH_STORAGE_KEY, HttpError } from './http.js';

const BASE = '/convenios';

function qs({ buscar, incluirInactivos } = {}) {
  const p = new URLSearchParams();
  if (buscar && buscar.trim()) p.set('buscar', buscar.trim());
  if (incluirInactivos) p.set('incluirInactivos', 'true');
  const s = p.toString();
  return s ? `?${s}` : '';
}

export function list(opts) {
  return http.get(`${BASE}${qs(opts)}`);
}

export function get(id) {
  return http.get(`${BASE}/${id}`);
}

export function create(payload) {
  return http.post(BASE, payload);
}

export function update(id, payload) {
  return http.put(`${BASE}/${id}`, payload);
}

export function remove(id) {
  return http.del(`${BASE}/${id}`);
}

export function historial(cedula, soloVigentes = true) {
  const p = new URLSearchParams();
  p.set('soloVigentes', soloVigentes ? 'true' : 'false');
  return http.get(`${BASE}/colaborador/${encodeURIComponent(cedula)}?${p.toString()}`);
}

/** Resuelve un colaborador (externo o DOS) por cédula para asociarlo. 404 => null. */
export async function buscarColaborador(cedula) {
  try {
    return await http.get(`/colaboradores/buscar/${encodeURIComponent(cedula)}`);
  } catch (err) {
    if (err && err.status === 404) return null;
    throw err;
  }
}

/**
 * Sube un anexo con barra de progreso real (XMLHttpRequest expone `upload.onprogress`,
 * fetch no). `onProgress(pct)` recibe 0..100. Resuelve con el ConvenioDto actualizado.
 */
export function subirAnexo(id, file, onProgress) {
  return new Promise((resolve, reject) => {
    const fd = new FormData();
    fd.append('archivo', file);
    const xhr = new XMLHttpRequest();
    const base = (API_BASE || '/api').replace(/\/$/, '');
    xhr.open('POST', `${base}${BASE}/${id}/anexos`);
    let token = null;
    try { token = localStorage.getItem(AUTH_STORAGE_KEY); } catch { /* storage bloqueado */ }
    if (token) xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.setRequestHeader('Accept', 'application/json');
    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable && typeof onProgress === 'function') {
        onProgress(Math.round((e.loaded / e.total) * 100));
      }
    };
    xhr.onload = () => {
      let parsed = null;
      try { parsed = xhr.responseText ? JSON.parse(xhr.responseText) : null; } catch { /* no-json */ }
      if (xhr.status >= 200 && xhr.status < 300) resolve(parsed);
      else reject(new HttpError(parsed?.message || `HTTP ${xhr.status}`, { status: xhr.status, body: parsed }));
    };
    xhr.onerror = () => reject(new HttpError('Error de red al subir el anexo.', { status: 0 }));
    xhr.send(fd);
  });
}

export function eliminarAnexo(id, anexoId) {
  return http.del(`${BASE}/${id}/anexos/${anexoId}`);
}

export function descargarAnexo(id, anexoId, fallbackFilename = 'anexo') {
  return http.downloadBlob(`${BASE}/${id}/anexos/${anexoId}/descargar`, { fallbackFilename });
}

/** B) Descarga el PDF del convenio (documento GIC-EC-ANX-01). */
export function imprimir(id, fallbackFilename = 'Convenio.pdf') {
  return http.downloadBlob(`${BASE}/${id}/imprimir`, { fallbackFilename });
}

/** D) Descarga el PDF de reporte de convenios por colaborador. */
export function descargarReporteColaborador(cedula, fallbackFilename) {
  return http.downloadBlob(`${BASE}/colaborador/${encodeURIComponent(cedula)}/reporte`,
    { fallbackFilename: fallbackFilename || `Reporte_Convenios_${cedula}.pdf` });
}

/** E) Datos agregados del dashboard de convenios. */
export function dashboard() {
  return http.get(`${BASE}/dashboard`);
}

/** E) Descarga el PDF resumen del dashboard. */
export function descargarDashboardPdf(fallbackFilename = 'Dashboard_Convenios.pdf') {
  return http.downloadBlob(`${BASE}/dashboard/pdf`, { fallbackFilename });
}

/** C) Liquidación por desvinculación a una fecha de salida (yyyy-MM-dd). */
export function liquidacion(cedula, fechaSalida) {
  const p = new URLSearchParams();
  if (fechaSalida) p.set('fechaSalida', fechaSalida);
  const q = p.toString();
  return http.get(`${BASE}/colaborador/${encodeURIComponent(cedula)}/liquidacion${q ? `?${q}` : ''}`);
}

/** Estado del contador de numeración de convenios (GIC-EC-REG-###). */
export function obtenerNumeracion() {
  return http.get(`${BASE}/numeracion`);
}

/** Fija el próximo número del contador de convenios. */
export function actualizarNumeracion(siguienteNumero) {
  return http.put(`${BASE}/numeracion`, { siguienteNumero });
}

export default {
  list, get, create, update, remove, historial, buscarColaborador,
  subirAnexo, eliminarAnexo, descargarAnexo,
  obtenerNumeracion, actualizarNumeracion,
  imprimir, descargarReporteColaborador, dashboard, descargarDashboardPdf, liquidacion,
};
