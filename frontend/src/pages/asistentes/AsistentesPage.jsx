import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, ClipboardList, FileBadge, FileCheck2 } from 'lucide-react';
import DataTable from '../../components/Table/DataTable.jsx';
import Modal from '../../components/Modal/Modal.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import AttendanceToggle from '../../components/AttendanceToggle/AttendanceToggle.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import {
  getCapacitacion,
  generarYEnviarCertificados,
} from '../../services/capacitaciones.js';
import {
  listByCapacitacion,
  descargarCertificado,
  descargarReporteAsistencia,
  marcarAsistenciaAdmin,
  calificarAsistenteAdmin,
} from '../../services/asistentes.js';
import { formatFechaHora } from '../../utils/formatters.js';
import styles from './AsistentesPage.module.css';

/**
 * Vista admin: listado de asistentes inscritos a una capacitación.
 *
 * Flujo:
 *  - `useParams` para obtener el id de la capacitación.
 *  - Al montar: carga en paralelo detalle de la capacitación + listado de asistentes.
 *  - Acción individual "Descargar certificado" por fila (habilitada sólo si la
 *    capacitación está en estado "Finalizada").
 *  - Acción global "Generar todos los certificados" (habilitada sólo si la
 *    capacitación está Finalizada). Muestra resumen modal si hay errores parciales.
 *
 * Errores contemplados del backend (Fase 6 — ver instrucciones.md §7.2.5 y §7.2.6):
 *   409 CAPACITACION_NO_FINALIZADA  → toast error
 *   409 FIRMAS_FALTANTES            → modal con lista de faltantes (legibilidad)
 *   503 SERVICIO_EMISOR_NO_DISPONIBLE → toast error ("intenta en unos minutos")
 *   404                              → toast error genérico
 */
