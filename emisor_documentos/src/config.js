'use strict';

const path = require('path');

const config = {
  port: parseInt(process.env.PORT || '3000', 10),
  templatesDir: process.env.TEMPLATES_DIR || path.resolve(__dirname, '..', 'templates'),
  outputDir: process.env.OUTPUT_DIR || '/output',
  // Fase 12 — volumen compartido (RO) con los logos de capacitaciones. El path recibido
  // por el API (logoPathLocal) debe estar debajo de este directorio; se valida antes de leer.
  imagenCapacitacionesDir: process.env.IMAGEN_CAPACITACIONES_DIR || '/imagen_capacitaciones',
  timeZone: process.env.TZ || 'America/Guayaquil',
  version: process.env.npm_package_version || '1.0.0'
};

module.exports = config;
