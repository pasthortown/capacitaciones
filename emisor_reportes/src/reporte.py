"""Generación del PDF del dashboard de encuesta.

Diseño:
  - A4 vertical, márgenes 14mm arriba/abajo, 12mm laterales (consistente con
    `reporte_asistencia.html`).
  - Header oficial repetido en cada página (logo DOS + título + código /
    versión / "Página X de Y").
  - Página 1: header + metadata de la capacitación + resumen (totales).
  - Páginas siguientes: una o más preguntas por página. Si una pregunta con
    comentarios largos no cabe, se parte en varias páginas respetando el
    título de la pregunta en la primera.

Gráficas:
  - SeleccionMultiple → barras horizontales (Y=opción, X=conteo).
  - SiNo → pastel con % Sí vs % No.
  - TextoLargo → tabla con dos columnas (Asistente, Comentario).
"""
from __future__ import annotations

import io
import os
import re
from datetime import datetime
from pathlib import Path
from typing import Any

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from matplotlib import font_manager  # noqa: E402
from reportlab.lib import colors  # noqa: E402
from reportlab.lib.pagesizes import A4  # noqa: E402
from reportlab.lib.styles import ParagraphStyle  # noqa: E402
from reportlab.lib.units import mm  # noqa: E402
from reportlab.platypus import (  # noqa: E402
    BaseDocTemplate,
    Flowable,
    Frame,
    Image as RLImage,
    KeepTogether,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


ASSETS_DIR = Path(os.environ.get("ASSETS_DIR", "/app/assets"))
LOGO_PATH = ASSETS_DIR / "logotipo-DOS-Color.png"

# Paleta DOS (referencia del design system + branding del PDF de asistencia).
COLOR_PRIMARIO = colors.HexColor("#1f3a6b")     # azul DOS
COLOR_SECUNDARIO = colors.HexColor("#d43b2f")   # rojo DOS
COLOR_TEXTO = colors.HexColor("#1a1a1a")
COLOR_SUBTEXTO = colors.HexColor("#555555")
COLOR_BORDE = colors.HexColor("#222222")
COLOR_BG_HEADER = colors.HexColor("#f2f2f2")
COLOR_MUY_BUENO = colors.HexColor("#1ea97c")
COLOR_BUENO = colors.HexColor("#6ac0a3")
COLOR_REGULAR = colors.HexColor("#f0ad4e")
COLOR_DEFICIENTE = colors.HexColor("#d0413a")

PAGE_W, PAGE_H = A4
MARGIN_TOP = 14 * mm
MARGIN_BOTTOM = 14 * mm
MARGIN_LEFT = 12 * mm
MARGIN_RIGHT = 12 * mm

# Estilos reportlab. Usamos DejaVu Sans (viene en python:3.12-slim vía fonts-dejavu-core)
# como fallback sólido si la familia registrada falla.
STYLE_BASE = ParagraphStyle(
    name="Base",
    fontName="Helvetica",
    fontSize=9.5,
    leading=12,
    textColor=COLOR_TEXTO,
)
STYLE_LABEL = ParagraphStyle(
    name="Label",
    parent=STYLE_BASE,
    fontName="Helvetica-Bold",
    fontSize=8.5,
    textColor=COLOR_TEXTO,
    leading=10,
    spaceAfter=2,
)
STYLE_VALUE = ParagraphStyle(
    name="Value",
    parent=STYLE_BASE,
    fontSize=10,
    leading=12.5,
)
STYLE_H1 = ParagraphStyle(
    name="H1",
    parent=STYLE_BASE,
    fontName="Helvetica-Bold",
    fontSize=13,
    leading=16,
    textColor=COLOR_PRIMARIO,
    spaceAfter=4,
)
STYLE_H2 = ParagraphStyle(
    name="H2",
    parent=STYLE_BASE,
    fontName="Helvetica-Bold",
    fontSize=11,
    leading=14,
    textColor=COLOR_PRIMARIO,
    spaceAfter=2,
)
STYLE_PREGUNTA = ParagraphStyle(
    name="Pregunta",
    parent=STYLE_BASE,
    fontName="Helvetica-Bold",
    fontSize=10.5,
    leading=13,
    textColor=COLOR_TEXTO,
    spaceAfter=6,
)
STYLE_SUBTITLE = ParagraphStyle(
    name="Subtitle",
    parent=STYLE_BASE,
    fontSize=9,
    leading=11,
    textColor=COLOR_SUBTEXTO,
)
STYLE_COMENTARIO = ParagraphStyle(
    name="Comentario",
    parent=STYLE_BASE,
    fontSize=9.5,
    leading=12.5,
    textColor=COLOR_TEXTO,
)


class ValidationError(ValueError):
    pass


# ================================================================= helpers

def build_pdf_filename(payload: dict) -> str:
    codigo = payload.get("codigo") or payload.get("Codigo") or "CAPACITACION"
    codigo_safe = re.sub(r"[^A-Za-z0-9_-]+", "_", str(codigo)).strip("_") or "CAPACITACION"
    return f"Reporte_Encuesta_{codigo_safe}.pdf"


def _format_fecha_es(iso_str: str) -> str:
    if not iso_str:
        return ""
    try:
        # El backend serializa UTC; basta con mostrar la fecha (sin hora) como la hoja de asistencia.
        dt = datetime.fromisoformat(iso_str.replace("Z", "+00:00"))
    except ValueError:
        return str(iso_str)
    meses = [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ]
    return f"{dt.day} de {meses[dt.month - 1]} de {dt.year}"


def _format_duracion(total_minutos: Any) -> str:
    try:
        m = int(total_minutos)
    except (TypeError, ValueError):
        return str(total_minutos or "")
    if m <= 0:
        return "0 min"
    h, rem = divmod(m, 60)
    if h > 0 and rem == 0:
        return f"{h} hora{'s' if h != 1 else ''}"
    if h == 0 and rem > 0:
        return f"{rem} min"
    return f"{h}h {rem}min"


def _fit(text: str, max_len: int) -> str:
    if not text:
        return ""
    if len(text) <= max_len:
        return text
    return text[: max_len - 1].rstrip() + "…"


# ================================================================= header

class PageState:
    """Comparte el total de páginas entre draw_header y el cálculo total."""
    def __init__(self, codigo: str) -> None:
        self.codigo = codigo
        self.total_paginas = 1  # se actualiza tras primer pase


def make_header_drawer(state: PageState):
    def draw_header(canvas, doc):
        canvas.saveState()
        x0 = MARGIN_LEFT
        y_top = PAGE_H - MARGIN_TOP
        header_height = 22 * mm

        # Caja envolvente
        canvas.setStrokeColor(COLOR_BORDE)
        canvas.setLineWidth(0.6)
        canvas.rect(x0, y_top - header_height, PAGE_W - MARGIN_LEFT - MARGIN_RIGHT, header_height)

        # Logo a la izquierda (30mm de ancho)
        logo_w = 30 * mm
        if LOGO_PATH.exists():
            canvas.drawImage(
                str(LOGO_PATH),
                x0 + 2 * mm,
                y_top - header_height + 2 * mm,
                width=22 * mm,
                height=header_height - 4 * mm,
                preserveAspectRatio=True,
                mask="auto",
            )
        # Separador logo / título
        canvas.line(x0 + logo_w, y_top, x0 + logo_w, y_top - header_height)

        # Meta a la derecha (46mm)
        meta_w = 46 * mm
        meta_x = PAGE_W - MARGIN_RIGHT - meta_w
        canvas.line(meta_x, y_top, meta_x, y_top - header_height)
        meta_row_h = header_height / 3
        canvas.line(meta_x, y_top - meta_row_h, PAGE_W - MARGIN_RIGHT, y_top - meta_row_h)
        canvas.line(meta_x, y_top - 2 * meta_row_h, PAGE_W - MARGIN_RIGHT, y_top - 2 * meta_row_h)
        canvas.setFont("Helvetica", 8.5)
        canvas.drawString(meta_x + 2 * mm, y_top - meta_row_h + 2, f"Código: {state.codigo}")
        canvas.drawString(meta_x + 2 * mm, y_top - 2 * meta_row_h + 2, "Versión: 2")
        canvas.drawString(
            meta_x + 2 * mm,
            y_top - 3 * meta_row_h + 2,
            f"Página {canvas.getPageNumber()} de {state.total_paginas}",
        )

        # Título centrado
        title_x = x0 + logo_w
        title_w = meta_x - title_x
        canvas.setFont("Helvetica-Bold", 11)
        canvas.setFillColor(COLOR_TEXTO)
        canvas.drawCentredString(
            title_x + title_w / 2,
            y_top - header_height / 2 - 3,
            "RESULTADOS DE ENCUESTA DE SATISFACCIÓN",
        )
        canvas.restoreState()
    return draw_header


# ============================================================== gráficas

def _bar_chart_horizontal(labels: list[str], values: list[int], width_mm: float, height_mm: float):
    """Genera un chart horizontal y lo devuelve como BytesIO PNG."""
    fig, ax = plt.subplots(
        figsize=(width_mm / 25.4, height_mm / 25.4),
        dpi=160,
    )
    # Reverse para que el primero aparezca arriba.
    labels_r = list(reversed(labels))
    values_r = list(reversed(values))
    palette = [
        "#1f3a6b", "#2e5aa5", "#4b7fd1", "#7aa8e6", "#b0cfef",
        "#d43b2f", "#e6716a", "#f0a2a0",
    ]
    colors_list = [palette[i % len(palette)] for i in range(len(labels_r))]
    bars = ax.barh(labels_r, values_r, color=colors_list, edgecolor="#222", linewidth=0.5, height=0.6)
    for bar, v in zip(bars, values_r):
        ax.text(
            bar.get_width() + max(0.2, max(values_r) * 0.015 if values_r else 0.2),
            bar.get_y() + bar.get_height() / 2,
            str(v),
            va="center",
            ha="left",
            fontsize=9,
            color="#222",
        )
    ax.set_xlabel("Respuestas")
    ax.set_xlim(0, max(values_r) * 1.2 if values_r and max(values_r) > 0 else 1)
    ax.grid(axis="x", linestyle=":", alpha=0.4)
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)
    plt.tight_layout()
    buf = io.BytesIO()
    fig.savefig(buf, format="png", bbox_inches="tight", facecolor="white")
    plt.close(fig)
    buf.seek(0)
    return buf


