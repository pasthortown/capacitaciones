'use strict';

/**
 * Lectura, normalización de payload y sustitución de tokens del certificado.
 * El HTML resultante es lo que Puppeteer renderiza a PDF.
 */

const fs = require('fs');
const path = require('path');
const config = require('./config');

const TEMPLATE_PATH = path.join(config.templatesDir, 'certificado.html');
const REPORTE_TEMPLATE_PATH = path.join(config.templatesDir, 'reporte_asistencia.html');

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

// ========== Reporte de Asistencia (Fase post-12) =====================================

/**
 * Constantes de paginación calculadas contra el layout de reporte_asistencia.html,
 * con margen de seguridad aplicado tras smoke test: los bordes, paddings y márgenes
 * acumulados reales suman ~10-15mm más que el cálculo teórico.
 *
 * Presupuesto vertical (A4 portrait, margen 14mm top/bottom → 269mm útiles),
 * valores calibrados en vivo contra descripción de altura fija (32mm total):
 *   Primera página: 6 filas de 18mm (108mm) + bloques fijos cabe en 269mm.
 *     (con 7 filas la última quedaba clipeada por el overflow:hidden de .pagina
 *      cuando la metadata llega a su tope; 6 es seguro con margen).
 *   Páginas siguientes: 12 filas de 18mm (216mm) + header oficial + thead cabe.
 *
 * Cada `.pagina` tiene height:269mm + overflow:hidden (ver CSS) — si una fila
 * extra se cuela aquí, se corta visualmente pero NO se rompe el layout ni genera
 * una hoja intermedia sin header.
 */
const REPORTE_FILAS_PRIMERA_PAGINA = 6;
const REPORTE_FILAS_PAGINA_SIGUIENTE = 12;
const REPORTE_FILAS_MINIMAS_HOJA_VACIA = 12;

/**
 * Valida el payload del reporte de asistencia. Estructura:
 *   {
 *     capacitacion: { codigo, tema, capacitador, fechaInicio, duracionHoras,
 *                     departamento?, descripcion?, firmaCapacitadorBase64? },
 *     asistentes:   [ { nombres, apellidos, identificacion, area?, estadoAsistencia?, firmaBase64? } ]
 *   }
 *
 * `asistentes` puede ser lista vacía (la tabla sale sin filas).
 */
function validateReportePayload(body) {
  if (!body || typeof body !== 'object') {
    throw new ValidationError('Payload vacío o no es un objeto.');
  }
  const { capacitacion, asistentes } = body;
  if (!capacitacion || typeof capacitacion !== 'object') {
    throw new ValidationError('`capacitacion` es obligatorio.');
  }
  if (!capacitacion.codigo) throw new ValidationError('`capacitacion.codigo` es obligatorio.');
  if (!capacitacion.tema) throw new ValidationError('`capacitacion.tema` es obligatorio.');
  if (!capacitacion.fechaInicio) throw new ValidationError('`capacitacion.fechaInicio` es obligatorio.');
  if (capacitacion.duracionHoras === undefined || capacitacion.duracionHoras === null) {
    throw new ValidationError('`capacitacion.duracionHoras` es obligatorio.');
  }
  if (asistentes !== undefined && !Array.isArray(asistentes)) {
    throw new ValidationError('`asistentes` debe ser un arreglo.');
  }
}

/**
 * Construye la imagen de firma del capacitador o una caja vacía si no hay firma.
 * El espacio se reserva igual para que la celda del metadata no colapse.
 */
function buildFirmaCapacitadorHtml(firmaBase64) {
  if (!firmaBase64 || typeof firmaBase64 !== 'string') {
    return '<div style="height:26mm;"></div>';
  }
  return `<img class="firma-img" src="${firmaBase64}" alt="Firma del capacitador" />`;
}

/**
 * Construye la fila HTML de un asistente. Posición fija: <tr height=18mm>.
 * Firma se embebe SOLO si estadoAsistencia === 'Presente'. Ausentes → "AUSENTE" rojo;
 * null → celda con guión discreto.
 */
