'use strict';

/**
 * HTTP entrypoint. Express expone:
 *   GET  /health                  → healthcheck
 *   POST /emitir/certificado      → genera el PDF y lo escribe en OUTPUT_DIR
 */

const path = require('path');
const fs = require('fs');
const fsp = require('fs/promises');
const express = require('express');

const config = require('./config');
const {
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
  renderLiquidacionHtml,
  buildLiquidacionFilename,
  ValidationError
} = require('./template');
const { renderPdf, closeBrowser } = require('./renderer');

const app = express();
app.use(express.json({ limit: '10mb' }));

app.get('/health', (_req, res) => {
  res.status(200).json({ status: 'ok', version: config.version });
});

app.post('/emitir/certificado', async (req, res) => {
  try {
    const html = renderHtml(req.body);
    const { codigo } = req.body.capacitacion;
    const { identificacion } = req.body.asistente;

    const filename = buildPdfFilename(codigo, identificacion);
    const outPath = path.join(config.outputDir, filename);

    await fsp.mkdir(config.outputDir, { recursive: true });

    const pdfBuffer = await renderPdf(html, config.templatesDir);
    await fsp.writeFile(outPath, pdfBuffer);

    const responsePath = `/output/${filename}`;
    return res.status(201).json({ ruta: responsePath });
  } catch (err) {
    if (err instanceof ValidationError) {
      return res.status(400).json({ error: 'ValidationError', message: err.message });
    }
    // eslint-disable-next-line no-console
    console.error('[emisor] error al emitir certificado:', err);
    return res.status(500).json({ error: 'InternalError', message: err.message || 'Error interno' });
  }
});

/**
 * Reporte de Asistencia (post-Fase 12). Layout A4 portrait definido por la
 * plantilla, no usa el fondo del certificado. PDF se escribe en OUTPUT_DIR con
 * nombre `Reporte_Asistencia_{codigo}.pdf`.
 */
app.post('/emitir/reporte-asistencia', async (req, res) => {
  try {
    const html = renderReporteAsistenciaHtml(req.body);
    const { codigo } = req.body.capacitacion;

    const filename = buildPdfReporteFilename(codigo);
    const outPath = path.join(config.outputDir, filename);

    await fsp.mkdir(config.outputDir, { recursive: true });

    const pdfBuffer = await renderPdf(html, config.templatesDir, {
      landscape: false,
      preferCSSPageSize: true,
      // preferCSSPageSize honra @page del HTML, así que margin aquí lo ignora
      // puppeteer, pero lo dejamos explícito para que no herede el del certificado.
      margin: { top: '0mm', right: '0mm', bottom: '0mm', left: '0mm' }
    });
    await fsp.writeFile(outPath, pdfBuffer);

    const responsePath = `/output/${filename}`;
    return res.status(201).json({ ruta: responsePath });
  } catch (err) {
    if (err instanceof ValidationError) {
      return res.status(400).json({ error: 'ValidationError', message: err.message });
    }
    // eslint-disable-next-line no-console
    console.error('[emisor] error al emitir reporte de asistencia:', err);
    return res.status(500).json({ error: 'InternalError', message: err.message || 'Error interno' });
  }
});

/**
 * Convenio — Anexo de Capacitación oficial (GIC-EC-ANX-01). A4 portrait, layout por
 * la propia @page del HTML. PDF en OUTPUT_DIR con nombre `Convenio_{codigoRegistro}.pdf`.
 */
app.post('/emitir/convenio', async (req, res) => {
  try {
    const html = renderConvenioHtml(req.body);
    const codigoRegistro = req.body.convenio && req.body.convenio.codigoRegistro;

    const filename = buildConvenioFilename(codigoRegistro);
    const outPath = path.join(config.outputDir, filename);

    await fsp.mkdir(config.outputDir, { recursive: true });

    const pdfBuffer = await renderPdf(html, config.templatesDir, {
      landscape: false,
      preferCSSPageSize: true,
      margin: { top: '0mm', right: '0mm', bottom: '0mm', left: '0mm' }
    });
    await fsp.writeFile(outPath, pdfBuffer);

    return res.status(201).json({ ruta: `/output/${filename}` });
  } catch (err) {
    if (err instanceof ValidationError) {
      return res.status(400).json({ error: 'ValidationError', message: err.message });
    }
    // eslint-disable-next-line no-console
    console.error('[emisor] error al emitir convenio:', err);
    return res.status(500).json({ error: 'InternalError', message: err.message || 'Error interno' });
  }
});

