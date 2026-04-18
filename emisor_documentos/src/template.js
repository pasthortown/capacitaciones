'use strict';

/**
 * Lectura, normalización de payload y sustitución de tokens del certificado.
 * El HTML resultante es lo que Puppeteer renderiza a PDF.
 */

const fs = require('fs');
const path = require('path');
const config = require('./config');

const TEMPLATE_PATH = path.join(config.templatesDir, 'certificado.html');

// Mapeo artículo + sustantivo por tipo de actividad (case-insensitive, sin acentos).
const ACTIVIDAD_MAP = {
  charla: 'la charla',
  workshop: 'el workshop',
  capacitacion: 'la capacitación',
  curso: 'el curso',
  taller: 'el taller',
  seminario: 'el seminario'
};

function stripAccents(str) {
  return String(str || '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '');
}

function articuloSustantivo(tipoActividad) {
  const key = stripAccents(tipoActividad).toLowerCase().trim();
  if (ACTIVIDAD_MAP[key]) return ACTIVIDAD_MAP[key];
  const fallback = String(tipoActividad || '').toLowerCase().trim();
  return `la ${fallback}`;
}

/**
 * Fase 12 — la etiqueta del certificado se decide con `certificadoEfectivo` (valor
 * calculado por el backend que puede diferir del `tipoCertificacion` original: una
 * capacitación Aprobación con calificación < puntaje mínimo imprime "DE ASISTENCIA").
 * Si el backend aún no envía `certificadoEfectivo` (compat con clientes viejos), se
 * usa `tipoCertificacion` como fallback.
 */
function tipoCertificacionLabel(certificadoEfectivo, tipoCertificacion) {
  const key = stripAccents(certificadoEfectivo || tipoCertificacion).toLowerCase().trim();
  if (key === 'aprobacion') return 'DE APROBACIÓN';
  if (key === 'asistencia') return 'DE ASISTENCIA';
  // Default: participación
  return 'DE PARTICIPACIÓN';
}

function formatFechaEs(fechaIso, timeZone) {
  const date = new Date(fechaIso);
  if (Number.isNaN(date.getTime())) {
    throw new Error(`Fecha inválida: ${fechaIso}`);
  }
  const fmt = new Intl.DateTimeFormat('es-EC', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    timeZone: timeZone || config.timeZone
  });
  // Intl devuelve "17 de abril de 2026" para es-EC
  return fmt.format(date);
}

function sanitizeFilenamePart(value) {
  return String(value || '')
    .replace(/[^A-Za-z0-9_-]/g, '_')
    .replace(/_+/g, '_')
    .replace(/^_|_$/g, '');
}

function buildPdfFilename(codigo, identificacion) {
  const safeCodigo = sanitizeFilenamePart(codigo) || 'CERTIFICADO';
  const safeId = sanitizeFilenamePart(identificacion) || 'ASISTENTE';
  return `${safeCodigo}_${safeId}.pdf`;
}

