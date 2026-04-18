'use strict';

const path = require('path');

const config = {
  port: parseInt(process.env.PORT || '3000', 10),
  templatesDir: process.env.TEMPLATES_DIR || path.resolve(__dirname, '..', 'templates'),
  outputDir: process.env.OUTPUT_DIR || '/output',
  timeZone: process.env.TZ || 'America/Guayaquil',
  version: process.env.npm_package_version || '1.0.0'
};

module.exports = config;
