import { X, CheckCircle2, AlertCircle, Info, AlertTriangle } from 'lucide-react';
import styles from './Toast.module.css';

/**
 * Tarjeta visual de un toast individual.
 * Reutiliza `.alert` del design system para la paleta y suma posicionamiento
 * local (ver Toast.module.css).
 */
const ICON_BY_TYPE = {
  success: CheckCircle2,
  error: AlertCircle,
  info: Info,
  warning: AlertTriangle,
};

export default function Toast({ toast, onDismiss }) {
  const Icon = ICON_BY_TYPE[toast.type] || Info;
  const alertClass = `alert alert--${toast.type}`;

  return (
    <div className={`${alertClass} ${styles.toast}`} role="status">
      <Icon className="alert__icon" width={20} height={20} />
      <div className="alert__content">
        {toast.title && <div className="alert__title">{toast.title}</div>}
        <div className="alert__message">{toast.message}</div>
      </div>
      <button
        type="button"
        className={styles.close}
        onClick={() => onDismiss(toast.id)}
        aria-label="Cerrar notificación"
      >
        <X width={16} height={16} />
      </button>
    </div>
  );
}