def _pie_chart_sino(count_si: int, count_no: int, width_mm: float, height_mm: float):
    total = count_si + count_no
    if total == 0:
        labels = ["Sin respuestas"]
        sizes = [1]
        colors_pie = ["#cccccc"]
    else:
        labels = [f"Sí ({count_si})", f"No ({count_no})"]
        sizes = [count_si, count_no]
        colors_pie = ["#1ea97c", "#d43b2f"]

    fig, ax = plt.subplots(
        figsize=(width_mm / 25.4, height_mm / 25.4),
        dpi=160,
    )

    def autopct(pct):
        return f"{pct:.0f}%" if total > 0 else ""

    wedges, texts, autotexts = ax.pie(
        sizes,
        labels=labels,
        colors=colors_pie,
        autopct=autopct,
        startangle=90,
        counterclock=False,
        wedgeprops=dict(edgecolor="white", linewidth=1.5),
        textprops=dict(fontsize=10, color="#222"),
    )
    for t in autotexts:
        t.set_color("white")
        t.set_fontweight("bold")
    ax.axis("equal")
    plt.tight_layout()
    buf = io.BytesIO()
    fig.savefig(buf, format="png", bbox_inches="tight", facecolor="white")
    plt.close(fig)
    buf.seek(0)
    return buf