function buildFilaAsistenteHtml(asistente, numero) {
  const nombre = escapeHtml(`${asistente.apellidos || ''} ${asistente.nombres || ''}`.trim());
  const cedula = escapeHtml(asistente.identificacion || '');
  const area = escapeHtml(asistente.area || '—');

  let firmaHtml = '';
  const estado = String(asistente.estadoAsistencia || '').toLowerCase();
  if (estado === 'presente') {
    firmaHtml = asistente.firmaBase64
      ? `<img class="firma-img" src="${asistente.firmaBase64}" alt="firma" />`
      : '';
  } else if (estado === 'ausente') {
    firmaHtml = '<span class="ausente">Ausente</span>';
  } else {
    firmaHtml = '<span class="pendiente">—</span>';
  }

  return `<tr>
    <td class="col-num">${numero}</td>
    <td class="col-nombre">${nombre}</td>
    <td class="col-cedula">${cedula}</td>
    <td class="col-area">${area}</td>
    <td class="col-firma">${firmaHtml}</td>
  </tr>`;
}

/**
 * Construye una fila vacía (placeholder). Mantiene la numeración correlativa.
 * Se usa cuando no hay inscritos (hoja en blanco estilo modelo impreso).
 */
function buildFilaVaciaHtml(numero) {
  return `<tr>
    <td class="col-num">${numero}</td>
    <td class="col-nombre"></td>
    <td class="col-cedula"></td>
    <td class="col-area"></td>
    <td class="col-firma"></td>
  </tr>`;
}

/**
 * Construye el HTML del header oficial (logo DOS + título + cuadro código/versión/página).
 * Se repite en cada página lógica. Recibe el número de página actual y el total
 * para emitir "Página X de Y" ya calculado desde JS (no dependemos del pageNumber
 * nativo de Puppeteer porque ese solo está disponible en headerTemplate/footerTemplate,
 * que es un contexto separado sin acceso a nuestros estilos).
 */
function buildHeaderOficialHtml(codigo, numeroPagina, totalPaginas) {
  return `
    <table class="page-header">
      <tr>
        <td class="logo-cell" rowspan="3">
          <img src="./logotipo-DOS-Color.png" alt="DOS" />
        </td>
        <td class="title-cell" rowspan="3">Registro de Capacitación de Personal</td>
        <td class="meta-cell">Código: ${escapeHtml(codigo)}</td>
      </tr>
      <tr><td class="meta-cell">Versión: 2</td></tr>
      <tr><td class="meta-cell">Página ${numeroPagina} de ${totalPaginas}</td></tr>
    </table>`;
}

/**
 * Construye el bloque de metadata de la capacitación (solo va en la primera página).
 */
function buildMetadataHtml(capacitacion) {
  const tema = escapeHtml(capacitacion.tema || '');
  const capacitador = escapeHtml(capacitacion.capacitador || '—');
  const fecha = escapeHtml(formatFechaEs(capacitacion.fechaInicio, config.timeZone));
  const duracion = `${escapeHtml(formatDuracion(capacitacion.duracionHoras))} hora${Number(capacitacion.duracionHoras) === 1 ? '' : 's'}`;
  const departamento = escapeHtml(capacitacion.departamento || '—');
  const descripcion = escapeHtml(capacitacion.descripcion || '—');
  const firmaCapacitadorHtml = buildFirmaCapacitadorHtml(capacitacion.firmaCapacitadorBase64);

  return `
    <table class="metadata">
      <tr>
        <td colspan="2"><div class="label">Tema de Capacitación:</div><div class="value">${tema}</div></td>
      </tr>
      <tr>
        <td colspan="2"><div class="label">Capacitador:</div><div class="value">${capacitador}</div></td>
      </tr>
      <tr>
        <td>
          <div class="label">Fecha:</div>
          <div class="value">${fecha}</div>
        </td>
        <td class="firma-cell" rowspan="3">
          <div class="firma-label">Firma del Capacitador</div>
          ${firmaCapacitadorHtml}
        </td>
      </tr>
      <tr>
        <td>
          <div class="label">Duración de la Capacitación:</div>
          <div class="value">${duracion}</div>
        </td>
      </tr>
      <tr>
        <td>
          <div class="label">Departamento Capacitado:</div>
          <div class="value">${departamento}</div>
        </td>
      </tr>
      <tr>
        <td colspan="2" class="descripcion-cell"><div class="label">Descripción de la Capacitación:</div><div class="value descripcion-valor">${descripcion}</div></td>
      </tr>
    </table>`;
}

