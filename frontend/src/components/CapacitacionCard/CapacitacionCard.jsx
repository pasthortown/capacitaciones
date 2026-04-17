import { useNavigate } from 'react-router-dom';
import {
  User,
  Clock,
  Calendar,
  Link2,
  Users,
  Share2,
  Pencil,
  Trash2,
} from 'lucide-react';
import { useToast } from '../Toast/useToast.js';
import { formatFechaHora, formatDuracion } from '../../utils/formatters.js';
import styles from './CapacitacionCard.module.css';

/**
 * Card de una capacitación en el dashboard.
 *
 * Layout: card horizontal, alto corto. Grid 1fr | acciones.
 *
 * Props:
 *   - capacitacion: resumen con {id, codigo, tema, capacitador, modalidad, fechaHoraInicio,
 *                                duracionMinutos, estado, totalAsistentes, activo}
 *   - onEdit(capacitacion)
 *   - onDelete(capacitacion)
 */
export default function CapacitacionCard({ capacitacion, onEdit, onDelete }) {
  const navigate = useNavigate();
  const toast = useToast();

  const {
    id,
    codigo,
    tema,
    capacitador,
    modalidad,
    fechaHoraInicio,
    duracionMinutos,
    estado,
    totalAsistentes = 0,
    activo = true,
  } = capacitacion || {};

  const esFinalizada = estado === 'Finalizada';

  // TODO Fase 4: reemplazar este placeholder por la URL firmada real que
  // provee el backend. Por ahora copiamos una URL local basada en el id.
  const copyToClipboard = async (url, successMessage) => {
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        await navigator.clipboard.writeText(url);
      } else {
        // Fallback para entornos sin Clipboard API.
        const tmp = document.createElement('textarea');
        tmp.value = url;
        tmp.setAttribute('readonly', '');
        tmp.style.position = 'absolute';
        tmp.style.left = '-9999px';
        document.body.appendChild(tmp);
        tmp.select();
        document.execCommand('copy');
        document.body.removeChild(tmp);
      }
      toast.success(successMessage);
    } catch {
      toast.error('No se pudo copiar el enlace.');
    }
  };

  const handleCopyCapacitadorLink = () => {
    // TODO Fase 4: sustituir por URL firmada emitida por el backend.
    const url = `${window.location.origin}/capacitador/${id}`;
    copyToClipboard(url, 'Enlace copiado');
  };

  const handleOpenAsistentes = () => {
    navigate(`/capacitaciones/${id}/asistentes`);
  };

  const handleCopyInscripcionLink = () => {
    // TODO Fase 5: sustituir por URL firmada emitida por el backend.
    const url = `${window.location.origin}/inscripcion/${id}`;
    copyToClipboard(url, 'Enlace copiado');
  };

  return (
    <article className={styles.card} aria-label={`Capacitación ${codigo || ''}`}>
      <div className={styles.main}>
        <div className={styles.titleRow}>
          <h3 className={styles.title}>{tema || 'Sin tema'}</h3>
          {codigo && <span className={styles.codeChip}>{codigo}</span>}
        </div>

        <div className={styles.row}>
          <User className={styles.icon} aria-hidden="true" />
          <span>{capacitador || '—'}</span>
        </div>

        <div className={styles.row}>
          <Calendar className={styles.icon} aria-hidden="true" />
          <strong>{formatFechaHora(fechaHoraInicio) || '—'}</strong>
          <span className={styles.separator}>·</span>
          <Clock className={styles.icon} aria-hidden="true" />
          <span>{formatDuracion(duracionMinutos)}</span>
          {modalidad?.nombre && (
            <>
              <span className={styles.separator}>·</span>
              <span>{modalidad.nombre}</span>
            </>
          )}
        </div>

        <div className={styles.footerRow}>
          <div className={styles.row}>
            <Users className={styles.icon} aria-hidden="true" />
            <strong>{totalAsistentes}</strong>
            <span>asistentes</span>
            <span className={styles.separator}>·</span>
            <EstadoBadge estado={estado} activo={activo} />
          </div>
        </div>
      </div>

      <div className={styles.actions}>
        <div className={styles.actionsRow}>
          <button
            type="button"
            className={styles.iconBtn}
            onClick={handleCopyCapacitadorLink}
            title="Copiar enlace para el capacitador"
            aria-label="Copiar enlace para el capacitador"
          >
            <Link2 width={16} height={16} />
          </button>
          <button
            type="button"
            className={styles.iconBtn}
            onClick={handleOpenAsistentes}
            title="Ver asistentes"
            aria-label="Ver asistentes"
          >
            <Users width={16} height={16} />
          </button>
          <button
            type="button"
            className={styles.iconBtn}
            onClick={handleCopyInscripcionLink}
            title="Copiar enlace de inscripción"
            aria-label="Copiar enlace de inscripción"
          >
            <Share2 width={16} height={16} />
          </button>
        </div>

        <div className={styles.actionsRow}>
          {!esFinalizada && (
            <button
              type="button"
              className={styles.iconBtn}
              onClick={() => onEdit?.(capacitacion)}
              title="Editar"
              aria-label="Editar capacitación"
            >
              <Pencil width={16} height={16} />
            </button>
          )}
          <button
            type="button"
            className={`${styles.iconBtn} ${styles.iconBtnDanger}`}
            onClick={() => onDelete?.(capacitacion)}
            title="Eliminar"
            aria-label="Eliminar capacitación"
          >
            <Trash2 width={16} height={16} />
          </button>
        </div>
      </div>
    </article>
  );
}

function EstadoBadge({ estado, activo }) {
  if (!activo) {
    return <span className={`${styles.badge} ${styles.badgeInactive}`}>Inactiva</span>;
  }
  switch (estado) {
    case 'Iniciada':
      return <span className={`${styles.badge} ${styles.badgeStarted}`}>Iniciada</span>;
    case 'Finalizada':
      return <span className={`${styles.badge} ${styles.badgeFinished}`}>Finalizada</span>;
    case 'Inscripciones Abiertas':
    default:
      return (
        <span className={`${styles.badge} ${styles.badgeOpen}`}>Inscripciones Abiertas</span>
      );
  }
}
