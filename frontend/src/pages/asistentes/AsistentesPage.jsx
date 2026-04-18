import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Download, FileCheck2 } from 'lucide-react';
import DataTable from '../../components/Table/DataTable.jsx';
import Modal from '../../components/Modal/Modal.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import {
  getCapacitacion,
  generarCertificados,
} from '../../services/capacitaciones.js';
import {
  listByCapacitacion,
  descargarCertificado,
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

  // Generación en lote
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [loteResult, setLoteResult] = useState(null); // { total, emitidos, errores }

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

  // Índice rápido id → asistente para resolver nombres en el resumen del lote.
  const asistentesById = useMemo(() => {
    const map = new Map();
    for (const a of asistentes) {
      if (a?.id) map.set(a.id, a);
    }
    return map;
  }, [asistentes]);

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

  const confirmGenerarLote = async () => {
    setGenerating(true);
    try {
      const resp = await generarCertificados(id);
      if (!mountedRef.current) return;
      const total = Number(resp?.total ?? 0);
      const emitidos = Number(resp?.emitidos ?? 0);
      const errores = Array.isArray(resp?.errores) ? resp.errores : [];
      setConfirmOpen(false);
      if (errores.length === 0 && emitidos === total) {
        toast.success(`Se emitieron ${total} certificados.`);
        setLoteResult(null);
      } else {
        // Hay errores parciales — abrir modal con el resumen.
        setLoteResult({ total, emitidos, errores });
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

  const columns = [
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
  ];

  const renderActions = (row) => {
    const isDownloading = downloadingId === row?.id;
    const disabled = !esFinalizada || isDownloading || generating;
    return (
      <button
        type="button"
        className="btn btn--secondary btn--sm"
        onClick={() => handleDescargar(row)}
        disabled={disabled}
        title={
          esFinalizada
            ? 'Descargar certificado'
            : 'Disponible cuando la capacitación esté Finalizada'
        }
      >
        {isDownloading ? (
          <Spinner size={14} label="Descargando..." />
        ) : (
          <Download width={14} height={14} />
        )}
        <span>Certificado</span>
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
          {esFinalizada && (
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleGenerarLote}
              disabled={generating || asistentes.length === 0}
              title={
                asistentes.length === 0
                  ? 'No hay asistentes inscritos'
                  : 'Generar todos los certificados'
              }
            >
              {generating ? (
                <Spinner size={14} label="Generando..." />
              ) : (
                <FileCheck2 width={16} height={16} />
              )}
              <span>Generar todos los certificados</span>
            </button>
          )}
          <button
            type="button"
            className="btn btn--secondary"
            onClick={handleVolver}
            disabled={generating}
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
        title="Generar todos los certificados"
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
          Se generarán los certificados de todos los asistentes inscritos a{' '}
          <strong>{capacitacion?.codigo}</strong>
          {capacitacion?.tema ? ` — ${capacitacion.tema}` : ''}.
        </p>
        <p className="text-sm text-secondary">
          La operación puede tardar unos segundos. ¿Deseas continuar?
        </p>
      </Modal>

      {/* Modal resumen de resultados del lote (cuando hay errores parciales) */}
      <Modal
        isOpen={Boolean(loteResult)}
        onClose={() => setLoteResult(null)}
        title="Resumen de emisión"
        footer={
          <button
            type="button"
            className="btn btn--primary"
            onClick={() => setLoteResult(null)}
          >
            Cerrar
          </button>
        }
      >
        {loteResult && (
          <>
            <p>
              Se emitieron <strong>{loteResult.emitidos}</strong> de{' '}
              <strong>{loteResult.total}</strong> certificados.
            </p>
            {loteResult.errores.length > 0 && (
              <>
                <p className="text-sm text-secondary">
                  Los siguientes fallaron:
                </p>
                <div className="table-container">
                  <table className="table">
                    <thead>
                      <tr>
                        <th style={{ textAlign: 'left' }}>Asistente</th>
                        <th style={{ textAlign: 'left' }}>Motivo</th>
                      </tr>
                    </thead>
                    <tbody>
                      {loteResult.errores.map((e, idx) => {
                        const asist = e.asistenteId
                          ? asistentesById.get(e.asistenteId)
                          : null;
                        const label = asist
                          ? `${asist.nombres ?? ''} ${asist.apellidos ?? ''}`.trim() ||
                            e.codigo ||
                            e.asistenteId
                          : e.codigo || e.asistenteId || '—';
                        return (
                          <tr key={`${e.asistenteId || e.codigo || idx}-${idx}`}>
                            <td>{label}</td>
                            <td>{e.mensaje || '—'}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </>
            )}
          </>
        )}
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
