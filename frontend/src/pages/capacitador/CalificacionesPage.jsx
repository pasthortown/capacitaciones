import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import Swal from 'sweetalert2';
import { Award } from 'lucide-react';
import DataTable from '../../components/Table/DataTable.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { HttpError } from '../../services/http.js';
import { getCalificaciones, calificarAsistente } from '../../services/calificaciones.js';
import { formatFechaHora, formatDuracion } from '../../utils/formatters.js';
import styles from './CalificacionesPage.module.css';

/**
 * Pantalla pública (sin sidebar ni guard admin) — Calificaciones (Fase 11).
 *
 * Flujo:
 *  - Lee `?token=...` del querystring.
 *  - GET /capacitador/calificaciones con ese Bearer → hidrata capacitación + asistentes Presentes.
 *  - Input numérico por asistente (0–10 step 0.1). Al blur / Enter dispara PUT.
 *  - El umbral de aprobación (`puntajeMinimo`) pinta el background verde/rojo y
 *    el badge "Aprobado/No aprobado".
 *  - Optimistic update con rollback al fallar.
 *
 * Aislamiento: `calificaciones.js` usa fetch directo (no http.js), por eso un 401
 * aquí NO dispara `auth:expired` del admin.
 */
export default function CalificacionesPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [capacitacion, setCapacitacion] = useState(null);
  const [asistentes, setAsistentes] = useState([]);
  const [savingId, setSavingId] = useState(null);

  const puntajeMinimo = capacitacion?.puntajeMinimo ?? null;

  const aprobados = useMemo(
    () =>
      asistentes.filter(
        (a) =>
          a?.calificacion != null &&
          puntajeMinimo != null &&
          Number(a.calificacion) >= Number(puntajeMinimo),
      ).length,
    [asistentes, puntajeMinimo],
  );

  const calificados = useMemo(
    () => asistentes.filter((a) => a?.calificacion != null).length,
    [asistentes],
  );

  const mapError = useCallback((err) => {
    if (err instanceof HttpError) {
      if (err.status === 401 || err.status === 403) {
        return 'Enlace inválido o expirado. Solicita uno nuevo al administrador.';
      }
      if (err.status === 404) {
        return 'Capacitación no encontrada.';
      }
      if (err.status === 409 && err.body?.error === 'CALIFICACIONES_NO_APLICA') {
        return 'Esta capacitación es de Participación, no admite calificaciones.';
      }
    }
    return err?.message || 'Ocurrió un error inesperado.';
  }, []);

  useEffect(() => {
    if (!token) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError('');
    getCalificaciones(token)
      .then((dto) => {
        if (cancelled) return;
        setCapacitacion(dto?.capacitacion ?? null);
        const list = Array.isArray(dto?.asistentes) ? dto.asistentes : [];
        setAsistentes(list);
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
  }, [token, mapError]);

  /**
   * Persiste una calificación (o `null` para limpiar) con optimistic update.
   * Validaciones client-side: rango 0..10, step 0.1; fuera de eso → toast + rollback.
   */
  const handleSaveCalificacion = async (asistente, rawValue) => {
    if (!asistente?.id) return;
    const previous = asistente?.calificacion ?? null;

    // Interpretar vacío como null (limpiar).
    const trimmed = typeof rawValue === 'string' ? rawValue.trim() : rawValue;
    let nextValue = null;
    if (trimmed !== '' && trimmed !== null && trimmed !== undefined) {
      const parsed = Number(trimmed);
      if (!Number.isFinite(parsed) || parsed < 0 || parsed > 10) {
        await Swal.fire({
          icon: 'error',
          title: 'Calificación inválida',
          text: 'La calificación debe estar entre 0 y 10.',
          confirmButtonText: 'Entendido',
        });
        // Revertir UI al valor anterior.
        setAsistentes((prev) =>
          prev.map((a) =>
            a?.id === asistente.id ? { ...a, calificacion: previous } : a,
          ),
        );
        return;
      }
      // Redondeo a 1 decimal para respetar step=0.1.
      nextValue = Math.round(parsed * 10) / 10;
    }

    // No-op si no cambia.
    const prevNum = previous == null ? null : Number(previous);
    if (prevNum === nextValue) return;

    setSavingId(asistente.id);
    // Optimistic update.
    setAsistentes((prev) =>
      prev.map((a) =>
        a?.id === asistente.id ? { ...a, calificacion: nextValue } : a,
      ),
    );

    try {
      const resp = await calificarAsistente(token, asistente.id, nextValue);
      setAsistentes((prev) =>
        prev.map((a) =>
          a?.id === asistente.id
            ? { ...a, calificacion: resp?.calificacion ?? nextValue }
            : a,
        ),
      );
    } catch (err) {
      // Rollback.
      setAsistentes((prev) =>
        prev.map((a) =>
          a?.id === asistente.id ? { ...a, calificacion: previous } : a,
        ),
      );
      Swal.fire({
        icon: 'error',
        title: 'No se pudo guardar',
        text: mapError(err),
        confirmButtonText: 'Entendido',
      });
    } finally {
      setSavingId(null);
    }
  };

  const columns = useMemo(
    () => [
      { key: 'apellidos', header: 'Apellidos' },
      { key: 'nombres', header: 'Nombres' },
      { key: 'identificacion', header: 'Identificación' },
      {
        key: 'calificacion',
        header: 'Calificación',
        accessor: (row) => (
          <CalificacionCell
            row={row}
            puntajeMinimo={puntajeMinimo}
            saving={savingId === row?.id}
            onSave={(v) => handleSaveCalificacion(row, v)}
          />
        ),
      },
      {
        key: 'estado',
        header: 'Estado',
        accessor: (row) => (
          <EstadoCalificacionBadge
            calificacion={row?.calificacion ?? null}
            puntajeMinimo={puntajeMinimo}
          />
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [puntajeMinimo, savingId],
  );

  // --- Render: sin token ---
  if (!token) {
    return (
      <div className={styles.page}>
        <div className={`${styles.fullMessage} ${styles.alert} ${styles.alertError}`}>
          Enlace inválido. Solicita al administrador un enlace de calificaciones.
        </div>
      </div>
    );
  }

  // --- Render: cargando ---
  if (loading) {
    return (
      <div className={styles.page}>
        <div className={styles.loadingWrap}>
          <Spinner size={36} label="Cargando calificaciones..." />
        </div>
      </div>
    );
  }

  // --- Render: error de carga ---
  if (error) {
    return (
      <div className={styles.page}>
        <div className={`${styles.fullMessage} ${styles.alert} ${styles.alertError}`}>
          {error}
        </div>
      </div>
    );
  }

  // --- Render: sin asistentes (todos ausentes / nadie marcado) ---
  if (!asistentes || asistentes.length === 0) {
    return (
      <div className={styles.page}>
        <Header capacitacion={capacitacion} />
        <div className={`${styles.fullMessage} ${styles.alert} ${styles.alertInfo}`}>
          No hay asistentes Presentes para calificar. Completa primero el pase de lista.
        </div>
      </div>
    );
  }

  // --- Render: tabla de calificaciones ---
  return (
    <div className={styles.page}>
      <Header capacitacion={capacitacion} />

      <div className={styles.container}>
        <div className={styles.summary}>
          <span>
            <strong>{asistentes.length}</strong> asistente
            {asistentes.length === 1 ? '' : 's'} Presente
            {asistentes.length === 1 ? '' : 's'} · <strong>{calificados}</strong>{' '}
            calificado{calificados === 1 ? '' : 's'}
            {puntajeMinimo != null && (
              <>
                {' '}· <strong>{aprobados}</strong> aprobado
                {aprobados === 1 ? '' : 's'}
              </>
            )}
          </span>
          {puntajeMinimo != null ? (
            <span className={styles.puntajeBadge}>
              <Award width={16} height={16} aria-hidden="true" />
              Puntaje mínimo: {formatPuntaje(puntajeMinimo)}
            </span>
          ) : (
            <span className={`${styles.alert} ${styles.alertWarning}`}>
              Esta capacitación no tiene puntaje mínimo definido.
            </span>
          )}
        </div>

        <div className={styles.tableWrap}>
          <DataTable
            columns={columns}
            rows={asistentes}
            rowKey={(row) => row?.id}
            emptyMessage="No hay asistentes Presentes."
          />
        </div>
      </div>
    </div>
  );
}

/**
 * Celda editable de calificación. Guarda on blur / on Enter.
 * Resalta verde o rojo según el puntaje mínimo.
 */
function CalificacionCell({ row, puntajeMinimo, saving, onSave }) {
  const initial = row?.calificacion == null ? '' : String(row.calificacion);
  const [value, setValue] = useState(initial);

  // Sincroniza cuando el padre muta la fila (por ejemplo, rollback tras fallo).
  useEffect(() => {
    setValue(row?.calificacion == null ? '' : String(row.calificacion));
  }, [row?.calificacion]);

  const commit = () => {
    onSave(value);
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      e.currentTarget.blur();
    }
  };

  const numeric = row?.calificacion == null ? null : Number(row.calificacion);
  const cellClass =
    numeric == null || puntajeMinimo == null
      ? styles.calificacionCell
      : numeric >= Number(puntajeMinimo)
        ? `${styles.calificacionCell} ${styles.calificacionCellPass}`
        : `${styles.calificacionCell} ${styles.calificacionCellFail}`;

  return (
    <span className={cellClass}>
      <input
        type="number"
        min="0"
        max="10"
        step="0.1"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onBlur={commit}
        onKeyDown={handleKeyDown}
        disabled={saving}
        className={styles.calificacionInput}
        aria-label={`Calificación de ${row?.nombres ?? ''} ${row?.apellidos ?? ''}`}
      />
      {saving && <Spinner size={14} label="Guardando..." />}
    </span>
  );
}

/**
 * Badge textual: Aprobado / No aprobado / Sin calificar.
 */
function EstadoCalificacionBadge({ calificacion, puntajeMinimo }) {
  if (calificacion == null) {
    return <span className={`${styles.statusBadge} ${styles.statusNeutral}`}>Sin calificar</span>;
  }
  if (puntajeMinimo == null) {
    return <span className={`${styles.statusBadge} ${styles.statusNeutral}`}>—</span>;
  }
  return Number(calificacion) >= Number(puntajeMinimo) ? (
    <span className={`${styles.statusBadge} ${styles.statusPass}`}>Aprobado</span>
  ) : (
    <span className={`${styles.statusBadge} ${styles.statusFail}`}>No aprobado</span>
  );
}

function Header({ capacitacion }) {
  return (
    <header className={styles.header}>
      <h1 className={styles.title}>Calificaciones</h1>
      <p className={styles.subtitle}>
        {capacitacion?.codigo ? `${capacitacion.codigo} · ` : ''}
        {capacitacion?.tema || ''}
      </p>
      {capacitacion && (
        <p className={styles.subtitleMeta}>
          {formatFechaHora(capacitacion.fechaHoraInicio) || '—'}
          {' · '}
          {formatDuracion(capacitacion.duracionMinutos)}
          {capacitacion.estado ? ` · ${capacitacion.estado}` : ''}
        </p>
      )}
    </header>
  );
}

/**
 * Formatea el puntaje mínimo a 1 decimal ("8" → "8.0").
 */
function formatPuntaje(p) {
  const n = Number(p);
  if (!Number.isFinite(n)) return '—';
  return n.toFixed(1);
}