export default function AsistentesPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const toast = useToast();

  const [loading, setLoading] = useState(true);
  const [capacitacion, setCapacitacion] = useState(null);
  const [asistentes, setAsistentes] = useState([]);
  const [downloadingId, setDownloadingId] = useState(null);

  // Generación en lote (envío en segundo plano: el avance por asistente se
  // refleja en la columna "Certificado" vía polling del listado).
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [generating, setGenerating] = useState(false);

  // Reporte de asistencia (PDF)
  const [descargandoReporte, setDescargandoReporte] = useState(false);

  // Modal informativo para "firmas faltantes"
  const [firmasFaltantes, setFirmasFaltantes] = useState(null);
  // { asistente: string|null, faltantes: string[], mensaje: string }

  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const fetchAll = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      const [cap, list] = await Promise.all([
        getCapacitacion(id),
        listByCapacitacion(id),
      ]);
      if (!mountedRef.current) return;
      setCapacitacion(cap || null);
      setAsistentes(Array.isArray(list) ? list : []);
    } catch (err) {
      if (!mountedRef.current) return;
      toast.error(err?.message || 'No se pudieron cargar los asistentes.');
      setAsistentes([]);
    } finally {
      if (mountedRef.current) setLoading(false);
    }
  }, [id, toast]);

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  const handleVolver = () => {
    navigate('/capacitaciones');
  };

  // Refresca el listado para reflejar el avance del envío en segundo plano
  // (Pendiente → Enviado / Error en la columna "Certificado"). Se detiene
  // cuando ya no quedan pendientes o tras un número acotado de intentos.
  const pollEstadoEnvio = useCallback(async () => {
    const MAX_INTENTOS = 15;
    for (let intento = 0; intento < MAX_INTENTOS; intento += 1) {
      // Espera entre refrescos (el primero también espera, para dar tiempo al
      // worker a procesar el primer asistente).
      await new Promise((resolve) => setTimeout(resolve, 4000));
      if (!mountedRef.current) return;
      let list = [];
      try {
        list = await listByCapacitacion(id);
      } catch {
        continue; // un refresco fallido no aborta el seguimiento
      }
      if (!mountedRef.current) return;
      if (Array.isArray(list)) setAsistentes(list);
      const quedanPendientes = Array.isArray(list)
        && list.some((a) => a?.estadoEnvioCertificado === 'Pendiente');
      if (!quedanPendientes) return;
    }
  }, [id]);

  const handleDescargar = async (asistente) => {
    if (!asistente?.id || downloadingId) return;
    setDownloadingId(asistente.id);
    try {
      const codigo = capacitacion?.codigo || 'certificado';
      const identificacion = asistente.identificacion || asistente.id;
      const fallback = `${codigo}_${identificacion}.pdf`;
      await descargarCertificado(id, asistente.id, fallback);
      toast.success('Certificado descargado.');
    } catch (err) {
      handleCertificadoError(err, asistente);
    } finally {
      if (mountedRef.current) setDownloadingId(null);
    }
  };

  const handleCertificadoError = (err, asistente) => {
    if (err instanceof HttpError) {
      const code = err.body?.error;
      if (err.status === 409 && code === 'FIRMAS_FALTANTES') {
        const faltantes = Array.isArray(err.body?.faltantes)
          ? err.body.faltantes
          : [];
        setFirmasFaltantes({
          asistente: asistente
            ? `${asistente.nombres ?? ''} ${asistente.apellidos ?? ''}`.trim()
            : null,
          faltantes,
          mensaje:
            err.message ||
            'No se puede emitir el certificado: faltan firmas.',
        });
        return;
      }
      if (err.status === 409 && code === 'CAPACITACION_NO_FINALIZADA') {
        toast.error('La capacitación aún no está finalizada.');
        return;
      }
      if (err.status === 409 && code === 'ASISTENTE_NO_ELEGIBLE_CERTIFICADO') {
        // Fase 12: diferenciamos por motivo para que el mensaje sea específico.
        const motivo = err.body?.motivo;
        const msg =
          motivo === 'AUSENTE'
            ? 'El asistente está marcado como ausente — no recibe certificado.'
            : motivo === 'SIN_MARCAR'
            ? 'El asistente no fue marcado en el pase de lista.'
            : err.message || 'El asistente no es elegible para certificado.';
        toast.error(msg);
        return;
      }
      if (err.status === 503 && code === 'SERVICIO_EMISOR_NO_DISPONIBLE') {
        toast.error(
          'El servicio de emisión no está disponible. Intenta en unos minutos.',
        );
        return;
      }
      if (err.status === 404) {
        toast.error('Asistente o capacitación no encontrados.');
        return;
      }
      toast.error(err.message || 'No se pudo descargar el certificado.');
      return;
    }
    toast.error(err?.message || 'No se pudo descargar el certificado.');
  };

  const handleGenerarLote = () => {
    setConfirmOpen(true);
  };

  const handleDescargarReporte = async () => {
    if (!id || descargandoReporte) return;
    setDescargandoReporte(true);
    try {
      const fallback = capacitacion?.codigo
        ? `Reporte_Asistencia_${capacitacion.codigo}.pdf`
        : 'Reporte_Asistencia.pdf';
      await descargarReporteAsistencia(id, fallback);
      toast.success('Reporte de asistencia descargado y enviado por correo.');
    } catch (err) {
      if (err instanceof HttpError) {
        if (err.status === 503) {
          toast.error('El servicio de emisión no está disponible. Intenta en unos minutos.');
        } else if (err.status === 404) {
          toast.error('Capacitación no encontrada.');
        } else {
          toast.error(err.message || 'No se pudo descargar el reporte.');
        }
      } else {
        toast.error(err?.message || 'No se pudo descargar el reporte.');
      }
    } finally {
      if (mountedRef.current) setDescargandoReporte(false);
    }
  };

  const confirmGenerarLote = async () => {
    setGenerating(true);
    try {
      // El backend encola el envío y responde 202 con { estado, pendientes, message }.
      // El resultado por asistente NO viene en esta respuesta: se sigue en la
      // columna "Certificado" (Pendiente → Enviado / Error) vía pollEstadoEnvio.
      const resp = await generarYEnviarCertificados(id);
      if (!mountedRef.current) return;
      setConfirmOpen(false);
      const pendientes = Number(resp?.pendientes ?? 0);
      if (pendientes > 0) {
        toast.success(
          resp?.message ||
            `Se están generando y enviando ${pendientes} certificado(s) en segundo plano.`,
        );
        await fetchAll(); // muestra de inmediato los badges "Pendiente"
        await pollEstadoEnvio(); // y los actualiza conforme el worker avanza
      } else {
        // Sin elegibles (todos ausentes o sin marcar en el pase de lista).
        toast.info(
          resp?.message ||
            'No hay asistentes elegibles para certificado (ausentes o sin marcar en el pase de lista).',
        );
      }
    } catch (err) {
      if (!mountedRef.current) return;
      setConfirmOpen(false);
      if (err instanceof HttpError) {
        const code = err.body?.error;
        if (err.status === 409 && code === 'CAPACITACION_NO_FINALIZADA') {
          toast.error('La capacitación aún no está finalizada.');
        } else if (err.status === 503) {
          toast.error(
            'El servicio de emisión no está disponible. Intenta en unos minutos.',
          );
        } else {
          toast.error(err.message || 'No se pudieron generar los certificados.');
        }
      } else {
        toast.error(
          err?.message || 'No se pudieron generar los certificados.',
        );
      }
    } finally {
      if (mountedRef.current) setGenerating(false);
    }
  };

  const esFinalizada = capacitacion?.estado === 'Finalizada';
  // Bandera global del evento: si está en false, ocultamos toda la maquinaria
  // de certificados (botón de lote y acción individual de descarga).
  // Default true cuando el backend no manda el campo (capacitaciones legacy).
  const emiteCertificado = capacitacion?.emiteCertificado !== false;

  /**
   * Corrige la asistencia de un asistente desde la tabla admin (Fase 10).
   * Optimistic update: si falla el PUT, revierte y muestra toast.
   */
  const handleMarcarAsistencia = async (row, nuevoValor) => {
    if (!row?.id || !id) return;
    const previous = row?.estadoAsistencia ?? null;
    if (previous === nuevoValor) return;

    // Optimistic update.
    setAsistentes((prev) =>
      prev.map((a) =>
        a?.id === row.id ? { ...a, estadoAsistencia: nuevoValor } : a,
      ),
    );

    try {
      const resp = await marcarAsistenciaAdmin(id, row.id, nuevoValor);
      setAsistentes((prev) =>
        prev.map((a) =>
          a?.id === row.id
            ? {
                ...a,
                estadoAsistencia: resp?.estadoAsistencia ?? nuevoValor,
                fechaMarcacionAsistencia:
                  resp?.fechaMarcacionAsistencia ?? a.fechaMarcacionAsistencia,
              }
            : a,
        ),
      );
    } catch (err) {
      // Revertir al valor anterior.
      setAsistentes((prev) =>
        prev.map((a) =>
          a?.id === row.id ? { ...a, estadoAsistencia: previous } : a,
        ),
      );
      toast.error(err?.message || 'No se pudo actualizar la asistencia.');
    }
  };

  /**
   * Persiste la calificación de un asistente (Fase 11). Sólo habilitado si el
   * asistente está `Presente`. Optimistic update con rollback ante error,
   * y validación client-side de rango (0..10, step 0.1).
   */
  const handleCalificar = async (row, rawValue) => {
    if (!row?.id || !id) return;
    const previous = row?.calificacion ?? null;

    // Interpretar vacío como null (limpiar).
    const trimmed = typeof rawValue === 'string' ? rawValue.trim() : rawValue;
    let nextValue = null;
    if (trimmed !== '' && trimmed !== null && trimmed !== undefined) {
      const parsed = Number(trimmed);
      if (!Number.isFinite(parsed) || parsed < 0 || parsed > 10) {
        toast.error('La calificación debe estar entre 0 y 10.');
        // Revertir UI (por si el input aún muestra el valor fuera de rango).
        setAsistentes((prev) =>
          prev.map((a) =>
            a?.id === row.id ? { ...a, calificacion: previous } : a,
          ),
        );
        return;
      }
      nextValue = Math.round(parsed * 10) / 10;
    }

    const prevNum = previous == null ? null : Number(previous);
    if (prevNum === nextValue) return;

    // Optimistic update.
    setAsistentes((prev) =>
      prev.map((a) =>
        a?.id === row.id ? { ...a, calificacion: nextValue } : a,
      ),
    );

    try {
      const resp = await calificarAsistenteAdmin(id, row.id, nextValue);
      setAsistentes((prev) =>
        prev.map((a) =>
          a?.id === row.id
            ? { ...a, calificacion: resp?.calificacion ?? nextValue }
            : a,
        ),
      );
    } catch (err) {
      // Rollback.
      setAsistentes((prev) =>
        prev.map((a) =>
          a?.id === row.id ? { ...a, calificacion: previous } : a,
        ),
      );
      if (err instanceof HttpError) {
        const code = err.body?.error;
        if (err.status === 409 && code === 'ASISTENTE_NO_PRESENTE') {
          toast.error('Sólo se puede calificar a asistentes Presentes.');
          return;
        }
        if (err.status === 400 && code === 'CALIFICACION_FUERA_DE_RANGO') {
          toast.error('La calificación debe estar entre 0 y 10.');
          return;
        }
        if (err.status === 409 && code === 'CALIFICACIONES_NO_APLICA') {
          toast.error('Esta capacitación es de Participación; no admite calificaciones.');
          return;
        }
      }
      toast.error(err?.message || 'No se pudo guardar la calificación.');
    }
  };

  const esAprobacion = capacitacion?.tipoCertificacion === 'Aprobacion';
  const puntajeMinimo = capacitacion?.puntajeMinimo ?? null;

  // Pinta el estado del envío del certificado como badge. `null` => aún no se
  // disparó el envío (o el asistente no es elegible: ausente / sin marcar).
  const renderEstadoEnvio = (row) => {
    const estado = row?.estadoEnvioCertificado ?? null;
    if (estado === 'Enviado') {
      const fecha = formatFechaHora(row?.fechaEnvioCertificado);
      return (
        <span
          className={`${styles.badge} ${styles.badgeCertSent}`}
          title={fecha ? `Enviado el ${fecha}` : undefined}
        >
          Enviado
        </span>
      );
    }
    if (estado === 'Pendiente') {
      return (
        <span className={`${styles.badge} ${styles.badgeCertPending}`}>
          Pendiente
        </span>
      );
    }
    if (estado === 'Error') {
      return (
        <span
          className={`${styles.badge} ${styles.badgeCertError}`}
          title={row?.mensajeErrorEnvio || 'Error en el envío'}
        >
          Error
        </span>
      );
    }
    return '—';
  };

  const columns = [
    {
      key: 'asistencia',
      header: 'Asistencia',
      accessor: (row) => (
        <AttendanceToggle
          value={row?.estadoAsistencia ?? null}
          onChange={(v) => handleMarcarAsistencia(row, v)}
          size="sm"
        />
      ),
    },
    // Columna Calificación visible sólo en capacitaciones de Aprobación (Fase 11).
    ...(esAprobacion
      ? [
          {
            key: 'calificacion',
            header: 'Calificación',
            accessor: (row) => (
              <CalificacionCell
                row={row}
                puntajeMinimo={puntajeMinimo}
                onChange={(v) => handleCalificar(row, v)}
              />
            ),
          },
        ]
      : []),
    { key: 'nombres', header: 'Nombres' },
    { key: 'apellidos', header: 'Apellidos' },
    { key: 'identificacion', header: 'Identificación' },
    {
      key: 'email',
      header: 'Email',
      accessor: (row) => row?.email || '—',
    },
    {
      key: 'area',
      header: 'Área',
      accessor: (row) => row?.area?.nombre || '—',
    },
    {
      key: 'fechaInscripcion',
      header: 'Fecha inscripción',
      accessor: (row) => formatFechaHora(row?.fechaInscripcion) || '—',
    },
    // Columna Certificado: estado del envío por correo. Visible sólo si el
    // evento emite certificado. "Enviado" | "Pendiente" | "Error" | sin envío.
    ...(emiteCertificado
      ? [
          {
            key: 'estadoEnvioCertificado',
            header: 'Certificado',
            accessor: (row) => renderEstadoEnvio(row),
          },
        ]
      : []),
  ];

  const renderActions = (row) => {
    // Si el evento no emite certificado, no mostramos la acción de descarga.
    if (!emiteCertificado) {
      return null;
    }
    const isDownloading = downloadingId === row?.id;
    // Fase 12: ausentes o no marcados no son elegibles para certificado; deshabilitamos
    // el botón para evitar un 409 evitable y dejamos claro por qué en el tooltip.
    const esPresente = row?.estadoAsistencia === 'Presente';
    const esAusente = row?.estadoAsistencia === 'Ausente';
    const sinMarcar = !row?.estadoAsistencia;
    const noElegible = esFinalizada && !esPresente;
    const disabled = !esFinalizada || isDownloading || generating || noElegible;

    const titleMsg = !esFinalizada
      ? 'Disponible cuando la capacitación esté Finalizada'
      : esAusente
      ? 'El asistente está ausente — no recibe certificado'
      : sinMarcar
      ? 'Pendiente de marcación en el pase de lista'
      : 'Descargar certificado';

    return (
      <button
        type="button"
        className="btn btn--icon btn--secondary btn--sm"
        onClick={() => handleDescargar(row)}
        disabled={disabled}
        title={titleMsg}
        aria-label={titleMsg}
      >
        {isDownloading ? (
          <Spinner size={16} label="Descargando..." />
        ) : (
          <FileBadge width={18} height={18} />
        )}
      </button>
    );
  };

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Asistentes</h1>
          <p className="page-header__subtitle">
            {capacitacion ? (
              <>
                <span>
                  {capacitacion.codigo ? `${capacitacion.codigo} · ` : ''}
                  {capacitacion.tema || ''}
                </span>
                <span className={styles.badgeSpacer}>
                  <EstadoBadge
                    estado={capacitacion.estado}
                    activo={capacitacion.activo !== false}
                  />
                </span>
              </>
            ) : (
              <>Capacitación: <code>{id}</code></>
            )}
          </p>
        </div>
        <div className={styles.headerActions}>
          <button
            type="button"
            className="btn btn--secondary"
            onClick={handleDescargarReporte}
            disabled={descargandoReporte || generating}
            title="Descargar reporte de asistencia (PDF)"
          >
            {descargandoReporte ? (
              <Spinner size={14} label="Descargando..." />
            ) : (
              <ClipboardList width={16} height={16} />
            )}
            <span>Reporte de asistencia</span>
          </button>
          {esFinalizada && emiteCertificado && (
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleGenerarLote}
              disabled={generating || asistentes.length === 0}
              title={
                asistentes.length === 0
                  ? 'No hay asistentes inscritos'
                  : 'Generar y Enviar todos los certificados'
              }
            >
              {generating ? (
                <Spinner size={14} label="Generando..." />
              ) : (
                <FileCheck2 width={16} height={16} />
              )}
              <span>Generar y Enviar todos los certificados</span>
            </button>
          )}
          <button
            type="button"
            className="btn btn--secondary"
            onClick={handleVolver}
            disabled={generating || descargandoReporte}
          >
            <ArrowLeft width={16} height={16} />
            <span>Volver</span>
          </button>
        </div>
      </div>

      <div className="card">
        <div className="card__body">
          <DataTable
            columns={columns}
            rows={asistentes}
            rowKey={(row) => row?.id}
            actions={renderActions}
            loading={loading}
            emptyMessage="Aún no hay inscritos."
          />
        </div>
      </div>

      {/* Modal confirmación generación en lote */}
      <Modal
        isOpen={confirmOpen}
        onClose={() => !generating && setConfirmOpen(false)}
        title="Generar y Enviar todos los certificados"
        footer={
          <>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setConfirmOpen(false)}
              disabled={generating}
            >
              Cancelar
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={confirmGenerarLote}
              disabled={generating}
            >
              {generating ? 'Generando...' : 'Continuar'}
            </button>
          </>
        }
      >
        <p>
          Se generarán y enviarán por correo los certificados de todos los
          asistentes inscritos a <strong>{capacitacion?.codigo}</strong>
          {capacitacion?.tema ? ` — ${capacitacion.tema}` : ''}.
        </p>
        <p className="text-sm text-secondary">
          El envío se hace en bloques de 5 con pausa entre bloques para no
          saturar el SMTP, así que la operación puede tardar varios segundos
          en cohortes grandes. ¿Deseas continuar?
        </p>
      </Modal>

      {/* Modal informativo: firmas faltantes (individual) */}
      <Modal
        isOpen={Boolean(firmasFaltantes)}
        onClose={() => setFirmasFaltantes(null)}
        title="Firmas pendientes"
        footer={
          <button
            type="button"
            className="btn btn--primary"
            onClick={() => setFirmasFaltantes(null)}
          >
            Entendido
          </button>
        }
      >
        {firmasFaltantes && (
          <>
            <p>
              {firmasFaltantes.mensaje ||
                'No se puede emitir el certificado: faltan firmas.'}
            </p>
            {firmasFaltantes.faltantes.length > 0 && (
              <>
                <p className="text-sm text-secondary">
                  Cargue las firmas faltantes y reintente:
                </p>
                <ul>
                  {firmasFaltantes.faltantes.map((n) => (
                    <li key={n}>{n}</li>
                  ))}
                </ul>
              </>
            )}
          </>
        )}
      </Modal>
    </div>
  );
}

