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
const { renderHtml, buildPdfFilename, ValidationError } = require('./template');
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
