import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import Swal from 'sweetalert2';
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from 'lucide-react';
import AttendanceToggle from '../../components/AttendanceToggle/AttendanceToggle.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { HttpError } from '../../services/http.js';
import { getPaseLista, marcarAsistencia } from '../../services/paseLista.js';
import { formatFechaHora, formatDuracion } from '../../utils/formatters.js';
import styles from './PaseListaPage.module.css';

/**
 * Pantalla pública (sin sidebar ni guard admin) — Pase de lista.
 *
 * Flujo:
 *  - Lee `?token=...` del querystring.
 *  - GET /capacitador/pase-lista con ese Bearer → hidrata capacitación + asistentes.
 *  - Itera asistentes uno por uno (orden alfabético ya garantizado por backend).
 *  - AttendanceToggle marca Presente/Ausente → PUT inmediato, sin avanzar
 *    automáticamente (el usuario controla el ritmo).
 *  - "Anterior"/"Siguiente" navegan. En el último asistente, "Siguiente"
 *    dispara swal2:
 *      - success si todos están marcados.
 *      - info si faltan marcaciones, con CTA "Ir al primero sin marcar".
 *
 * Aislamiento: `paseLista.js` usa fetch directo (no http.js), por eso un 401
 * aquí NO dispara `auth:expired` del admin.
 */