/**
 * Celda editable de calificación para el admin (Fase 11).
 *  - Bloqueada si el asistente no está `Presente` (tooltip explicativo).
 *  - Fondo verde/rojo según supere o no el `puntajeMinimo`.
 *  - Guarda on blur / on Enter.
 */
function CalificacionCell({ row, puntajeMinimo, onChange }) {
  const initial = row?.calificacion == null ? '' : String(row.calificacion);
  const [value, setValue] = useState(initial);

  // Sincroniza el input con el estado de la fila (por rollback o reloads).
  useEffect(() => {
    setValue(row?.calificacion == null ? '' : String(row.calificacion));
  }, [row?.calificacion]);

  const disabled = row?.estadoAsistencia !== 'Presente';

  const commit = () => {
    if (disabled) return;
    onChange(value);
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      e.currentTarget.blur();
    }
  };

  const numeric = row?.calificacion == null ? null : Number(row.calificacion);
  let wrapperClass = styles.calificacionCell;
  if (!disabled && numeric != null && puntajeMinimo != null) {
    wrapperClass =
      numeric >= Number(puntajeMinimo)
        ? `${styles.calificacionCell} ${styles.calificacionCellPass}`
        : `${styles.calificacionCell} ${styles.calificacionCellFail}`;
  }

  return (
    <span
      className={wrapperClass}
      title={
        disabled
          ? 'Sólo se puede calificar a asistentes Presentes.'
          : undefined
      }
    >
      <input
        type="number"
        min="0"
        max="10"
        step="0.1"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onBlur={commit}
        onKeyDown={handleKeyDown}
        disabled={disabled}
        className={styles.calificacionInput}
        aria-label={`Calificación de ${row?.nombres ?? ''} ${row?.apellidos ?? ''}`}
        placeholder={disabled ? '—' : ''}
      />
    </span>
  );
}

function EstadoBadge({ estado, activo }) {
  if (!activo) {
    return <span className={`${styles.badge} ${styles.badgeInactive}`}>Inactiva</span>;
  }
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
