import Toast from './Toast.jsx';
import styles from './Toast.module.css';

/**
 * Contenedor fijo en la esquina superior derecha donde se renderizan
 * los toasts activos. Solo lo usa `ToastProvider`.
 */
export default function ToastViewport({ toasts, onDismiss }) {
  if (!toasts || toasts.length === 0) return null;

  return (
    <div className={styles.viewport} aria-live="polite" aria-atomic="false">
      {toasts.map((toast) => (
        <Toast key={toast.id} toast={toast} onDismiss={onDismiss} />
      ))}
    </div>
  );
}
