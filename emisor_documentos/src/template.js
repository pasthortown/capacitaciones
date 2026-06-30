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
const CONVENIO_TEMPLATE_PATH = path.join(config.templatesDir, 'convenio.html');
const REPORTE_CONVENIOS_TEMPLATE_PATH = path.join(config.templatesDir, 'reporte_convenios.html');
const DASHBOARD_CONVENIOS_TEMPLATE_PATH = path.join(config.templatesDir, 'dashboard_convenios.html');
const LIQUIDACION_CONVENIOS_TEMPLATE_PATH = path.join(config.templatesDir, 'liquidacion_convenios.html');

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

/**
 * Formatea un monto como USD (ej. "$1,200.00"). Si el valor no es numérico
 * devuelve "—" para mantener el patrón de "campo vacío → guión" de los anexos.
 */
function formatMonedaUsd(valor) {
  if (valor === undefined || valor === null || valor === '') return '—';
  const num = Number(valor);
  if (!Number.isFinite(num)) return '—';
  const fmt = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return `$${fmt.format(num)}`;
}

/**
 * Formatea un porcentaje (ej. 66.67 → "66.7%"). Acepta number o string numérica.
 */
function formatPorcentaje(valor) {
  const num = Number(valor);
  if (!Number.isFinite(num)) return '—';
  const str = Number.isInteger(num) ? String(num) : num.toFixed(1);
  return `${str}%`;
}

/**
 * Helper de "campo vacío → guión" para textos. Devuelve el valor escapado o "—".
 */
function valorOGuion(valor) {
  if (valor === undefined || valor === null || String(valor).trim() === '') return '—';
  return escapeHtml(valor);
}

/**
 * Formatea una fecha ISO en es-EC; si está vacía o es inválida devuelve "—"
 * (a diferencia de formatFechaEs que lanza). Útil para campos opcionales de fecha.
 */
