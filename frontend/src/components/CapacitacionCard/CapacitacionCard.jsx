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
  ClipboardCheck,
  Award,
  MessagesSquare,
  Tag,
} from 'lucide-react';
import { useToast } from '../Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import { formatFechaHora, formatDuracion } from '../../utils/formatters.js';
import { buildPublicUrl } from '../../utils/urls.js';
import {
  generateLinkCapacitador,
  generateLinkInscripcion,
  generateLinkPaseLista,
  generateLinkCalificaciones,
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
  const [generandoPaseLista, setGenerandoPaseLista] = useState(false);
  const [generandoCalificaciones, setGenerandoCalificaciones] = useState(false);

  const {
    id,
    codigo,
    tema,
    capacitador,
    modalidad,
    tipoActividad,
    fechaHoraInicio,
    duracionMinutos,
    estado,
    totalAsistentes = 0,
    activo = true,
    logoUrl = null,
    tipoCertificacion = null,
  } = capacitacion || {};

  // El link/botón de calificaciones sólo aplica cuando la capacitación
  // es de Aprobación (ver instrucciones.md §7.10 — Fase 11).
  const esAprobacion = tipoCertificacion === 'Aprobacion';

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
      const fullUrl = buildPublicUrl(url);
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

  const handleCopyPaseListaLink = async () => {
    if (!id || generandoPaseLista) return;
    setGenerandoPaseLista(true);
    try {
      const { url, expiresAt } = await generateLinkPaseLista(id);
      const fullUrl = buildPublicUrl(url);
      await copyToClipboard(fullUrl);
      const fecha = formatExpiresAt(expiresAt);
      toast.success(
        fecha
          ? `Enlace de pase de lista copiado (expira: ${fecha})`
          : 'Enlace de pase de lista copiado',
      );
    } catch (error) {
      toast.error(error?.message || 'No se pudo generar el enlace de pase de lista.');
    } finally {
      setGenerandoPaseLista(false);
    }
  };

  const handleCopyCalificacionesLink = async () => {
    if (!id || generandoCalificaciones) return;
    setGenerandoCalificaciones(true);
    try {
      const { url, expiresAt } = await generateLinkCalificaciones(id);
      const fullUrl = buildPublicUrl(url);
      await copyToClipboard(fullUrl);
      const fecha = formatExpiresAt(expiresAt);
      toast.success(
        fecha
          ? `Enlace de calificaciones copiado (expira: ${fecha})`
          : 'Enlace de calificaciones copiado',
      );
    } catch (error) {
      // Edge case: la capacitación cambió de tipo y ya no aplica calificar.
      if (
        error instanceof HttpError &&
        error.status === 409 &&
        error.body?.error === 'CALIFICACIONES_NO_APLICA'
      ) {
        toast.error(
          'Esta capacitación es de Participación; no se pueden registrar calificaciones.',
        );
      } else {
        toast.error(error?.message || 'No se pudo generar el enlace de calificaciones.');
      }
    } finally {
      setGenerandoCalificaciones(false);
    }
  };

  const handleOpenAsistentes = () => {
    navigate(`/capacitaciones/${id}/asistentes`);
  };

  const handleCopyEncuestaLink = async () => {
    if (!id) return;
    try {
      // La encuesta usa id de capacitación directo (sin JWT) — el asistente se
      // autoidentifica con cédula al enviar. `buildPublicUrl` prefija origin + BASE_URL.
      const fullUrl = buildPublicUrl(`/encuesta/${id}`);
      await copyToClipboard(fullUrl);
      toast.success('Enlace de encuesta copiado.');
    } catch (error) {
      toast.error(error?.message || 'No se pudo copiar el enlace de encuesta.');
    }
  };

  const handleCopyInscripcionLink = async () => {
    if (!id || generandoInscripcion) return;
    setGenerandoInscripcion(true);
    try {
      const { url, expiresAt } = await generateLinkInscripcion(id);
      const fullUrl = buildPublicUrl(url);
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

      {/* Fila 2: tema (con miniatura opcional del logo a la izquierda) */}
      <div className={styles.titleRow}>
        {logoUrl && (
          <img
            src={logoUrl}
            alt=""
            className={styles.logoThumb}
            loading="lazy"
            aria-hidden="true"
          />
        )}
        <h3 className={styles.title}>{tema || 'Sin tema'}</h3>
      </div>

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
        {tipoActividad?.nombre && (
          <span className={styles.row}>
            <Tag className={styles.icon} aria-hidden="true" />
            <span>{tipoActividad.nombre}</span>
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
          onClick={handleCopyPaseListaLink}
          disabled={generandoPaseLista}
          title="Copiar enlace para pasar lista"
          aria-label="Copiar enlace para pasar lista"
        >
          <ClipboardCheck width={16} height={16} />
        </button>
        {esAprobacion && (
          <button
            type="button"
            className={styles.iconBtn}
            onClick={handleCopyCalificacionesLink}
            disabled={generandoCalificaciones}
            title="Copiar enlace de calificaciones"
            aria-label="Copiar enlace de calificaciones"
          >
            <Award width={16} height={16} />
          </button>
        )}
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
        {esFinalizada && (
          <button
            type="button"
            className={styles.iconBtn}
            onClick={handleCopyEncuestaLink}
            title="Copiar enlace de encuesta de satisfacción"
            aria-label="Copiar enlace de encuesta de satisfacción"
          >
            <MessagesSquare width={16} height={16} />
          </button>
        )}
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
