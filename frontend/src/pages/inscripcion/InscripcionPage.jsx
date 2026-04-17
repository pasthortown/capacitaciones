import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Send, RotateCcw } from 'lucide-react';
import SignaturePad from '../../components/SignaturePad/SignaturePad.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import EmailConSufijo from '../../components/EmailConSufijo/EmailConSufijo.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import { getCapacitacion, inscribir } from '../../services/inscripcion.js';
import { formatFechaHora, formatDuracion } from '../../utils/formatters.js';
import styles from './InscripcionPage.module.css';

/**
 * Pantalla pública (sin sidebar ni guard admin) de inscripción.
 *
 * Flujo:
 *  - Lee `?token=...` del querystring (JWT específico del link de inscripción).
 *  - GET /inscripcion/capacitacion con ese Bearer → muestra datos read-only
 *    de la capacitación + el listado de áreas para el combo.
 *  - POST /inscripcion/capacitacion con los datos del asistente + firma.
 *  - Éxito: pantalla de confirmación + botón "Inscribir a otra persona".
 *
 * Aislamiento: el servicio `inscripcion.js` usa fetch directo (no http.js),
 * así un 401 del link público NO dispara `auth:expired` del admin logueado.
 */

const INITIAL_FORM = {
  nombres: '',
  apellidos: '',
  identificacion: '',
  areaId: '',
  emailUsuario: '',
  firma: null,
};