/**
 * Reporte de Convenios por Colaborador (montos por devengar). A4 landscape, layout
 * por la @page del HTML. PDF en OUTPUT_DIR con nombre `Reporte_Convenios_{cedula}.pdf`.
 */
app.post('/emitir/reporte-convenios', async (req, res) => {
  try {
    const html = renderReporteConveniosHtml(req.body);
    const cedula = req.body.colaborador && req.body.colaborador.cedula;

    const filename = buildReporteConveniosFilename(cedula);
    const outPath = path.join(config.outputDir, filename);

    await fsp.mkdir(config.outputDir, { recursive: true });

    const pdfBuffer = await renderPdf(html, config.templatesDir, {
      landscape: true,
      preferCSSPageSize: true,
      margin: { top: '0mm', right: '0mm', bottom: '0mm', left: '0mm' }
    });
    await fsp.writeFile(outPath, pdfBuffer);

    return res.status(201).json({ ruta: `/output/${filename}` });
  } catch (err) {
    if (err instanceof ValidationError) {
      return res.status(400).json({ error: 'ValidationError', message: err.message });
    }
    // eslint-disable-next-line no-console
    console.error('[emisor] error al emitir reporte de convenios:', err);
    return res.status(500).json({ error: 'InternalError', message: err.message || 'Error interno' });
  }
});

/**
 * Dashboard de Convenios (resumen por curso + gráfico de pastel). A4 portrait, layout
 * por la @page del HTML. PDF en OUTPUT_DIR con nombre `Dashboard_Convenios.pdf`.
 */
app.post('/emitir/dashboard-convenios', async (req, res) => {
  try {
    const html = renderDashboardConveniosHtml(req.body);

    const filename = buildDashboardConveniosFilename();
    const outPath = path.join(config.outputDir, filename);

    await fsp.mkdir(config.outputDir, { recursive: true });

    const pdfBuffer = await renderPdf(html, config.templatesDir, {
      landscape: false,
      preferCSSPageSize: true,
      margin: { top: '0mm', right: '0mm', bottom: '0mm', left: '0mm' }
    });
    await fsp.writeFile(outPath, pdfBuffer);

    return res.status(201).json({ ruta: `/output/${filename}` });
  } catch (err) {
    if (err instanceof ValidationError) {
      return res.status(400).json({ error: 'ValidationError', message: err.message });
    }
    // eslint-disable-next-line no-console
    console.error('[emisor] error al emitir dashboard de convenios:', err);
    return res.status(500).json({ error: 'InternalError', message: err.message || 'Error interno' });
  }
});

/**
 * Liquidación por Desvinculación (montos a reintegrar a la fecha de salida). A4 landscape,
 * layout por la @page del HTML. PDF en OUTPUT_DIR con nombre `Liquidacion_{cedula}.pdf`.
 */
app.post('/emitir/liquidacion-convenios', async (req, res) => {
  try {
    const html = renderLiquidacionHtml(req.body);
    const cedula = req.body.colaborador && req.body.colaborador.cedula;

    const filename = buildLiquidacionFilename(cedula);
    const outPath = path.join(config.outputDir, filename);

    await fsp.mkdir(config.outputDir, { recursive: true });

    const pdfBuffer = await renderPdf(html, config.templatesDir, {
      landscape: true,
      preferCSSPageSize: true,
      margin: { top: '0mm', right: '0mm', bottom: '0mm', left: '0mm' }
    });
    await fsp.writeFile(outPath, pdfBuffer);

    return res.status(201).json({ ruta: `/output/${filename}` });
  } catch (err) {
    if (err instanceof ValidationError) {
      return res.status(400).json({ error: 'ValidationError', message: err.message });
    }
    // eslint-disable-next-line no-console
    console.error('[emisor] error al emitir liquidación por desvinculación:', err);
    return res.status(500).json({ error: 'InternalError', message: err.message || 'Error interno' });
  }
});

// 404 fallback
app.use((_req, res) => {
  res.status(404).json({ error: 'NotFound', message: 'Ruta no encontrada' });
});

const server = app.listen(config.port, () => {
  // eslint-disable-next-line no-console
  console.log(`[emisor] escuchando en puerto ${config.port} — templates=${config.templatesDir} — output=${config.outputDir} — tz=${config.timeZone}`);
});

async function shutdown(signal) {
  // eslint-disable-next-line no-console
  console.log(`[emisor] recibido ${signal}, cerrando...`);
  server.close(() => {});
  await closeBrowser();
  process.exit(0);
}

process.on('SIGTERM', () => shutdown('SIGTERM'));
process.on('SIGINT', () => shutdown('SIGINT'));