# ================================================================ bloques

def _build_metadata_table(payload: dict) -> Table:
    tema = Paragraph(payload.get("tema") or "—", STYLE_VALUE)
    capacitador = Paragraph(payload.get("capacitador") or "—", STYLE_VALUE)
    fecha = Paragraph(_format_fecha_es(payload.get("fechaHoraInicio") or ""), STYLE_VALUE)
    duracion = Paragraph(_format_duracion(payload.get("duracionMinutos")), STYLE_VALUE)
    tipo_actividad = Paragraph(payload.get("tipoActividadNombre") or "—", STYLE_VALUE)
    total_asistentes = int(payload.get("totalAsistentes") or 0)
    total_respondieron = int(payload.get("totalRespondieron") or 0)
    pct = (total_respondieron / total_asistentes * 100) if total_asistentes > 0 else 0.0
    participacion = Paragraph(
        f"{total_respondieron} de {total_asistentes} ({pct:.0f}%)",
        STYLE_VALUE,
    )

    def cell(label: str, value: Paragraph) -> list[Paragraph]:
        return [Paragraph(label, STYLE_LABEL), value]

    data = [
        [cell("TEMA DE CAPACITACIÓN:", tema), cell("TIPO DE ACTIVIDAD:", tipo_actividad)],
        [cell("CAPACITADOR:", capacitador), cell("FECHA:", fecha)],
        [cell("DURACIÓN:", duracion), cell("PARTICIPACIÓN EN LA ENCUESTA:", participacion)],
    ]
    table_w = PAGE_W - MARGIN_LEFT - MARGIN_RIGHT
    col_w = table_w / 2
    table = Table(
        [[c[0] for c in row] + [c[1] for c in row] if False else row for row in data],
        colWidths=[col_w, col_w],
    )
    # Para que reportlab respete el layout label/value dentro de cada celda, las
    # cells contienen listas de flowables (permitido en platypus Table).
    table.setStyle(
        TableStyle(
            [
                ("BOX", (0, 0), (-1, -1), 0.7, COLOR_BORDE),
                ("INNERGRID", (0, 0), (-1, -1), 0.5, COLOR_BORDE),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 4),
                ("RIGHTPADDING", (0, 0), (-1, -1), 4),
                ("TOPPADDING", (0, 0), (-1, -1), 3),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
            ]
        )
    )
    return table


