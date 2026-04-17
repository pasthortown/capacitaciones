import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Save } from 'lucide-react';
import SignaturePad from '../../components/SignaturePad/SignaturePad.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import {
  getCapacitacion,
  updateCapacitacion,
} from '../../services/capacitador.js';
import { formatFechaHora, formatDuracion } from '../../utils/formatters.js';
import styles from './CapacitadorPage.module.css';

/**
 * Pantalla pública (sin sidebar ni guard admin) del capacitador.
 *
 * Flujo:
 *  - Lee `?token=...` del querystring.
 *  - GET /capacitador/capacitacion con ese Bearer → muestra datos read-only
 *    y un formulario para los 4 campos editables (descripcion + firma + cargo + empresa).
 *  - PUT /capacitador/capacitacion al guardar — SIEMPRE envía los 4 campos
 *    (semántica "replace" del backend, ver services/capacitador.js).
 *
 * Aislamiento: el servicio `capacitador.js` usa fetch directo (no http.js),
 * así un 401 del capacitador NO dispara `auth:expired` del admin.
 */
export default function CapacitadorPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const toast = useToast();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [data, setData] = useState(null);
  const [form, setForm] = useState({
    capacitador: '',
    descripcion: '',
    firmaCapacitador: null,
    cargoCapacitador: '',
    empresaCapacitador: '',
  });

  const hydrateFromData = useCallback((dto) => {
    setForm({
      capacitador: dto?.capacitador ?? '',
      descripcion: dto?.descripcion ?? '',
      firmaCapacitador: dto?.firmaCapacitador ?? null,
      cargoCapacitador: dto?.cargoCapacitador ?? '',
      empresaCapacitador: dto?.empresaCapacitador ?? '',
    });
  }, []);

  const mapError = (err) => {
    if (err instanceof HttpError) {
      if (err.status === 401 || err.status === 403) {
        return 'Enlace inválido o expirado. Solicita uno nuevo al administrador.';
      }
      if (err.status === 404) {
        return 'Capacitación no encontrada.';
      }
    }
    return err?.message || 'Ocurrió un error inesperado.';
  };

  useEffect(() => {
    if (!token) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError('');
    getCapacitacion(token)
      .then((dto) => {
        if (cancelled) return;
        setData(dto);
        hydrateFromData(dto);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(mapError(err));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [token, hydrateFromData]);

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (saving || !token) return;
    const nombre = form.capacitador?.trim();
    if (!nombre) {
      const msg = 'El nombre del capacitador es obligatorio.';
      setError(msg);
      toast.error(msg);
      return;
    }
    setSaving(true);
    setError('');
    try {
      const payload = {
        capacitador: nombre,
        descripcion: form.descripcion?.trim() ? form.descripcion : null,
        firmaCapacitador: form.firmaCapacitador || null,
        cargoCapacitador: form.cargoCapacitador?.trim() ? form.cargoCapacitador : null,
        empresaCapacitador: form.empresaCapacitador?.trim()
          ? form.empresaCapacitador
          : null,
      };
      const updated = await updateCapacitacion(token, payload);
      setData(updated);
      hydrateFromData(updated);
      toast.success('Datos guardados');
    } catch (err) {
      const message = mapError(err);
      setError(message);
      toast.error(message);
    } finally {
      setSaving(false);
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

  // --- Render: error de carga (sin data) ---
  if (error && !data) {
    return (
      <div className={styles.page}>
        <div className={`${styles.fullMessage} ${styles.alert} ${styles.alertError}`}>
          {error}
        </div>
      </div>
    );
  }

  // --- Render: OK, con data ---
  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Capacitador</h1>
        <p className={styles.subtitle}>
          {data?.codigo ? `${data.codigo} · ` : ''}
          {data?.tema || ''}
        </p>
      </header>

      <div className={styles.container}>
        {/* Card 1: datos read-only */}
        <section className={styles.card} aria-label="Datos de la capacitación">
          <h2 className={styles.sectionTitle}>Datos de la capacitación</h2>
          <div className={styles.grid}>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Código</span>
              <span className={styles.fieldValue}>{data?.codigo || '—'}</span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Tema</span>
              <span className={styles.fieldValue}>{data?.tema || '—'}</span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Fecha/hora inicio</span>
              <span className={styles.fieldValue}>
                {formatFechaHora(data?.fechaHoraInicio) || '—'}
              </span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Duración</span>
              <span className={styles.fieldValue}>
                {formatDuracion(data?.duracionMinutos)}
              </span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Modalidad</span>
              <span className={styles.fieldValue}>
                {data?.modalidad?.nombre || '—'}
              </span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Tipo actividad</span>
              <span className={styles.fieldValue}>
                {data?.tipoActividad?.nombre || '—'}
              </span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Tipo certificación</span>
              <span className={styles.fieldValue}>
                {data?.tipoCertificacion || '—'}
              </span>
            </div>
            <div className={styles.field}>
              <span className={styles.fieldLabel}>Estado</span>
              <span className={styles.fieldValue}>
                <EstadoBadge estado={data?.estado} />
              </span>
            </div>
          </div>
        </section>

        {/* Card 2: formulario editable */}
        <section className={styles.card} aria-label="Datos del capacitador">
          <h2 className={styles.sectionTitle}>Mis datos</h2>
          <form onSubmit={handleSubmit} noValidate>
            <div className={styles.formRow}>
              <label className={styles.formLabel} htmlFor="capacitador-nombre">
                Nombre del capacitador
              </label>
              <input
                id="capacitador-nombre"
                type="text"
                className="form-input"
                value={form.capacitador}
                onChange={(e) =>
                  setForm((prev) => ({ ...prev, capacitador: e.target.value }))
                }
                placeholder="Ej. Luis Salazar"
                disabled={saving}
                required
                maxLength={255}
              />
            </div>

            <div className={styles.formRow}>
              <label className={styles.formLabel} htmlFor="descripcion">
                Descripción
              </label>
              <textarea
                id="descripcion"
                className={styles.textarea}
                value={form.descripcion}
                onChange={(e) =>
                  setForm((prev) => ({ ...prev, descripcion: e.target.value }))
                }
                placeholder="Describe brevemente el contenido cubierto."
                disabled={saving}
                maxLength={2000}
              />
            </div>

            <div className={styles.formRow}>
              <label className={styles.formLabel}>Firma</label>
              <SignaturePad
                value={form.firmaCapacitador}
                onChange={(dataUrl) =>
                  setForm((prev) => ({ ...prev, firmaCapacitador: dataUrl }))
                }
                width={400}
                height={150}
                disabled={saving}
              />
            </div>

            <div className={styles.twoCols}>
              <div className={styles.formRow}>
                <label className={styles.formLabel} htmlFor="cargo">
                  Cargo
                </label>
                <input
                  id="cargo"
                  type="text"
                  className="form-input"
                  value={form.cargoCapacitador}
                  onChange={(e) =>
                    setForm((prev) => ({
                      ...prev,
                      cargoCapacitador: e.target.value,
                    }))
                  }
                  placeholder="Ej. Jefe de Seguridad Industrial"
                  disabled={saving}
                  maxLength={200}
                />
              </div>
              <div className={styles.formRow}>
                <label className={styles.formLabel} htmlFor="empresa">
                  Empresa
                </label>
                <input
                  id="empresa"
                  type="text"
                  className="form-input"
                  value={form.empresaCapacitador}
                  onChange={(e) =>
                    setForm((prev) => ({
                      ...prev,
                      empresaCapacitador: e.target.value,
                    }))
                  }
                  placeholder="Ej. DOS S.A."
                  disabled={saving}
                  maxLength={200}
                />
              </div>
            </div>

            {error && (
              <div
                className={`${styles.alert} ${styles.alertError}`}
                role="alert"
                style={{ marginTop: 12 }}
              >
                {error}
              </div>
            )}

            <div className={styles.actions}>
              <button
                type="submit"
                className="btn btn--primary"
                disabled={saving}
              >
                {saving ? (
                  <Spinner size={16} label="Guardando..." />
                ) : (
                  <Save width={16} height={16} />
                )}
                <span>{saving ? 'Guardando...' : 'Guardar'}</span>
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
      return <span className={`${styles.badge} ${styles.badgeStarted}`}>Iniciada</span>;
    case 'Finalizada':
      return (
        <span className={`${styles.badge} ${styles.badgeFinished}`}>Finalizada</span>
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
