import { useCallback, useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Send, CheckCircle2 } from 'lucide-react';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import { obtener, responder } from '../../services/encuesta.js';
import { formatFechaHora, formatDuracion } from '../../utils/formatters.js';
import styles from './EncuestaPublicaPage.module.css';

/**
 * Pantalla pública (sin sidebar ni guard admin) de la encuesta de satisfacción.
 *
 * Flujo:
 *  - Lee el id de la capacitación desde la URL (`/encuesta/:capacitacionId`).
 *  - GET /publico/encuesta/:capacitacionId → header + preguntas aplicables.
 *  - El asistente se autoidentifica con su cédula y responde cada pregunta
 *    según su tipo:
 *     * SeleccionMultiple: radio buttons con las opciones del admin.
 *     * SiNo: botones Sí / No.
 *     * TextoLargo: textarea.
 *  - POST → NoContent. Éxito → pantalla de agradecimiento.
 *  - Si ya respondió antes (409 ENCUESTA_YA_RESPONDIDA) se muestra un mensaje
 *    "ya registramos tus respuestas, gracias".
 */

const SINO_OPCIONES = ['Si', 'No'];
const SINO_LABELS = { Si: 'Sí', No: 'No' };

export default function EncuestaPublicaPage() {
  const { capacitacionId } = useParams();
  const toast = useToast();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [capacitacion, setCapacitacion] = useState(null);
  const [preguntas, setPreguntas] = useState([]);
  const [identificacion, setIdentificacion] = useState('');
  const [respuestas, setRespuestas] = useState({}); // preguntaId -> string
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState('');
  const [successMode, setSuccessMode] = useState(null); // 'submitted' | 'already'

  const cargar = useCallback(async () => {
    setLoading(true);
    setLoadError('');
    try {
      const data = await obtener(capacitacionId);
      setCapacitacion(data);
      setPreguntas(Array.isArray(data?.preguntas) ? data.preguntas : []);
    } catch (err) {
      if (err instanceof HttpError) {
        if (err.status === 404) {
          setLoadError('La capacitación no existe o ya no está disponible.');
        } else if (err.status === 409 && err.body?.error === 'CAPACITACION_NO_FINALIZADA') {
          setLoadError('La encuesta estará disponible una vez finalizada la capacitación.');
        } else {
          setLoadError(err.message || 'No se pudo cargar la encuesta.');
        }
      } else {
        setLoadError(err?.message || 'No se pudo cargar la encuesta.');
      }
    } finally {
      setLoading(false);
    }
  }, [capacitacionId]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  const setRespuesta = (preguntaId, valor) => {
    setRespuestas((prev) => ({ ...prev, [preguntaId]: valor }));
    if (formError) setFormError('');
  };

  const todasRespondidas = useMemo(() => {
    return preguntas.every((p) => {
      const v = respuestas[p.id];
      if (typeof v !== 'string') return false;
      if (p.tipoPregunta === 'TextoLargo') return v.trim().length > 0;
      return v.length > 0;
    });
  }, [preguntas, respuestas]);

  const handleSubmit = async (event) => {
    event?.preventDefault?.();
    const ced = identificacion.trim();
    if (!ced) {
      setFormError('Ingresa tu cédula o identificación.');
      return;
    }
    if (!todasRespondidas) {
      setFormError('Por favor responde todas las preguntas antes de enviar.');
      return;
    }
    setSubmitting(true);
    try {
      await responder(capacitacionId, {
        identificacion: ced,
        respuestas: preguntas.map((p) => ({
          preguntaEncuestaId: p.id,
          respuesta: (respuestas[p.id] ?? '').trim(),
        })),
      });
      setSuccessMode('submitted');
    } catch (err) {
      if (err instanceof HttpError) {
        const code = err.body?.error;
        if (err.status === 409 && code === 'ENCUESTA_YA_RESPONDIDA') {
          setSuccessMode('already');
          return;
        }
        if (err.status === 404 && code === 'ASISTENTE_NO_INSCRITO') {
          setFormError(
            'La identificación ingresada no pertenece a un asistente inscrito a esta capacitación.',
          );
          return;
        }
        if (err.status === 409 && code === 'SIN_PREGUNTAS_CONFIGURADAS') {
          setFormError('Aún no hay preguntas configuradas para este tipo de actividad.');
          return;
        }
        setFormError(err.message || 'No se pudo enviar la encuesta.');
      } else {
        setFormError(err?.message || 'No se pudo enviar la encuesta.');
      }
      toast.error(formError || 'No se pudo enviar la encuesta.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className={styles.page}>
        <div className={styles.card}>
          <Spinner size={32} label="Cargando encuesta..." />
        </div>
      </div>
    );
  }

  if (loadError) {
    return (
      <div className={styles.page}>
        <div className={styles.card}>
          <h1 className={styles.title}>Encuesta de satisfacción</h1>
          <p className={styles.error}>{loadError}</p>
        </div>
      </div>
    );
  }

  if (successMode) {
    return (
      <div className={styles.page}>
        <div className={styles.card}>
          <div className={styles.successIcon}>
            <CheckCircle2 width={64} height={64} />
          </div>
          <h1 className={styles.title}>
            {successMode === 'already' ? '¡Ya registramos tus respuestas!' : '¡Gracias por responder!'}
          </h1>
          <p className={styles.subtitle}>
            {successMode === 'already'
              ? 'Detectamos que ya completaste la encuesta para esta capacitación. Agradecemos tu aporte.'
              : 'Tus respuestas fueron enviadas correctamente. Tu opinión nos ayuda a mejorar.'}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <div className={styles.header}>
          <div className={styles.eyebrow}>
            {capacitacion?.codigo} · {capacitacion?.tipoActividadNombre}
          </div>
          <h1 className={styles.title}>{capacitacion?.tema}</h1>
          <div className={styles.meta}>
            {capacitacion?.capacitador && <span>Capacitador: {capacitacion.capacitador}</span>}
            {capacitacion?.fechaHoraInicio && (
              <span>{formatFechaHora(capacitacion.fechaHoraInicio)}</span>
            )}
            {capacitacion?.duracionMinutos != null && (
              <span>{formatDuracion(capacitacion.duracionMinutos)}</span>
            )}
          </div>
          <h2 className={styles.sectionTitle}>Encuesta de satisfacción</h2>
          <p className={styles.subtitle}>
            Ayúdanos con una breve evaluación. Responde cada pregunta según el formato
            indicado.
          </p>
        </div>

        <form onSubmit={handleSubmit} className={styles.form} noValidate>
          <div className="form-group" style={{ position: 'static' }}>
            <label className="form-label form-label--required" style={{ position: 'static' }}>
              Cédula / identificación
            </label>
            <input
              className="form-input"
              value={identificacion}
              onChange={(e) => {
                setIdentificacion(e.target.value);
                if (formError) setFormError('');
              }}
              placeholder="Ingresa tu identificación"
              disabled={submitting}
              required
            />
          </div>

          {preguntas.length === 0 ? (
            <div className={styles.emptyQuestions}>
              Aún no hay preguntas configuradas para este tipo de capacitación.
            </div>
          ) : (
            <ol className={styles.questions}>
              {preguntas.map((p, idx) => (
                <li key={p.id} className={styles.question}>
                  <div className={styles.questionText}>
                    <span className={styles.questionIndex}>{idx + 1}.</span> {p.texto}
                  </div>
                  {renderInput(p, respuestas[p.id], (v) => setRespuesta(p.id, v), submitting)}
                </li>
              ))}
            </ol>
          )}

          {formError && <div className={styles.formError}>{formError}</div>}

          <div className={styles.actions}>
            <button
              type="submit"
              className="btn btn--primary"
              disabled={submitting || preguntas.length === 0}
            >
              {submitting ? (
                <Spinner size={14} label="Enviando..." />
              ) : (
                <>
                  <Send width={16} height={16} />
                  <span>Enviar respuestas</span>
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function renderInput(pregunta, value, onChange, disabled) {
  if (pregunta.tipoPregunta === 'SeleccionMultiple') {
    const opciones = Array.isArray(pregunta.opciones) ? pregunta.opciones : [];
    return (
      <div className={styles.optionsList} role="radiogroup" aria-label={pregunta.texto}>
        {opciones.map((op) => {
          const selected = value === op;
          return (
            <button
              key={op}
              type="button"
              role="radio"
              aria-checked={selected}
              disabled={disabled}
              onClick={() => onChange(op)}
              className={`${styles.optionButton}${selected ? ` ${styles.optionButtonSelected}` : ''}`}
            >
              {op}
            </button>
          );
        })}
      </div>
    );
  }

  if (pregunta.tipoPregunta === 'SiNo') {
    return (
      <div className={styles.sinoList} role="radiogroup" aria-label={pregunta.texto}>
        {SINO_OPCIONES.map((op) => {
          const selected = value === op;
          return (
            <button
              key={op}
              type="button"
              role="radio"
              aria-checked={selected}
              disabled={disabled}
              onClick={() => onChange(op)}
              className={`${styles.sinoButton} ${
                selected
                  ? op === 'Si'
                    ? styles.sinoButtonYes
                    : styles.sinoButtonNo
                  : ''
              }`}
            >
              {SINO_LABELS[op]}
            </button>
          );
        })}
      </div>
    );
  }

  // TextoLargo
  return (
    <textarea
      className={styles.textarea}
      rows={4}
      maxLength={2000}
      value={value ?? ''}
      onChange={(e) => onChange(e.target.value)}
      disabled={disabled}
      placeholder="Escribe tu comentario..."
    />
  );
}