class PreguntaBlock(Flowable):
    """Agrupa título + gráfica como un bloque visual consistente."""

    def __init__(self, title_flow, chart_flow, width):
        super().__init__()
        self.title_flow = title_flow
        self.chart_flow = chart_flow
        self.width = width

    def wrap(self, availWidth, availHeight):
        tw, th = self.title_flow.wrap(availWidth, availHeight)
        cw, ch = self.chart_flow.wrap(availWidth, availHeight - th)
        self._th = th
        self._ch = ch
        return availWidth, th + ch + 6

    def draw(self):
        self.title_flow.drawOn(self.canv, 0, self._ch + 6)
        self.chart_flow.drawOn(self.canv, 0, 0)


def _render_pregunta_seleccion(p: dict) -> list[Flowable]:
    """Pregunta de selección múltiple (incluye SiNo vía barras + pastel para SiNo)."""
    flows: list[Flowable] = []
    pregunta_text = Paragraph(
        f"<b>{_escape(p.get('texto') or '')}</b>",
        STYLE_PREGUNTA,
    )
    total = int(p.get("totalRespuestas") or 0)
    subtitulo = Paragraph(
        f"<font color='#555555'>Respuestas recibidas: {total}</font>",
        STYLE_SUBTITLE,
    )

    conteos = p.get("conteoOpciones") or []
    labels = [str(c.get("opcion") or "") for c in conteos]
    values = [int(c.get("conteo") or 0) for c in conteos]

    chart_buf = _bar_chart_horizontal(
        labels, values,
        width_mm=170,
        height_mm=14 + 10 * max(1, len(labels)),
    )
    img = RLImage(chart_buf, width=170 * mm, height=(14 + 10 * max(1, len(labels))) * mm)
    img.hAlign = "CENTER"

    flows.append(KeepTogether([pregunta_text, subtitulo, Spacer(1, 2 * mm), img]))
    flows.append(Spacer(1, 6 * mm))
    return flows


def _render_pregunta_sino(p: dict) -> list[Flowable]:
    flows: list[Flowable] = []
    pregunta_text = Paragraph(
        f"<b>{_escape(p.get('texto') or '')}</b>",
        STYLE_PREGUNTA,
    )
    conteos = {c.get("opcion"): int(c.get("conteo") or 0) for c in (p.get("conteoOpciones") or [])}
    count_si = conteos.get("Si", 0) + conteos.get("Sí", 0)
    count_no = conteos.get("No", 0)
    total = count_si + count_no
    subtitulo = Paragraph(
        f"<font color='#555555'>Respuestas recibidas: {total}</font>",
        STYLE_SUBTITLE,
    )
    chart_buf = _pie_chart_sino(count_si, count_no, width_mm=110, height_mm=80)
    img = RLImage(chart_buf, width=110 * mm, height=80 * mm)
    img.hAlign = "CENTER"
    flows.append(KeepTogether([pregunta_text, subtitulo, Spacer(1, 2 * mm), img]))
    flows.append(Spacer(1, 6 * mm))
    return flows


def _render_pregunta_texto(p: dict) -> list[Flowable]:
    flows: list[Flowable] = []
    pregunta_text = Paragraph(
        f"<b>{_escape(p.get('texto') or '')}</b>",
        STYLE_PREGUNTA,
    )
    respuestas = p.get("respuestasTexto") or []
    subtitulo = Paragraph(
        f"<font color='#555555'>Comentarios recibidos: {len(respuestas)}</font>",
        STYLE_SUBTITLE,
    )
    flows.append(pregunta_text)
    flows.append(subtitulo)
    flows.append(Spacer(1, 2 * mm))

    if not respuestas:
        flows.append(Paragraph("<i>— Sin comentarios —</i>", STYLE_SUBTITLE))
        flows.append(Spacer(1, 6 * mm))
        return flows

    table_w = PAGE_W - MARGIN_LEFT - MARGIN_RIGHT
    col_asist = 55 * mm
    col_texto = table_w - col_asist
    header = [
        Paragraph("<b>ASISTENTE</b>", STYLE_LABEL),
        Paragraph("<b>COMENTARIO</b>", STYLE_LABEL),
    ]
    rows: list[list] = [header]
    for r in respuestas:
        asistente = _escape(str(r.get("asistente") or "Asistente"))
        texto = _escape(str(r.get("texto") or "").strip())
        if not texto:
            texto = "—"
        rows.append([
            Paragraph(asistente, STYLE_COMENTARIO),
            Paragraph(texto, STYLE_COMENTARIO),
        ])

    tbl = Table(rows, colWidths=[col_asist, col_texto], repeatRows=1)
    tbl.setStyle(
        TableStyle([
            ("BOX", (0, 0), (-1, -1), 0.6, COLOR_BORDE),
            ("INNERGRID", (0, 0), (-1, -1), 0.4, COLOR_BORDE),
            ("BACKGROUND", (0, 0), (-1, 0), COLOR_BG_HEADER),
            ("VALIGN", (0, 0), (-1, -1), "TOP"),
            ("LEFTPADDING", (0, 0), (-1, -1), 4),
            ("RIGHTPADDING", (0, 0), (-1, -1), 4),
            ("TOPPADDING", (0, 0), (-1, -1), 3),
            ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
        ])
    )
    flows.append(tbl)
    flows.append(Spacer(1, 6 * mm))
    return flows


