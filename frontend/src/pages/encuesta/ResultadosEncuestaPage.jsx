import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Download, PieChart as PieIcon } from 'lucide-react';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import { getResultados, descargarReporte } from '../../services/resultadosEncuesta.js';
import { formatFechaHora, formatDuracion } from '../../utils/formatters.js';
import styles from './ResultadosEncuestaPage.module.css';

/**
 * Dashboard admin con los resultados de la encuesta de una capacitación.
 * Reutiliza el mismo endpoint que el PDF para garantizar datos consistentes.
 */
export default function ResultadosEncuestaPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const toast = useToast();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [data, setData] = useState(null);
  const [downloading, setDownloading] = useState(false);

  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  const cargar = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setLoadError('');
    try {
      const dto = await getResultados(id);
      if (mounted.current) setData(dto);
    } catch (err) {
      if (mounted.current) setData(null);
      if (err instanceof HttpError && err.status === 404) {
        setLoadError('Capacitación no encontrada.');
      } else {
        setLoadError(err?.message || 'No se pudieron cargar los resultados.');
      }
    } finally {
      if (mounted.current) setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  const handleDescargarPdf = async () => {
    if (downloading) return;
    setDownloading(true);
    try {
      const fallback = data?.codigo ? `Reporte_Encuesta_${data.codigo}.pdf` : 'Reporte_Encuesta.pdf';
      await descargarReporte(id, fallback);
      toast.success('PDF generado.');
    } catch (err) {
      if (err instanceof HttpError) {
        if (err.status === 503) {
          toast.error('El servicio de generación de reportes no está disponible. Intenta en unos minutos.');
        } else {
          toast.error(err.message || 'No se pudo generar el PDF.');
        }
      } else {
        toast.error(err?.message || 'No se pudo generar el PDF.');
      }
    } finally {
      if (mounted.current) setDownloading(false);
    }
  };

  if (loading) {
    return (
      <div>
        <div className={styles.loadingWrap}>
          <Spinner size={28} label="Cargando resultados..." />
        </div>
      </div>
    );
  }

  if (loadError || !data) {
    return (
      <div>
        <div className="page-header">
          <div>
            <h1 className="page-header__title">Resultados de encuesta</h1>
          </div>
        </div>
        <div className="card">
          <div className="card__body">
            <p>{loadError || 'No hay datos disponibles.'}</p>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => navigate('/capacitaciones')}
            >
              <ArrowLeft width={16} height={16} />
              <span>Volver</span>
            </button>
          </div>
        </div>
      </div>
    );
  }

  const pctParticipacion = data.totalAsistentes > 0
    ? Math.round((data.totalRespondieron / data.totalAsistentes) * 100)
    : 0;

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Resultados de encuesta</h1>
          <p className="page-header__subtitle">
            {data.codigo ? `${data.codigo} · ` : ''}
            {data.tema}
          </p>
        </div>
        <div className={styles.headerActions}>
          <button
            type="button"
            className="btn btn--primary"
            onClick={handleDescargarPdf}
            disabled={downloading}
            title="Descargar PDF del dashboard"
          >
            {downloading ? <Spinner size={14} label="Generando..." /> : <Download width={16} height={16} />}
            <span>Descargar PDF</span>
          </button>
          <button
            type="button"
            className="btn btn--secondary"
            onClick={() => navigate('/capacitaciones')}
          >
            <ArrowLeft width={16} height={16} />
            <span>Volver</span>
          </button>
        </div>
      </div>

      <div className={styles.summaryGrid}>
        <SummaryCard label="Tipo de actividad" value={data.tipoActividadNombre || '—'} />
        <SummaryCard label="Fecha" value={formatFechaHora(data.fechaHoraInicio) || '—'} />
        <SummaryCard label="Duración" value={formatDuracion(data.duracionMinutos) || '—'} />
        <SummaryCard
          label="Participación"
          value={`${data.totalRespondieron} / ${data.totalAsistentes}`}
          hint={`${pctParticipacion}% respondió`}
        />
      </div>

      {Array.isArray(data.preguntas) && data.preguntas.length > 0 ? (
        <div className={styles.preguntasList}>
          {data.preguntas.map((p, idx) => (
            <PreguntaBlock key={p.id} numero={idx + 1} pregunta={p} />
          ))}
        </div>
      ) : (
        <div className="card">
          <div className="card__body">
            <div className="empty-state">
              <div className="empty-state__title">Aún no hay preguntas configuradas</div>
              <p className="empty-state__description">
                Crea preguntas para el tipo de actividad "{data.tipoActividadNombre || '—'}" en
                Catálogos → Preguntas de encuesta.
              </p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function SummaryCard({ label, value, hint }) {
  return (
    <div className={styles.summaryCard}>
      <div className={styles.summaryLabel}>{label}</div>
      <div className={styles.summaryValue}>{value}</div>
      {hint && <div className={styles.summaryHint}>{hint}</div>}
    </div>
  );
}

function PreguntaBlock({ numero, pregunta }) {
  return (
    <div className={styles.preguntaCard}>
      <div className={styles.preguntaHeader}>
        <div className={styles.preguntaIndex}>{numero}</div>
        <div>
          <div className={styles.preguntaTexto}>{pregunta.texto}</div>
          <div className={styles.preguntaMeta}>
            {labelTipo(pregunta.tipoPregunta)} · {pregunta.totalRespuestas} respuesta
            {pregunta.totalRespuestas === 1 ? '' : 's'}
          </div>
        </div>
      </div>
      <div className={styles.preguntaBody}>
        {renderGraph(pregunta)}
      </div>
    </div>
  );
}

function labelTipo(tipo) {
  if (tipo === 'SeleccionMultiple') return 'Selección múltiple';
  if (tipo === 'SiNo') return 'Sí / No';
  if (tipo === 'TextoLargo') return 'Texto largo';
  return tipo || '—';
}

function renderGraph(pregunta) {
  if (pregunta.tipoPregunta === 'SeleccionMultiple') {
    return <BarChart conteos={pregunta.conteoOpciones || []} />;
  }
  if (pregunta.tipoPregunta === 'SiNo') {
    return <PieSiNo conteos={pregunta.conteoOpciones || []} />;
  }
  if (pregunta.tipoPregunta === 'TextoLargo') {
    return <TextoList respuestas={pregunta.respuestasTexto || []} />;
  }
  return <div className={styles.emptyGraph}>Tipo no soportado.</div>;
}

function BarChart({ conteos }) {
  if (!conteos.length) return <div className={styles.emptyGraph}>Sin respuestas aún.</div>;
  const max = Math.max(1, ...conteos.map((c) => c.conteo));
  const palette = ['#1f3a6b', '#2e5aa5', '#4b7fd1', '#7aa8e6', '#b0cfef', '#d43b2f', '#e6716a'];
  return (
    <div className={styles.barChart}>
      {conteos.map((c, idx) => {
        const pct = (c.conteo / max) * 100;
        return (
          <div key={c.opcion} className={styles.barRow}>
            <div className={styles.barLabel} title={c.opcion}>{c.opcion}</div>
            <div className={styles.barTrack}>
              <div
                className={styles.barFill}
                style={{ width: `${pct}%`, background: palette[idx % palette.length] }}
              />
              <div className={styles.barValue}>{c.conteo}</div>
            </div>
          </div>
        );
      })}
    </div>
  );
}

function PieSiNo({ conteos }) {
  const map = Object.fromEntries(conteos.map((c) => [c.opcion, c.conteo]));
  const countSi = (map['Si'] || 0) + (map['Sí'] || 0);
  const countNo = map['No'] || 0;
  const total = countSi + countNo;

  if (total === 0) return <div className={styles.emptyGraph}>Sin respuestas aún.</div>;

  const pctSi = (countSi / total) * 100;
  const gradient = `conic-gradient(#1ea97c 0 ${pctSi}%, #d0413a ${pctSi}% 100%)`;

  return (
    <div className={styles.pieWrap}>
      <div className={styles.pie} style={{ background: gradient }}>
        <PieIcon className={styles.pieIcon} width={28} height={28} aria-hidden="true" />
      </div>
      <div className={styles.pieLegend}>
        <div className={styles.legendRow}>
          <span className={styles.legendDotYes} /> Sí — {countSi} ({pctSi.toFixed(0)}%)
        </div>
        <div className={styles.legendRow}>
          <span className={styles.legendDotNo} /> No — {countNo} ({(100 - pctSi).toFixed(0)}%)
        </div>
      </div>
    </div>
  );
}

function TextoList({ respuestas }) {
  if (!respuestas.length) return <div className={styles.emptyGraph}>Sin comentarios aún.</div>;
  return (
    <ul className={styles.textoList}>
      {respuestas.map((r, idx) => (
        <li key={idx} className={styles.textoItem}>
          <div className={styles.textoAsistente}>{r.asistente}</div>
          <div className={styles.textoComentario}>{r.texto}</div>
        </li>
      ))}
    </ul>
  );
}
