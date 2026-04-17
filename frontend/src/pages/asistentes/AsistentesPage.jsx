import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Download } from 'lucide-react';
import DataTable from '../../components/Table/DataTable.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import { getCapacitacion } from '../../services/capacitaciones.js';
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
 *  - Acción "Descargar certificado" por fila, habilitada sólo si la
 *    capacitación está en estado "Finalizada".
 *
 * Nota — Fase 5 (stub): el backend aún no emite el PDF real. `descargarCertificado`
 * responderá 501 "pendiente" o 409. Para Fase 6, cambiar el servicio a
 * `http.downloadBlob` (ver `services/asistentes.js`). Aquí sólo adaptaremos
 * el handler para que en lugar de setear el toast con el mensaje, no haga nada
 * (el navegador dispara la descarga automáticamente).
 */
export default function AsistentesPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const toast = useToast();

  const [loading, setLoading] = useState(true);
  const [capacitacion, setCapacitacion] = useState(null);
  const [asistentes, setAsistentes] = useState([]);
  const [downloadingId, setDownloadingId] = useState(null);

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

  const handleDescargar = async (asistente) => {
    if (!asistente?.id || downloadingId) return;
    setDownloadingId(asistente.id);
    try {
      await descargarCertificado(id, asistente.id);
      // En Fase 6, cuando el servicio use `downloadBlob`, el navegador
      // dispara el <a download> automáticamente y no hace falta toast.
      toast.success('Certificado descargado.');
    } catch (err) {
      if (err instanceof HttpError) {
        if (err.status === 501) {
          toast.info('Pendiente integración con emisor (Fase 6).');
        } else if (err.status === 409) {
          toast.error(
            err.message ||
              'La capacitación aún no está finalizada.',
          );
        } else {
          toast.error(err.message || 'No se pudo descargar el certificado.');
        }
      } else {
        toast.error(err?.message || 'No se pudo descargar el certificado.');
      }
    } finally {
      if (mountedRef.current) setDownloadingId(null);
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
    const disabled = !esFinalizada || isDownloading;
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
        <button
          type="button"
          className="btn btn--secondary"
          onClick={handleVolver}
        >
          <ArrowLeft width={16} height={16} />
          <span>Volver</span>
        </button>
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
