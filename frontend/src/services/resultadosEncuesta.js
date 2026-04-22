/**
 * Servicio admin para el dashboard + descarga del reporte de encuesta.
 */
import http from './http.js';
import { API_BASE } from './http.js';

export function getResultados(capacitacionId) {
  return http.get(`/capacitaciones/${capacitacionId}/encuesta/resultados`);
}

/**
 * Descarga el PDF con los gráficos. Reutiliza el helper genérico `downloadBlob`.
 */
export function descargarReporte(capacitacionId, fallbackFilename) {
  return http.downloadBlob(
    `/capacitaciones/${capacitacionId}/encuesta/reporte`,
    { fallbackFilename: fallbackFilename || 'reporte_encuesta.pdf' },
  );
}

export default { getResultados, descargarReporte };
export { API_BASE };
