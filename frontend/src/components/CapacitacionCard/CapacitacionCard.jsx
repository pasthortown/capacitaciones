import { useState } from 'react';
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
  Building2,
} from 'lucide-react';
import { useToast } from '../Toast/useToast.js';
import { formatFechaHora, formatDuracion } from '../../utils/formatters.js';
import {
  generateLinkCapacitador,
  generateLinkInscripcion,
} from '../../services/capacitaciones.js';
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

  const [generandoLink, setGenerandoLink] = useState(false);
  const [generandoInscripcion, setGenerandoInscripcion] = useState(false);

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

  const copyToClipboard = async (url) => {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(url);
      return;
    }
    // Fallback para entornos sin Clipboard API (ej. http / iframes).
    const tmp = document.createElement('textarea');
    tmp.value = url;
    tmp.setAttribute('readonly', '');
    tmp.style.position = 'absolute';
    tmp.style.left = '-9999px';
    document.body.appendChild(tmp);
    tmp.select();
    document.execCommand('copy');
    document.body.removeChild(tmp);
  };

  const formatExpiresAt = (iso) => {
    if (!iso) return '';
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) return '';
    return formatFechaHora(date);
  };

  const handleCopyCapacitadorLink = async () => {
    if (!id || generandoLink) return;
    setGenerandoLink(true);
    try {
      const { url, expiresAt } = await generateLinkCapacitador(id);
      const fullUrl = `${window.location.origin}${url}`;
      await copyToClipboard(fullUrl);
      const fecha = formatExpiresAt(expiresAt);
      toast.success(
        fecha
          ? `Enlace del capacitador copiado (expira: ${fecha})`
          : 'Enlace del capacitador copiado',
      );
    } catch (error) {
      toast.error(error?.message || 'No se pudo generar el enlace del capacitador.');
    } finally {
      setGenerandoLink(false);
    }
  };

  const handleOpenAsistentes = () => {
    navigate(`/capacitaciones/${id}/asistentes`);
  };

  const handleCopyInscripcionLink = async () => {
    if (!id || generandoInscripcion) return;
    setGenerandoInscripcion(true);
    try {
      const { url, expiresAt } = await generateLinkInscripcion(id);
      const fullUrl = `${window.location.origin}${url}`;
      await copyToClipboard(fullUrl);
      const fecha = formatExpiresAt(expiresAt);
      toast.success(
        fecha
          ? `Enlace de inscripción copiado (expira: ${fecha})`
          : 'Enlace de inscripción copiado',
      );
    } catch (error) {
      toast.error(error?.message || 'No se pudo generar el enlace de inscripción.');
    } finally {
      setGenerandoInscripcion(false);
    }
  };

  return (
    <article className={styles.card} aria-label={`Capacitación ${codigo || ''}`}>
      {/* Fila 1: código (izquierda) | fecha · hora · duración (derecha) */}
      <div className={styles.topRow}>
        {codigo && <span className={styles.codeChip}>{codigo}</span>}
        <div className={styles.topRowMeta}>
          <Calendar className={styles.icon} aria-hidden="true" />
          <strong>{formatFechaHora(fechaHoraInicio) || '—'}</strong>
          <span className={styles.separator}>·</span>
          <Clock className={styles.icon} aria-hidden="true" />
          <span>{formatDuracion(duracionMinutos)}</span>
        </div>
      </div>

      {/* Fila 2: tema */}
      <h3 className={styles.title}>{tema || 'Sin tema'}</h3>

      {/* Fila 3: capacitador */}
      <div className={styles.row}>
        <User className={styles.icon} aria-hidden="true" />
        <span>{capacitador || '—'}</span>
      </div>

      {/* Fila 4: modalidad · asistentes · estado */}
      <div className={styles.metaRow}>
        {modalidad?.nombre && (
          <span className={styles.row}>
            <Building2 className={styles.icon} aria-hidden="true" />
            <span>{modalidad.nombre}</span>
          </span>
        )}
        <span className={styles.row}>
          <Users className={styles.icon} aria-hidden="true" />
          <strong>{totalAsistentes}</strong>
          <span>asistentes</span>
        </span>
        <EstadoBadge estado={estado} activo={activo} />
      </div>

      {/* Fila 5: acciones, alineadas a la derecha */}
      <div className={styles.actions}>
        <button
          type="button"
          className={styles.iconBtn}
          onClick={handleCopyCapacitadorLink}
          disabled={generandoLink}
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
          disabled={generandoInscripcion}
          title="Copiar enlace de inscripción"
          aria-label="Copiar enlace de inscripción"
        >
          <Share2 width={16} height={16} />
        </button>
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