def _escape(s: str) -> str:
    return (
        str(s or "")
        .replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )


# =============================================================== generación

def generate_pdf(payload: dict, out_path: Path) -> None:
    codigo = payload.get("codigo") or "CAPACITACION"
    preguntas = payload.get("preguntas") or []
    if not isinstance(preguntas, list):
        raise ValidationError("`preguntas` debe ser un arreglo.")

    state = PageState(codigo=codigo)

    doc = BaseDocTemplate(
        str(out_path),
        pagesize=A4,
        leftMargin=MARGIN_LEFT,
        rightMargin=MARGIN_RIGHT,
        topMargin=MARGIN_TOP,
        bottomMargin=MARGIN_BOTTOM,
        title="Resultados de encuesta de satisfacción",
    )
    header_height = 22 * mm
    # Frame para el contenido: empieza debajo del header oficial.
    frame_y = MARGIN_BOTTOM
    frame_h = PAGE_H - MARGIN_TOP - MARGIN_BOTTOM - header_height - 4 * mm
    frame_x = MARGIN_LEFT
    frame_w = PAGE_W - MARGIN_LEFT - MARGIN_RIGHT
    frame = Frame(frame_x, frame_y, frame_w, frame_h, showBoundary=0,
                  leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
    template = PageTemplate(id="main", frames=[frame], onPage=make_header_drawer(state))
    doc.addPageTemplates([template])

    story: list[Flowable] = []

    # Metadata (página 1)
    story.append(_build_metadata_table(payload))
    story.append(Spacer(1, 5 * mm))

    # Sección de preguntas
    if not preguntas:
        story.append(Paragraph("Aún no hay preguntas configuradas para este tipo de capacitación.", STYLE_VALUE))
    else:
        story.append(Paragraph("<b>Resumen por pregunta</b>", STYLE_H2))
        story.append(Spacer(1, 3 * mm))
        for p in preguntas:
            tipo = (p.get("tipoPregunta") or "").strip()
            if tipo == "SeleccionMultiple":
                story.extend(_render_pregunta_seleccion(p))
            elif tipo == "SiNo":
                story.extend(_render_pregunta_sino(p))
            elif tipo == "TextoLargo":
                story.extend(_render_pregunta_texto(p))
            else:
                story.append(Paragraph(
                    _escape(p.get("texto") or "") + f" <i>(tipo no soportado: {tipo})</i>",
                    STYLE_PREGUNTA,
                ))

    # Pase 1 — calcular total de páginas
    temp_path = out_path.with_suffix(".tmp.pdf")
    doc_tmp = BaseDocTemplate(
        str(temp_path),
        pagesize=A4,
        leftMargin=MARGIN_LEFT,
        rightMargin=MARGIN_RIGHT,
        topMargin=MARGIN_TOP,
        bottomMargin=MARGIN_BOTTOM,
    )
    doc_tmp.addPageTemplates([PageTemplate(id="main", frames=[Frame(frame_x, frame_y, frame_w, frame_h)],
                                           onPage=make_header_drawer(state))])
    doc_tmp.build(list(_clone_story(story)))
    state.total_paginas = max(1, doc_tmp.page)
    try:
        temp_path.unlink()
    except FileNotFoundError:
        pass

    # Pase 2 — build final con total_paginas correcto
    doc.build(list(_clone_story(story)))


def _clone_story(story: list[Flowable]):
    """Re-yields los flowables. Como son inmutables en nuestro uso, simple iter basta —
    pero queda como extensión si más adelante se necesita deep-copy."""
    for f in story:
        yield f