/**
 * Divide la lista ordenada de asistentes en chunks según FILAS_PRIMERA / FILAS_SIGUIENTE.
 * Devuelve un arreglo de arreglos (cada subarreglo es una página).
 */
function paginarAsistentes(asistentes) {
  const paginas = [];
  if (!Array.isArray(asistentes) || asistentes.length === 0) {
    // Hoja modelo en blanco cuando no hay inscritos.
    paginas.push({ filasVacias: REPORTE_FILAS_MINIMAS_HOJA_VACIA, start: 0 });
    return paginas;
  }
  const total = asistentes.length;
  // Primera página: hasta FILAS_PRIMERA_PAGINA.
  let cursor = 0;
  const primera = asistentes.slice(cursor, cursor + REPORTE_FILAS_PRIMERA_PAGINA);
  paginas.push({ items: primera, start: cursor });
  cursor += primera.length;
  // Siguientes páginas: hasta FILAS_PAGINA_SIGUIENTE cada una.
  while (cursor < total) {
    const slice = asistentes.slice(cursor, cursor + REPORTE_FILAS_PAGINA_SIGUIENTE);
    paginas.push({ items: slice, start: cursor });
    cursor += slice.length;
  }
  return paginas;
}

/**
 * Ensambla el HTML de una página lógica (div.pagina): header + metadata opcional + tabla.
 */
function buildPaginaHtml(page, capacitacion, numeroPagina, totalPaginas, esPrimera) {
  const headerHtml = buildHeaderOficialHtml(capacitacion.codigo || '', numeroPagina, totalPaginas);
  const metadataHtml = esPrimera ? buildMetadataHtml(capacitacion) : '';

  let filasHtml = '';
  if (Array.isArray(page.items)) {
    filasHtml = page.items
      .map((a, i) => buildFilaAsistenteHtml(a, page.start + i + 1))
      .join('');
  } else if (page.filasVacias) {
    for (let i = 1; i <= page.filasVacias; i++) {
      filasHtml += buildFilaVaciaHtml(i);
    }
  }

  const tablaHtml = `
    <table class="lista">
      <thead>
        <tr>
          <th class="col-num">N°</th>
          <th class="col-nombre">Nombre y Apellido</th>
          <th class="col-cedula">N° Cédula</th>
          <th class="col-area">Área</th>
          <th class="col-firma">Firma</th>
        </tr>
      </thead>
      <tbody>${filasHtml}</tbody>
    </table>`;

  return `<div class="pagina">${headerHtml}${metadataHtml}${tablaHtml}</div>`;
}

/**
 * Genera el HTML del reporte de asistencia. Se le pasa ya validado.
 */
function renderReporteAsistenciaHtml(payload) {
  validateReportePayload(payload);

  const { capacitacion, asistentes } = payload;
  const paginas = paginarAsistentes(asistentes || []);
  const totalPaginas = paginas.length;

  const paginasHtml = paginas
    .map((p, idx) => buildPaginaHtml(p, capacitacion, idx + 1, totalPaginas, idx === 0))
    .join('');

  const template = fs.readFileSync(REPORTE_TEMPLATE_PATH, 'utf8');

  return template.replace(/\{\{\s*paginasHtml\s*\}\}/g, paginasHtml);
}

function buildPdfReporteFilename(codigo) {
  const safeCodigo = sanitizeFilenamePart(codigo) || 'CAPACITACION';
  return `Reporte_Asistencia_${safeCodigo}.pdf`;
}

module.exports = {
  renderHtml,
  buildPdfFilename,
  renderReporteAsistenciaHtml,
  buildPdfReporteFilename,
  ValidationError
};