export default function PaseListaPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [capacitacion, setCapacitacion] = useState(null);
  const [asistentes, setAsistentes] = useState([]);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [saving, setSaving] = useState(false);

  const total = asistentes.length;
  const current = asistentes[currentIndex] ?? null;
  const isFirst = currentIndex <= 0;
  const isLast = currentIndex >= total - 1;

  const marcadosCount = useMemo(
    () => asistentes.filter((a) => a?.estadoAsistencia != null).length,
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
    getPaseLista(token)
      .then((dto) => {
        if (cancelled) return;
        setCapacitacion(dto?.capacitacion ?? null);
        const list = Array.isArray(dto?.asistentes) ? dto.asistentes : [];
        setAsistentes(list);
        // Empieza en el primer asistente sin marcar, o en 0 si todos están marcados.
        const firstUnmarked = list.findIndex((a) => a?.estadoAsistencia == null);
        setCurrentIndex(firstUnmarked >= 0 ? firstUnmarked : 0);
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

  const handleMarcar = async (nuevoValor) => {
    if (!current || saving) return;
    if (current.estadoAsistencia === nuevoValor) return; // no-op

    // Optimistic update para respuesta inmediata en el UI.
    const previous = current.estadoAsistencia;
    const asistenteId = current.id;
    setSaving(true);
    setAsistentes((prev) => {
      const next = [...prev];
      next[currentIndex] = { ...next[currentIndex], estadoAsistencia: nuevoValor };
      return next;
    });

    try {
      const updated = await marcarAsistencia(token, asistenteId, nuevoValor);
      setAsistentes((prev) => {
        const next = [...prev];
        // Sólo actualiza la fila del asistente marcado, aunque el índice
        // actual haya cambiado mientras viajaba la petición.
        const idx = next.findIndex((a) => a?.id === asistenteId);
        if (idx >= 0) {
          next[idx] = {
            ...next[idx],
            estadoAsistencia: updated?.estadoAsistencia ?? nuevoValor,
            fechaMarcacionAsistencia:
              updated?.fechaMarcacionAsistencia ?? next[idx].fechaMarcacionAsistencia,
          };
        }
        return next;
      });
    } catch (err) {
      // Revierte en caso de error y avisa.
      setAsistentes((prev) => {
        const next = [...prev];
        const idx = next.findIndex((a) => a?.id === asistenteId);
        if (idx >= 0) {
          next[idx] = { ...next[idx], estadoAsistencia: previous };
        }
        return next;
      });
      Swal.fire({
        icon: 'error',
        title: 'No se pudo guardar',
        text: mapError(err),
        confirmButtonText: 'Entendido',
      });
    } finally {
      setSaving(false);
    }
  };

  const handlePrev = () => {
    if (isFirst) return;
    setCurrentIndex((idx) => Math.max(0, idx - 1));
  };

  // Saltos extremos — no disparan el swal2 de "lista completada" aunque aterricen
  // en el último: ese flujo solo se activa al pulsar "Siguiente" desde el último.
  const handleFirst = () => {
    if (isFirst) return;
    setCurrentIndex(0);
  };

  const handleLast = () => {
    if (isLast || total === 0) return;
    setCurrentIndex(total - 1);
  };

  const handleNext = async () => {
    if (total === 0) return;
    if (!isLast) {
      setCurrentIndex((idx) => Math.min(total - 1, idx + 1));
      return;
    }

    // Último asistente — evaluar estado global.
    const pendientesIdx = asistentes
      .map((a, i) => (a?.estadoAsistencia == null ? i : -1))
      .filter((i) => i >= 0);

    if (pendientesIdx.length === 0) {
      await Swal.fire({
        icon: 'success',
        title: 'Pase de lista completado',
        text: `Se marcó la asistencia de ${total} persona${total === 1 ? '' : 's'}.`,
        confirmButtonText: 'Cerrar',
      });
      // Permanece en el último asistente (no hay loop).
      return;
    }

    const result = await Swal.fire({
      icon: 'info',
      title: 'Quedan asistentes sin marcar',
      text: `Faltan ${pendientesIdx.length} de ${total}. ¿Quieres ir al primero sin marcar?`,
      showCancelButton: true,
      confirmButtonText: 'Ir al primero sin marcar',
      cancelButtonText: 'Cerrar',
    });
    if (result.isConfirmed) {
      setCurrentIndex(pendientesIdx[0]);
    }
  };

  // --- Render: sin token ---
  if (!token) {
    return (
      <div className={styles.page}>
        <div className={`${styles.fullMessage} ${styles.alert} ${styles.alertError}`}>
          Enlace inválido. Solicita al administrador un enlace de pase de lista.
        </div>
      </div>
    );
  }

  // --- Render: cargando ---
  if (loading) {
    return (
      <div className={styles.page}>
        <div className={styles.loadingWrap}>
          <Spinner size={36} label="Cargando pase de lista..." />
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

  // --- Render: sin asistentes ---
  if (total === 0) {
    return (
      <div className={styles.page}>
        <Header capacitacion={capacitacion} />
        <div className={`${styles.fullMessage} ${styles.alert} ${styles.alertInfo}`}>
          Esta capacitación aún no tiene asistentes inscritos.
        </div>
      </div>
    );
  }

  // --- Render: pase de lista activo ---
  const nombreCompleto = `${current?.apellidos ?? ''} ${current?.nombres ?? ''}`.trim() || '—';

  return (
    <div className={styles.page}>
      <Header capacitacion={capacitacion} />

      <div className={styles.container}>
        <div className={styles.progress}>
          <span>
            <strong>{currentIndex + 1}</strong> de <strong>{total}</strong>
          </span>
          <span className={styles.progressMeta}>
            {marcadosCount} marcado{marcadosCount === 1 ? '' : 's'} · {total - marcadosCount} pendiente{total - marcadosCount === 1 ? '' : 's'}
          </span>
        </div>

        <section className={styles.card} aria-label="Asistente actual">
          <h2 className={styles.name}>{nombreCompleto}</h2>
          <p className={styles.identificacion}>
            <span className={styles.idLabel}>Identificación</span>
            <span>{current?.identificacion || '—'}</span>
          </p>

          <div className={styles.toggleWrap}>
            <AttendanceToggle
              value={current?.estadoAsistencia ?? null}
              onChange={handleMarcar}
              disabled={saving}
              size="md"
            />
          </div>
        </section>

        <div className={styles.navRow}>
          <button
            type="button"
            className="btn btn--icon btn--secondary"
            onClick={handleFirst}
            disabled={isFirst || saving}
            title="Ir al primero"
            aria-label="Ir al primer asistente"
          >
            <ChevronsLeft width={18} height={18} />
          </button>
          <button
            type="button"
            className="btn btn--secondary"
            onClick={handlePrev}
            disabled={isFirst || saving}
          >
            <ChevronLeft width={16} height={16} />
            <span>Anterior</span>
          </button>
          <button
            type="button"
            className="btn btn--primary"
            onClick={handleNext}
            disabled={saving}
          >
            <span>{isLast ? 'Finalizar' : 'Siguiente'}</span>
            <ChevronRight width={16} height={16} />
          </button>
          <button
            type="button"
            className="btn btn--icon btn--secondary"
            onClick={handleLast}
            disabled={isLast || saving}
            title="Ir al último"
            aria-label="Ir al último asistente"
          >
            <ChevronsRight width={18} height={18} />
          </button>
        </div>
      </div>
    </div>
  );
}

function Header({ capacitacion }) {
  return (
    <header className={styles.header}>
      <h1 className={styles.title}>Pase de lista</h1>
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
