"""Servicio HTTP que recibe los datos agregados de una encuesta y devuelve un
PDF generado con matplotlib (gráficas) + reportlab (composición), replicando
el estilo visual de la hoja de asistencia del proyecto.

Endpoints:
  GET  /health                      — ping
  POST /emitir/reporte-encuesta     — body JSON de ResultadoEncuestaDto
                                      → respuesta { "ruta": "/output/<file>.pdf" }
"""
from __future__ import annotations

import os
from pathlib import Path

from flask import Flask, jsonify, request

from . import reporte


app = Flask(__name__)
OUTPUT_DIR = Path(os.environ.get("OUTPUT_DIR", "/output"))
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)


@app.get("/health")
def health():
    return jsonify(status="ok")


@app.post("/emitir/reporte-encuesta")
def emitir_reporte_encuesta():
    payload = request.get_json(force=True, silent=True)
    if not isinstance(payload, dict):
        return jsonify(error="BAD_PAYLOAD", message="Body inválido."), 400

    try:
        filename = reporte.build_pdf_filename(payload)
        out_path = OUTPUT_DIR / filename
        reporte.generate_pdf(payload, out_path)
    except reporte.ValidationError as ex:
        return jsonify(error="VALIDATION", message=str(ex)), 400
    except Exception as ex:  # noqa: BLE001
        app.logger.exception("Error generando reporte")
        return jsonify(error="INTERNAL", message=str(ex)), 500

    return jsonify(ruta=str(out_path)), 201