function formatFechaEsOGuion(fechaIso) {
  if (!fechaIso) return '—';
  try {
    return escapeHtml(formatFechaEs(fechaIso, config.timeZone));
  } catch (_err) {
    return '—';
  }
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

// ========== 1) Convenio (Anexo de Capacitación GIC-EC-ANX-01) =======================

/**
 * Valida el payload del convenio. Estructura esperada:
 *   {
 *     convenio:    { codigoRegistro, codigoFormato, version, titulo, tipo, tipoCurso,
 *                    nombreCurso, marca, fechaConvenio, fechaFirma, fechaCreacion,
 *                    fechaInicioCurso, fechaFinCurso, horas, resultado, clasificacion,
 *                    modalidadReintegro, plazoTexto, mesesADevengar, montoTotal,
 *                    valorAsumidoEmpresa, convenioFirmado },
 *     colaborador: { nombre, cedula, cargo, area, empresa, centroCostos, jefeInmediato,
 *                    relacionLaboral, fechaIngreso, origen },
 *     items:       [ { tipo, valor, devengable, observacion } ],
 *     solicitadoPor, autorizadoPor
 *   }
 */
function validateConvenioPayload(body) {
  if (!body || typeof body !== 'object') {
    throw new ValidationError('Payload vacío o no es un objeto.');
  }
  const { convenio, colaborador, items } = body;
  if (!convenio || typeof convenio !== 'object') {
    throw new ValidationError('`convenio` es obligatorio.');
  }
  if (!convenio.codigoRegistro) throw new ValidationError('`convenio.codigoRegistro` es obligatorio.');
  if (!colaborador || typeof colaborador !== 'object') {
    throw new ValidationError('`colaborador` es obligatorio.');
  }
  if (!colaborador.nombre) throw new ValidationError('`colaborador.nombre` es obligatorio.');
  if (!colaborador.cedula) throw new ValidationError('`colaborador.cedula` es obligatorio.');
  if (items !== undefined && !Array.isArray(items)) {
    throw new ValidationError('`items` debe ser un arreglo.');
  }
}

function boolSiNo(valor) {
  if (valor === true) return 'Sí';
  if (valor === false) return 'No';
  return '—';
}

function buildConvenioColaboradorHtml(colaborador, convenio) {
  const campos = [
    ['Nombre del colaborador', valorOGuion(colaborador.nombre), true],
    ['Cédula', valorOGuion(colaborador.cedula), false],
    ['Cargo', valorOGuion(colaborador.cargo), false],
    ['Área / departamento', valorOGuion(colaborador.area), false],
    ['Centro de costos', valorOGuion(colaborador.centroCostos), false],
    ['Empresa', valorOGuion(colaborador.empresa), false],
    ['Jefe inmediato', valorOGuion(colaborador.jefeInmediato), false],
    ['Relación laboral', valorOGuion(colaborador.relacionLaboral), false],
    ['Fecha de ingreso', formatFechaEsOGuion(colaborador.fechaIngreso), false],
    ['Fecha de firma', formatFechaEsOGuion(convenio.fechaFirma), false]
  ];
  return `<div class="pgrid2">${campos
    .map(([label, value, span]) => `<div class="pf${span ? ' pspan2' : ''}"><label>${escapeHtml(label)}</label><span>${value}</span></div>`)
    .join('')}</div>`;
}

function buildConvenioEventoHtml(convenio) {
  const campos = [
    ['Título', valorOGuion(convenio.titulo), true],
    ['Nombre del curso', valorOGuion(convenio.nombreCurso), true],
    ['Tipo de evento', valorOGuion(convenio.tipo), false],
    ['Tipo de curso', valorOGuion(convenio.tipoCurso), false],
    ['Marca', valorOGuion(convenio.marca), false],
    ['Fecha de inicio', formatFechaEsOGuion(convenio.fechaInicioCurso), false],
    ['Fecha de fin / aprobación', formatFechaEsOGuion(convenio.fechaFinCurso), false],
    ['Horas', valorOGuion(convenio.horas), false],
    ['Resultado', valorOGuion(convenio.resultado), false],
    ['Convenio firmado', boolSiNo(convenio.convenioFirmado), false]
  ];
  return `<div class="pgrid2">${campos
    .map(([label, value, span]) => `<div class="pf${span ? ' pspan2' : ''}"><label>${escapeHtml(label)}</label><span>${value}</span></div>`)
    .join('')}</div>`;
}

function buildConvenioCostosHtml(items, convenio) {
  const filas = (Array.isArray(items) ? items : [])
    .map((it) => `<tr>
      <td>${valorOGuion(it.tipo)}</td>
      <td class="num">${formatMonedaUsd(it.valor)}</td>
      <td class="center">${boolSiNo(it.devengable)}</td>
      <td>${valorOGuion(it.observacion)}</td>
    </tr>`)
    .join('');

  const filasHtml = filas || `<tr><td colspan="4" class="center">Sin ítems de costo registrados.</td></tr>`;

  return `<table class="costos">
    <thead>
      <tr>
        <th>Tipo</th>
        <th class="num">Valor</th>
        <th class="center">Devengable</th>
        <th>Observación</th>
      </tr>
    </thead>
    <tbody>${filasHtml}</tbody>
    <tfoot>
      <tr><td>Total</td><td class="num">${formatMonedaUsd(convenio.montoTotal)}</td><td colspan="2"></td></tr>
      <tr class="total-empresa"><td>Valor asumido por la empresa</td><td class="num">${formatMonedaUsd(convenio.valorAsumidoEmpresa)}</td><td colspan="2"></td></tr>
    </tfoot>
  </table>`;
}

function buildConvenioReintegroHtml(convenio) {
  const campos = [
    ['Clasificación aplicable', valorOGuion(convenio.clasificacion)],
    ['Modalidad de reintegro', valorOGuion(convenio.modalidadReintegro)],
    ['Plazo de devengación', valorOGuion(convenio.plazoTexto)],
    ['Meses a devengar', valorOGuion(convenio.mesesADevengar)]
  ];
  return `<div class="pgrid2">${campos
    .map(([label, value]) => `<div class="pf"><label>${escapeHtml(label)}</label><span>${value}</span></div>`)
    .join('')}</div>`;
}

function renderConvenioHtml(payload) {
  validateConvenioPayload(payload);

  const { convenio, colaborador, items } = payload;

  const codigoFormato = valorOGuion(convenio.codigoFormato || 'GIC-EC-ANX-01');
  const version = valorOGuion(convenio.version || 'v1');
  const codigoRegistro = valorOGuion(convenio.codigoRegistro);
  // Pie: usa fechaFirma → fechaConvenio → fechaCreacion como respaldo.
  const fechaPie = formatFechaEsOGuion(convenio.fechaFirma || convenio.fechaConvenio || convenio.fechaCreacion);

  const tokens = {
    codigoFormato,
    version,
    codigoRegistro,
    seccionColaborador: buildConvenioColaboradorHtml(colaborador, convenio),
    seccionEvento: buildConvenioEventoHtml(convenio),
    tablaCostos: buildConvenioCostosHtml(items, convenio),
    seccionReintegro: buildConvenioReintegroHtml(convenio),
    nombreColaborador: valorOGuion(colaborador.nombre),
    cedulaColaborador: valorOGuion(colaborador.cedula),
    fechaPie
  };

  const template = fs.readFileSync(CONVENIO_TEMPLATE_PATH, 'utf8');
  return Object.keys(tokens).reduce((html, key) => {
    const re = new RegExp(`\\{\\{\\s*${key}\\s*\\}\\}`, 'g');
    return html.replace(re, tokens[key]);
  }, template);
}

function buildConvenioFilename(codigoRegistro) {
  const safe = sanitizeFilenamePart(codigoRegistro) || 'CONVENIO';
  return `Convenio_${safe}.pdf`;
}

// ========== 2) Reporte de Convenios por Colaborador ==================================

/**
 * Valida el payload del reporte de convenios. Estructura:
 *   {
 *     colaborador: { nombre, cedula, cargo, area, empresa },
 *     fechaCorte, totalPorDevengar,
 *     convenios: [ { codigoRegistro, titulo, nombreCurso, marca, fecha, fechaIngreso,
 *                    estado, montoTotal, valorAsumidoEmpresa, montoDevengado,
 *                    montoPendiente, mesesADevengar, mesesTranscurridos, mesesPendientes,
 *                    porcentajePendiente, solicitadoPor, autorizadoPor, items } ]
 *   }
 */
function validateReporteConveniosPayload(body) {
  if (!body || typeof body !== 'object') {
    throw new ValidationError('Payload vacío o no es un objeto.');
  }
  const { colaborador, convenios } = body;
  if (!colaborador || typeof colaborador !== 'object') {
    throw new ValidationError('`colaborador` es obligatorio.');
  }
  if (!colaborador.cedula) throw new ValidationError('`colaborador.cedula` es obligatorio.');
  if (convenios !== undefined && !Array.isArray(convenios)) {
    throw new ValidationError('`convenios` debe ser un arreglo.');
  }
}

function buildReporteConveniosColaboradorHtml(colaborador) {
  const campos = [
    ['Colaborador', valorOGuion(colaborador.nombre)],
    ['Cédula', valorOGuion(colaborador.cedula)],
    ['Cargo', valorOGuion(colaborador.cargo)],
    ['Área', valorOGuion(colaborador.area)],
    ['Empresa', valorOGuion(colaborador.empresa)]
  ];
  return `<div class="colab-box">${campos
    .map(([label, value]) => `<div class="cf"><label>${escapeHtml(label)}</label><span>${value}</span></div>`)
    .join('')}</div>`;
}

function estadoTagClass(estado) {
  const key = stripAccents(estado || '').toLowerCase().trim();
  if (key === 'vigente') return 'vigente';
  if (key === 'devengado' || key === 'devengado totalmente') return 'devengado';
  return 'otro';
}

function buildReporteConveniosTablaHtml(convenios) {
  const filas = (Array.isArray(convenios) ? convenios : [])
    .map((c) => {
      // Título principal + curso como subtítulo (si difieren). Si solo hay uno, se muestra ese.
      const tituloTxt = String(c.titulo || '').trim();
      const cursoTxt = String(c.nombreCurso || '').trim();
      let tituloHtml = '—';
      if (tituloTxt && cursoTxt && tituloTxt !== cursoTxt) {
        tituloHtml = `${escapeHtml(tituloTxt)}<span style="color:#77787B;font-size:7.5pt;display:block;">${escapeHtml(cursoTxt)}</span>`;
      } else {
        tituloHtml = valorOGuion(tituloTxt || cursoTxt);
      }
      const estadoCls = estadoTagClass(c.estado);
      const resp = `Sol.: ${valorOGuion(c.solicitadoPor)}<br/>Aut.: ${valorOGuion(c.autorizadoPor)}`;
      return `<tr>
        <td class="col-cod center">${valorOGuion(c.codigoRegistro)}</td>
        <td class="col-titulo">${tituloHtml}</td>
        <td class="col-est center"><span class="estado-tag ${estadoCls}">${valorOGuion(c.estado)}</span></td>
        <td class="col-mon num">${formatMonedaUsd(c.valorAsumidoEmpresa)}</td>
        <td class="col-mon num">${formatMonedaUsd(c.montoDevengado)}</td>
        <td class="col-mon num pendiente-cell">${formatMonedaUsd(c.montoPendiente)}</td>
        <td class="col-mes center">${valorOGuion(c.mesesPendientes)}</td>
        <td class="col-pct center">${formatPorcentaje(c.porcentajePendiente)}</td>
        <td class="col-resp">${resp}</td>
      </tr>`;
    })
    .join('');

  const filasHtml = filas || `<tr><td colspan="9" class="center">Sin convenios registrados.</td></tr>`;

  return `<table class="conv">
    <thead>
      <tr>
        <th class="col-cod">Código</th>
        <th class="col-titulo">Título / Curso</th>
        <th class="col-est">Estado</th>
        <th class="col-mon">Valor asumido</th>
        <th class="col-mon">Devengado</th>
        <th class="col-mon">Por devengar</th>
        <th class="col-mes">Meses pend.</th>
        <th class="col-pct">% pend.</th>
        <th class="col-resp">Solicitado / Autorizado por</th>
      </tr>
    </thead>
    <tbody>${filasHtml}</tbody>
  </table>`;
}

function renderReporteConveniosHtml(payload) {
  validateReporteConveniosPayload(payload);

  const { colaborador, convenios, fechaCorte, totalPorDevengar } = payload;

  const tokens = {
    fechaCorte: formatFechaEsOGuion(fechaCorte),
    colaboradorBox: buildReporteConveniosColaboradorHtml(colaborador),
    tablaConvenios: buildReporteConveniosTablaHtml(convenios),
    totalPorDevengar: formatMonedaUsd(totalPorDevengar)
  };

  const template = fs.readFileSync(REPORTE_CONVENIOS_TEMPLATE_PATH, 'utf8');
  return Object.keys(tokens).reduce((html, key) => {
    const re = new RegExp(`\\{\\{\\s*${key}\\s*\\}\\}`, 'g');
    return html.replace(re, tokens[key]);
  }, template);
}

function buildReporteConveniosFilename(cedula) {
  const safe = sanitizeFilenamePart(cedula) || 'COLABORADOR';
  return `Reporte_Convenios_${safe}.pdf`;
}

// ========== 3) Dashboard de Convenios (resumen por curso + pie chart) ================

/**
 * Valida el payload del dashboard de convenios. Estructura:
 *   {
 *     fechaCorte,
 *     cursos: [ { nombreCurso, codigoRegistro, colaborador, costoTotal,
 *                 costoAsumidoDOS, costoDevengado, costoPorDevengar } ],
 *     totales: { costoTotal, costoAsumidoDOS, costoDevengado, costoPorDevengar }
 *   }
 */
function validateDashboardConveniosPayload(body) {
  if (!body || typeof body !== 'object') {
    throw new ValidationError('Payload vacío o no es un objeto.');
  }
  const { cursos, totales } = body;
  if (cursos !== undefined && !Array.isArray(cursos)) {
    throw new ValidationError('`cursos` debe ser un arreglo.');
  }
  if (totales !== undefined && (totales === null || typeof totales !== 'object')) {
    throw new ValidationError('`totales` debe ser un objeto.');
  }
}

function buildDashboardTablaHtml(cursos, totales) {
  const filas = (Array.isArray(cursos) ? cursos : [])
    .map((c) => {
      const cursoCell = `${valorOGuion(c.nombreCurso)}<span class="sub">${valorOGuion(c.codigoRegistro)} · ${valorOGuion(c.colaborador)}</span>`;
      return `<tr>
        <td class="col-curso">${cursoCell}</td>
        <td class="col-mon num">${formatMonedaUsd(c.costoTotal)}</td>
        <td class="col-mon num">${formatMonedaUsd(c.costoAsumidoDOS)}</td>
        <td class="col-mon num">${formatMonedaUsd(c.costoDevengado)}</td>
        <td class="col-mon num">${formatMonedaUsd(c.costoPorDevengar)}</td>
      </tr>`;
    })
    .join('');

  const filasHtml = filas || `<tr><td colspan="5" class="center">Sin cursos registrados.</td></tr>`;
  const t = totales || {};

  return `<table class="resumen">
    <thead>
      <tr>
        <th class="col-curso">Curso</th>
        <th class="col-mon">Costo total</th>
        <th class="col-mon">Costo asumido DOS</th>
        <th class="col-mon">Costo devengado</th>
        <th class="col-mon">Costo por devengar</th>
      </tr>
    </thead>
    <tbody>${filasHtml}</tbody>
    <tfoot>
      <tr>
        <td>Totales</td>
        <td class="num">${formatMonedaUsd(t.costoTotal)}</td>
        <td class="num">${formatMonedaUsd(t.costoAsumidoDOS)}</td>
        <td class="num">${formatMonedaUsd(t.costoDevengado)}</td>
        <td class="num">${formatMonedaUsd(t.costoPorDevengar)}</td>
      </tr>
    </tfoot>
  </table>`;
}

/**
 * Calcula los porcentajes del pie chart (devengado vs por devengar) como porción
 * del costo asumido por DOS, y devuelve el conic-gradient CSS + textos de leyenda.
 * El pie se dibuja SIN librerías externas (CSP bloquea CDNs).
 */
function buildDashboardPie(totales) {
  const t = totales || {};
  const asumido = Number(t.costoAsumidoDOS);
  let devengado = Number(t.costoDevengado);
  let porDevengar = Number(t.costoPorDevengar);
  if (!Number.isFinite(devengado)) devengado = 0;
  if (!Number.isFinite(porDevengar)) porDevengar = 0;

  // Base de cálculo: costoAsumidoDOS si es válido y > 0; si no, suma de las porciones.
  let base = Number.isFinite(asumido) && asumido > 0 ? asumido : devengado + porDevengar;
  let pctDev = 0;
  let pctPen = 0;
  if (base > 0) {
    pctDev = (devengado / base) * 100;
    pctPen = (porDevengar / base) * 100;
  }
  // Redondeo a 1 decimal para el corte del gradiente y la leyenda.
  const pctDevR = Math.round(pctDev * 10) / 10;
  const corte = pctDevR; // grados de devengado en el conic-gradient

  // Colores: verde devengado, rojo por devengar. Si todo es 0, gris.
  const gradient = base > 0
    ? `conic-gradient(#1d7a3a 0% ${corte}%, #E20000 ${corte}% 100%)`
    : 'conic-gradient(#cfcfcf 0% 100%)';

  return {
    pieGradient: gradient,
    pctDevengado: formatPorcentaje(pctDevR),
    pctPorDevengar: formatPorcentaje(Math.round(pctPen * 10) / 10),
    montoDevengado: formatMonedaUsd(devengado),
    montoPorDevengar: formatMonedaUsd(porDevengar),
    costoAsumidoDOS: formatMonedaUsd(Number.isFinite(asumido) ? asumido : base)
  };
}

function renderDashboardConveniosHtml(payload) {
  validateDashboardConveniosPayload(payload);

  const { cursos, totales, fechaCorte } = payload;
  const pie = buildDashboardPie(totales);

  const tokens = {
    fechaCorte: formatFechaEsOGuion(fechaCorte),
    tablaCursos: buildDashboardTablaHtml(cursos, totales),
    pieGradient: pie.pieGradient,
    pctDevengado: pie.pctDevengado,
    pctPorDevengar: pie.pctPorDevengar,
    montoDevengado: pie.montoDevengado,
    montoPorDevengar: pie.montoPorDevengar,
    costoAsumidoDOS: pie.costoAsumidoDOS
  };

  const template = fs.readFileSync(DASHBOARD_CONVENIOS_TEMPLATE_PATH, 'utf8');
  return Object.keys(tokens).reduce((html, key) => {
    const re = new RegExp(`\\{\\{\\s*${key}\\s*\\}\\}`, 'g');
    return html.replace(re, tokens[key]);
  }, template);
}

function buildDashboardConveniosFilename() {
  return 'Dashboard_Convenios.pdf';
}

// ========== 4) Liquidación por Desvinculación ========================================

/**
 * Valida el payload de la liquidación por desvinculación. Estructura:
 *   {
 *     colaborador: { nombre, cedula, cargo, area, empresa },
 *     fechaSalida,
 *     convenios: [ { codigoRegistro, titulo, nombreCurso, marca, clasificacion,
 *                    modalidadReintegro, estado, fechaIngreso, valorAsumidoEmpresa,
 *                    mesesADevengar, mesesTranscurridosASalida, montoReintegro } ],
 *     totalReintegro
 *   }
 *
 * Obligatorio: `colaborador.cedula`. `convenios` (si viene) debe ser arreglo.
 */
function validateLiquidacionPayload(body) {
  if (!body || typeof body !== 'object') {
    throw new ValidationError('Payload vacío o no es un objeto.');
  }
  const { colaborador, convenios } = body;
  if (!colaborador || typeof colaborador !== 'object') {
    throw new ValidationError('`colaborador` es obligatorio.');
  }
  if (!colaborador.cedula) throw new ValidationError('`colaborador.cedula` es obligatorio.');
  if (convenios !== undefined && !Array.isArray(convenios)) {
    throw new ValidationError('`convenios` debe ser un arreglo.');
  }
}

function buildLiquidacionColaboradorHtml(colaborador) {
  const campos = [
    ['Colaborador', valorOGuion(colaborador.nombre)],
    ['Cédula', valorOGuion(colaborador.cedula)],
    ['Cargo', valorOGuion(colaborador.cargo)],
    ['Área', valorOGuion(colaborador.area)],
    ['Empresa', valorOGuion(colaborador.empresa)]
  ];
  return `<div class="colab-box">${campos
    .map(([label, value]) => `<div class="cf"><label>${escapeHtml(label)}</label><span>${value}</span></div>`)
    .join('')}</div>`;
}

function buildLiquidacionTablaHtml(convenios, totalReintegro) {
  const filas = (Array.isArray(convenios) ? convenios : [])
    .map((c) => {
      // Título principal + curso como subtítulo (si difieren). Si solo hay uno, se muestra ese.
      const tituloTxt = String(c.titulo || '').trim();
      const cursoTxt = String(c.nombreCurso || '').trim();
      let tituloHtml = '—';
      if (tituloTxt && cursoTxt && tituloTxt !== cursoTxt) {
        tituloHtml = `${escapeHtml(tituloTxt)}<span style="color:#77787B;font-size:7.5pt;display:block;">${escapeHtml(cursoTxt)}</span>`;
      } else {
        tituloHtml = valorOGuion(tituloTxt || cursoTxt);
      }
      return `<tr>
        <td class="col-cod center">${valorOGuion(c.codigoRegistro)}</td>
        <td class="col-titulo">${tituloHtml}</td>
        <td class="col-marca">${valorOGuion(c.marca)}</td>
        <td class="col-clas">${valorOGuion(c.clasificacion)}</td>
        <td class="col-mod">${valorOGuion(c.modalidadReintegro)}</td>
        <td class="col-fing center">${formatFechaEsOGuion(c.fechaIngreso)}</td>
        <td class="col-mes center">${valorOGuion(c.mesesTranscurridosASalida)}</td>
        <td class="col-mon num">${formatMonedaUsd(c.valorAsumidoEmpresa)}</td>
        <td class="col-mon num reintegro-cell">${formatMonedaUsd(c.montoReintegro)}</td>
      </tr>`;
    })
    .join('');

  const filasHtml = filas || `<tr><td colspan="9" class="center">Sin convenios con reintegro a la fecha indicada.</td></tr>`;
  const totalHtml = filas ? formatMonedaUsd(totalReintegro) : formatMonedaUsd(0);

  return `<table class="conv">
    <thead>
      <tr>
        <th class="col-cod">Código</th>
        <th class="col-titulo">Convenio</th>
        <th class="col-marca">Marca</th>
        <th class="col-clas">Clasificación</th>
        <th class="col-mod">Modalidad de reintegro</th>
        <th class="col-fing">Fecha de ingreso</th>
        <th class="col-mes">Meses a la salida</th>
        <th class="col-mon">Valor asumido</th>
        <th class="col-mon">Monto a reintegrar</th>
      </tr>
    </thead>
    <tbody>${filasHtml}</tbody>
    <tfoot>
      <tr>
        <td colspan="8">Total a reintegrar</td>
        <td class="num">${totalHtml}</td>
      </tr>
    </tfoot>
  </table>`;
}

function renderLiquidacionHtml(payload) {
  validateLiquidacionPayload(payload);

  const { colaborador, convenios, fechaSalida, totalReintegro } = payload;

  const tokens = {
    fechaSalida: formatFechaEsOGuion(fechaSalida),
    colaboradorBox: buildLiquidacionColaboradorHtml(colaborador),
    tablaConvenios: buildLiquidacionTablaHtml(convenios, totalReintegro)
  };

  const template = fs.readFileSync(LIQUIDACION_CONVENIOS_TEMPLATE_PATH, 'utf8');
  return Object.keys(tokens).reduce((html, key) => {
    const re = new RegExp(`\\{\\{\\s*${key}\\s*\\}\\}`, 'g');
    return html.replace(re, tokens[key]);
  }, template);
}

function buildLiquidacionFilename(cedula) {
  const safe = sanitizeFilenamePart(cedula) || 'COLABORADOR';
  return `Liquidacion_${safe}.pdf`;
}

module.exports = {
  renderHtml,
  buildPdfFilename,
  renderReporteAsistenciaHtml,
  buildPdfReporteFilename,
  renderConvenioHtml,
  buildConvenioFilename,
  renderReporteConveniosHtml,
  buildReporteConveniosFilename,
  renderDashboardConveniosHtml,
  buildDashboardConveniosFilename,
  validateLiquidacionPayload,
  renderLiquidacionHtml,
  buildLiquidacionFilename,
  ValidationError
};