export default function InscripcionPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const toast = useToast();

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [loadError, setLoadError] = useState('');
  const [formError, setFormError] = useState('');
  const [capacitacion, setCapacitacion] = useState(null);
  const [areas, setAreas] = useState([]);
  const [form, setForm] = useState(INITIAL_FORM);
  const [success, setSuccess] = useState(null); // { nombres, apellidos }

  const mapLoadError = (err) => {
    if (err instanceof HttpError) {
      if (err.status === 401 || err.status === 403) {
        return 'Enlace inválido o expirado. Solicita uno nuevo al administrador.';
      }
      if (err.status === 404) {
        return 'Capacitación no encontrada.';
      }
      if (err.status === 409) {
        return 'Las inscripciones están cerradas para esta capacitación.';
      }
    }
    return err?.message || 'Ocurrió un error inesperado.';
  };

  const mapSubmitError = (err) => {
    if (err instanceof HttpError) {
      if (err.status === 409) {
        return 'Ya existe una inscripción con esa identificación.';
      }
      if (err.status === 401 || err.status === 403) {
        return 'Enlace inválido o expirado. Solicita uno nuevo al administrador.';
      }
    }
    return err?.message || 'Ocurrió un error al enviar la inscripción.';
  };

  const fetchData = useCallback(() => {
    if (!token) {
      setLoading(false);
      return () => {};
    }
    let cancelled = false;
    setLoading(true);
    setLoadError('');
    getCapacitacion(token)
      .then((dto) => {
        if (cancelled) return;
        setCapacitacion(dto?.capacitacion || null);
        setAreas(Array.isArray(dto?.areas) ? dto.areas : []);
      })
      .catch((err) => {
        if (cancelled) return;
        setLoadError(mapLoadError(err));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [token]);

  useEffect(() => {
    const cleanup = fetchData();
    return cleanup;
  }, [fetchData]);

  const resetForm = () => {
    setForm(INITIAL_FORM);
    setFormError('');
    setSuccess(null);
  };

  const validate = () => {
    const nombres = form.nombres.trim();
    const apellidos = form.apellidos.trim();
    const identificacion = form.identificacion.trim();
    const areaId = form.areaId;
    const emailLocal = form.emailUsuario.trim();
    const firma = form.firma;

    if (!nombres) return 'Ingresa los nombres.';
    if (!apellidos) return 'Ingresa los apellidos.';
    if (!identificacion) return 'Ingresa el número de identificación.';
    if (!areaId) return 'Selecciona un área.';
    if (!emailLocal) return 'Ingresa el correo.';
    if (emailLocal.includes('@')) {
      return 'Ingresa solo la parte local del correo; el dominio @dos.com.ec se agrega automáticamente.';
    }
    if (!firma) return 'La firma es obligatoria.';
    return null;
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (submitting || !token) return;
    setFormError('');
    const err = validate();
    if (err) {
      setFormError(err);
      return;
    }
    setSubmitting(true);
    try {
      const payload = {
        nombres: form.nombres.trim(),
        apellidos: form.apellidos.trim(),
        identificacion: form.identificacion.trim(),
        areaId: form.areaId,
        emailUsuario: form.emailUsuario.trim(),
        firma: form.firma,
      };
      await inscribir(token, payload);
      setSuccess({ nombres: payload.nombres, apellidos: payload.apellidos });
      toast.success('Inscripción registrada.');
    } catch (error) {
      const message = mapSubmitError(error);
      setFormError(message);
      toast.error(message);
    } finally {
      setSubmitting(false);
    }
  };

  // --- Render: sin token ---
  if (!token) {
    return (
      <div className={styles.page}>
        <div className={`${styles.fullMessage} ${styles.alert} ${styles.alertError}`}>
          Enlace inválido. Solicita al administrador uno nuevo.
        </div>
      </div>
    );
  }

  // --- Render: cargando ---
  if (loading) {
    return (
      <div className={styles.page}>
        <div className={styles.loadingWrap}>
          <Spinner size={36} label="Cargando capacitación..." />
        </div>
      </div>
    );
  }

  // --- Render: error de carga ---
  if (loadError && !capacitacion) {
    return (
      <div className={styles.page}>
        <div className={`${styles.fullMessage} ${styles.alert} ${styles.alertError}`}>
          {loadError}
        </div>
      </div>
    );
  }

  // --- Render: éxito (post-submit) ---
  if (success) {
    return (
      <div className={styles.page}>
        <header className={styles.header}>
          <h1 className={styles.title}>Inscripción</h1>
          <p className={styles.subtitle}>
            {capacitacion?.codigo ? `${capacitacion.codigo} · ` : ''}
            {capacitacion?.tema || ''}
          </p>
        </header>
        <div className={styles.container}>
          <section
            className={`${styles.card} ${styles.successCard}`}
            aria-label="Inscripción registrada"
          >
            <h2 className={styles.sectionTitle}>Inscripción registrada</h2>
            <p className={styles.successText}>
              Gracias, <strong>{success.nombres} {success.apellidos}</strong>. Tu
              inscripción fue registrada correctamente.
            </p>
            <div className={styles.actions}>
              <button
                type="button"
                className="btn btn--secondary"
                onClick={resetForm}
              >
                <RotateCcw width={16} height={16} />
                <span>Inscribir a otra persona</span>
              </button>
            </div>
          </section>
        </div>
      </div>
    );
  }

  // --- Render: formulario ---
  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Inscripción</h1>
        <p className={styles.subtitle}>
          {capacitacion?.codigo ? `${capacitacion.codigo} · ` : ''}
          {capacitacion?.tema || ''}
        </p>
      </header>

      <div className={styles.container}>
        {/* Card 1: datos read-only de la capacitación */}
        <section className={styles.card} aria-label="Datos de la capacitación">
          <h2 className={styles.sectionTitle}>Datos de la capacitación</h2>
          <div className={styles.grid}>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Código</span>
              <span className={styles.fieldValue}>
                {capacitacion?.codigo || '—'}
              </span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Tema</span>
              <span className={styles.fieldValue}>
                {capacitacion?.tema || '—'}
              </span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Capacitador</span>
              <span className={styles.fieldValue}>
                {capacitacion?.capacitador || '—'}
              </span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Fecha/hora inicio</span>
              <span className={styles.fieldValue}>
                {formatFechaHora(capacitacion?.fechaHoraInicio) || '—'}
              </span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Duración</span>
              <span className={styles.fieldValue}>
                {formatDuracion(capacitacion?.duracionMinutos)}
              </span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Modalidad</span>
              <span className={styles.fieldValue}>
                {capacitacion?.modalidad?.nombre || '—'}
              </span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Tipo actividad</span>
              <span className={styles.fieldValue}>
                {capacitacion?.tipoActividad?.nombre || '—'}
              </span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Estado</span>
              <span className={styles.fieldValue}>
                <EstadoBadge estado={capacitacion?.estado} />
              </span>
            </div>
          </div>
        </section>

        {/* Card 2: formulario de inscripción */}
        <section className={styles.card} aria-label="Formulario de inscripción">
          <h2 className={styles.sectionTitle}>Tus datos</h2>
          <form onSubmit={handleSubmit} noValidate>
            <div className={styles.twoCols}>
              <div className={styles.formRow}>
                <label className={styles.formLabel} htmlFor="nombres">
                  Nombres
                </label>
                <input
                  id="nombres"
                  type="text"
                  className="form-input"
                  value={form.nombres}
                  onChange={(e) =>
                    setForm((prev) => ({ ...prev, nombres: e.target.value }))
                  }
                  placeholder="Ej. María Fernanda"
                  disabled={submitting}
                  maxLength={200}
                  required
                />
              </div>
              <div className={styles.formRow}>
                <label className={styles.formLabel} htmlFor="apellidos">
                  Apellidos
                </label>
                <input
                  id="apellidos"
                  type="text"
                  className="form-input"
                  value={form.apellidos}
                  onChange={(e) =>
                    setForm((prev) => ({ ...prev, apellidos: e.target.value }))
                  }
                  placeholder="Ej. Pérez Torres"
                  disabled={submitting}
                  maxLength={200}
                  required
                />
              </div>
            </div>

            <div className={styles.twoCols}>
              <div className={styles.formRow}>
                <label className={styles.formLabel} htmlFor="identificacion">
                  Identificación
                </label>
                <input
                  id="identificacion"
                  type="text"
                  className="form-input"
                  value={form.identificacion}
                  onChange={(e) =>
                    setForm((prev) => ({
                      ...prev,
                      identificacion: e.target.value,
                    }))
                  }
                  placeholder="Cédula o pasaporte"
                  disabled={submitting}
                  maxLength={50}
                  required
                />
              </div>
              <div className={styles.formRow}>
                <label className={styles.formLabel} htmlFor="area">
                  Área
                </label>
                <select
                  id="area"
                  className="form-input"
                  value={form.areaId}
                  onChange={(e) =>
                    setForm((prev) => ({ ...prev, areaId: e.target.value }))
                  }
                  disabled={submitting || areas.length === 0}
                  required
                >
                  <option value="">Selecciona un área</option>
                  {areas.map((area) => (
                    <option key={area.id} value={area.id}>
                      {area.nombre}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className={styles.formRow}>
              <label className={styles.formLabel} htmlFor="emailUsuario">
                Correo
              </label>
              <EmailConSufijo
                id="emailUsuario"
                name="emailUsuario"
                value={form.emailUsuario}
                onChange={(value) =>
                  setForm((prev) => ({ ...prev, emailUsuario: value }))
                }
                placeholder="nombre.apellido"
                disabled={submitting}
                required
              />
            </div>

            <div className={styles.formRow}>
              <label className={styles.formLabel}>Firma</label>
              <SignaturePad
                value={form.firma}
                onChange={(dataUrl) =>
                  setForm((prev) => ({ ...prev, firma: dataUrl }))
                }
                width={400}
                height={150}
                disabled={submitting}
              />
            </div>

            {formError && (
              <div
                className={`${styles.alert} ${styles.alertError}`}
                role="alert"
                style={{ marginTop: 12 }}
              >
                {formError}
              </div>
            )}

            <div className={styles.actions}>
              <button
                type="submit"
                className="btn btn--primary"
                disabled={submitting}
              >
                {submitting ? (
                  <Spinner size={16} label="Enviando..." />
                ) : (
                  <Send width={16} height={16} />
                )}
                <span>{submitting ? 'Enviando...' : 'Inscribirme'}</span>
              </button>
            </div>
          </form>
        </section>
      </div>
    </div>
  );
}

function EstadoBadge({ estado }) {
  switch (estado) {
    case 'Iniciada':
      return (
        <span className={`${styles.badge} ${styles.badgeStarted}`}>Iniciada</span>
      );
    case 'Finalizada':
      return (
        <span className={`${styles.badge} ${styles.badgeFinished}`}>
          Finalizada
        </span>
      );
    case 'Inscripciones Abiertas':
    default:
      return (
        <span className={`${styles.badge} ${styles.badgeOpen}`}>
          Inscripciones Abiertas
        </span>
      );
  }
}
