import { useContext } from 'react';
import { ToastContext } from './toastContext.js';

/**
 * Hook de acceso al contexto de Toast.
 *
 * Fuera del `ToastProvider` retorna un stub silencioso (no explota),
 * útil para tests o árboles donde el provider aún no fue montado.
 */
export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) {
    const noop = () => null;
    return {
      success: noop,
      error: noop,
      info: noop,
      warning: noop,
      dismiss: noop,
    };
  }
  return ctx;
}

export default useToast;