function escapeHtml(unsafe) {
  return String(unsafe || '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

/**
 * Fase 12 — lee el logo del volumen compartido (/imagen_capacitaciones) y lo devuelve
 * como <img> con data URL, para que el HTML renderizado sea autocontenido y Puppeteer
 * no tenga que resolver file:// externos.
 *
 * Seguridad: se valida que la ruta recibida esté dentro de `imagenCapacitacionesDir`
 * — así un payload malicioso con `../../etc/passwd` no llega a ser leído.
 * Si cualquier paso falla (ruta inválida, archivo inexistente, mime desconocido),
 * devuelve string vacío y loguea warning: la falta de logo no debe bloquear la emisión.
 */
const LOGO_MIME_BY_EXT = {
  png: 'image/png',
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  webp: 'image/webp',
  svg: 'image/svg+xml'
};

function buildLogoHtml(logoPathLocal) {
  if (!logoPathLocal || typeof logoPathLocal !== 'string') return '';
  try {
    const absPath = path.resolve(logoPathLocal);
    const allowedRoot = path.resolve(config.imagenCapacitacionesDir);
    // Path traversal guard: el archivo debe estar dentro del volumen de logos.
    if (!absPath.startsWith(allowedRoot + path.sep) && absPath !== allowedRoot) {
      console.warn('[emisor] logoPathLocal fuera del volumen permitido:', logoPathLocal);
      return '';
    }
    if (!fs.existsSync(absPath)) {
      console.warn('[emisor] logo no encontrado:', absPath);
      return '';
    }
    const ext = path.extname(absPath).toLowerCase().replace(/^\./, '');
    const mime = LOGO_MIME_BY_EXT[ext];
    if (!mime) {
      console.warn('[emisor] extensión de logo no soportada:', ext);
      return '';
    }
    const buf = fs.readFileSync(absPath);
    const b64 = buf.toString('base64');
    return `<img class="logo-capacitacion" src="data:${mime};base64,${b64}" alt="" />`;
  } catch (err) {
    console.warn('[emisor] no se pudo leer el logo:', err && err.message);
    return '';
  }
}

/**
 * Fase 12 — texto "con una calificación de X.X / 10" solo cuando el certificado
 * efectivo es Aprobación y hay calificación registrada. En Asistencia/Participación
 * no se muestra calificación (sería engañoso porque el asistente no aprobó).
 */
function buildCalificacionHtml(certificadoEfectivo, calificacion, puntajeMinimo) {
  const key = stripAccents(certificadoEfectivo || '').toLowerCase().trim();
  if (key !== 'aprobacion') return '';
  if (calificacion === undefined || calificacion === null) return '';
  const num = Number(calificacion);
  if (!Number.isFinite(num)) return '';
  const calStr = Number.isInteger(num) ? String(num) : num.toFixed(1);
  // El puntaje mínimo lo mostramos como referencia si está disponible; si no, solo la nota.
  if (puntajeMinimo !== undefined && puntajeMinimo !== null) {
    const min = Number(puntajeMinimo);
    const minStr = Number.isFinite(min) ? (Number.isInteger(min) ? String(min) : min.toFixed(1)) : '';
    if (minStr) {
      return `<div class="calificacion">Con una calificación de <strong>${escapeHtml(calStr)}</strong> / 10 (mínimo de aprobación: ${escapeHtml(minStr)}).</div>`;
    }
  }
  return `<div class="calificacion">Con una calificación de <strong>${escapeHtml(calStr)}</strong> / 10.</div>`;
}

function buildFirmantesHtml(firmantes) {
  if (!Array.isArray(firmantes) || firmantes.length === 0) {
    return '';
  }
  const items = firmantes
    .map((f) => {
      const nombres = escapeHtml(f.nombres || '');
      const cargo = escapeHtml(f.cargo || '');
      const empresa = escapeHtml(f.empresa || '');
      const firma = f.firmaBase64 ? String(f.firmaBase64) : '';
      const firmaImg = firma
        ? `<img class="firma-img" src="${firma}" alt="firma" />`
        : '<span class="firma-img firma-placeholder"></span>';
      return `
        <div class="firmante">
          ${firmaImg}
          <div class="firmante-linea"></div>
          <div class="firmante-nombre">${nombres}</div>
          <div class="firmante-cargo">${cargo}</div>
          <div class="firmante-empresa">${empresa}</div>
        </div>
      `;
    })
    .join('');
  return `<div class="firmantes">${items}</div>`;
}

function validatePayload(body) {
  if (!body || typeof body !== 'object') {
    throw new ValidationError('Payload vacío o no es un objeto.');
  }
  const { capacitacion, asistente, firmantes } = body;
  if (!capacitacion || typeof capacitacion !== 'object') {
    throw new ValidationError('`capacitacion` es obligatorio.');
  }
  if (!capacitacion.codigo) throw new ValidationError('`capacitacion.codigo` es obligatorio.');
  if (!capacitacion.tema) throw new ValidationError('`capacitacion.tema` es obligatorio.');
  if (!capacitacion.tipoActividad) throw new ValidationError('`capacitacion.tipoActividad` es obligatorio.');
  if (!capacitacion.tipoCertificacion) throw new ValidationError('`capacitacion.tipoCertificacion` es obligatorio.');
  if (!capacitacion.fechaInicio) throw new ValidationError('`capacitacion.fechaInicio` es obligatorio.');
  if (capacitacion.duracionHoras === undefined || capacitacion.duracionHoras === null) {
    throw new ValidationError('`capacitacion.duracionHoras` es obligatorio.');
  }
  if (!asistente || typeof asistente !== 'object') {
    throw new ValidationError('`asistente` es obligatorio.');
  }
  if (!asistente.identificacion) throw new ValidationError('`asistente.identificacion` es obligatorio.');
  if (!asistente.nombres && !asistente.apellidos) {
    throw new ValidationError('`asistente.nombres` o `asistente.apellidos` son obligatorios.');
  }
  if (firmantes !== undefined && !Array.isArray(firmantes)) {
    throw new ValidationError('`firmantes` debe ser un arreglo.');
  }
}

class ValidationError extends Error {
  constructor(message) {
    super(message);
    this.name = 'ValidationError';
  }
}

/**
 * Toma el payload validado y devuelve el HTML listo para Puppeteer.
 */
function renderHtml(payload) {
  validatePayload(payload);

  const { capacitacion, asistente, firmantes, certificadoEfectivo } = payload;

  const nombreCompleto = `${asistente.nombres || ''} ${asistente.apellidos || ''}`.trim().toUpperCase();
  const tipoCertStr = tipoCertificacionLabel(certificadoEfectivo, capacitacion.tipoCertificacion);
  const articulo = articuloSustantivo(capacitacion.tipoActividad);
  const fechaFormateada = formatFechaEs(capacitacion.fechaInicio, config.timeZone);
  const duracionHoras = formatDuracion(capacitacion.duracionHoras);
  const tema = String(capacitacion.tema || '').toUpperCase();
  const firmantesHtml = buildFirmantesHtml(firmantes || []);
  const logoHtml = buildLogoHtml(capacitacion.logoPathLocal);
  const calificacionHtml = buildCalificacionHtml(
    certificadoEfectivo,
    asistente.calificacion,
    capacitacion.puntajeMinimo
  );

  const template = fs.readFileSync(TEMPLATE_PATH, 'utf8');

  const tokens = {
    tipoCertificacion: tipoCertStr,
    nombreCompleto: escapeHtml(nombreCompleto),
    articuloSustantivo: escapeHtml(articulo),
    tema: escapeHtml(tema),
    fechaFormateada: escapeHtml(fechaFormateada),
    duracionHoras: escapeHtml(duracionHoras),
    firmantesHtml,
    logoHtml,
    calificacionHtml
  };

  return Object.keys(tokens).reduce((html, key) => {
    // Sustitución global de {{token}}.
    const re = new RegExp(`\\{\\{\\s*${key}\\s*\\}\\}`, 'g');
    return html.replace(re, tokens[key]);
  }, template);
}

function formatDuracion(duracionHoras) {
  const num = Number(duracionHoras);
  if (!Number.isFinite(num)) return String(duracionHoras);
  // Muestra sin decimales si es entero; si no, 1 decimal.
  if (Number.isInteger(num)) return String(num);
  return num.toFixed(1);
}

module.exports = {
  renderHtml,
  buildPdfFilename,
  ValidationError
};
