'use strict';

/**
 * Wrapper de Puppeteer. Mantiene una instancia singleton del browser por proceso
 * y expone renderPdf(html, baseDir) que genera el PDF en formato A4 landscape.
 */

const path = require('path');
const os = require('os');
const fs = require('fs');
const fsp = require('fs/promises');
const puppeteer = require('puppeteer');

let browserPromise = null;

async function getBrowser() {
  if (!browserPromise) {
    browserPromise = puppeteer.launch({
      headless: 'new',
      args: [
        '--no-sandbox',
        '--disable-setuid-sandbox',
        '--disable-dev-shm-usage'
      ]
    });
    // Si la promesa falla, permitimos reintento en el siguiente request.
    browserPromise.catch(() => {
      browserPromise = null;
    });
  }
  return browserPromise;
}

async function closeBrowser() {
  if (!browserPromise) return;
  try {
    const browser = await browserPromise;
    await browser.close();
  } catch (_err) {
    // noop
  } finally {
    browserPromise = null;
  }
}

/**
 * Renderiza el HTML a PDF. Escribimos el HTML en un archivo temporal dentro de
 * `baseDir` (carpeta de plantillas) y usamos `page.goto('file://...')` para que
 * Puppeteer resuelva los assets relativos (fonts, fondo.png) de forma natural.
 *
 * `pdfOptions` permite al caller sobreescribir los defaults (formato/landscape/margin).
 * Por default usa el layout del certificado: A4 landscape, márgenes 0. El reporte
 * de asistencia pasa `{ landscape: false, preferCSSPageSize: true }` y deja que
 * la propia `@page` del HTML defina tamaño y márgenes.
 */
async function renderPdf(html, baseDir, pdfOptions = {}) {
  const browser = await getBrowser();
  const page = await browser.newPage();

  const tmpFile = path.join(baseDir, `._render_${process.pid}_${Date.now()}_${Math.random().toString(36).slice(2, 8)}.html`);
  await fsp.writeFile(tmpFile, html, 'utf8');

  try {
    await page.goto(`file://${tmpFile}`, { waitUntil: 'networkidle0' });
    // Asegurar que las fuentes custom terminen de cargar antes de imprimir.
    await page.evaluateHandle('document.fonts.ready');

    const defaultOptions = {
      format: 'A4',
      landscape: true,
      printBackground: true,
      preferCSSPageSize: false,
      margin: { top: '0mm', right: '0mm', bottom: '0mm', left: '0mm' }
    };

    const pdfBuffer = await page.pdf({ ...defaultOptions, ...pdfOptions });
    return pdfBuffer;
  } finally {
    await page.close().catch(() => {});
    await fsp.unlink(tmpFile).catch(() => {});
  }
}

module.exports = {
  renderPdf,
  closeBrowser
};
