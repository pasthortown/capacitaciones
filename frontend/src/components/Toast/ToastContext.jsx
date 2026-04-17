import { useCallback, useMemo, useRef, useState } from 'react';
import ToastViewport from './ToastViewport.jsx';
import { ToastContext } from './toastContext.js';

/**
 * ToastProvider: provee un feed simple de notificaciones transitorias.
 *
 * API expuesta por `useToast()` (ver `./useToast.js`):
 *   - toast.success(message, { title, timeout })
 *   - toast.error(message, { title, timeout })
 *   - toast.info(message, { title, timeout })
 *   - toast.warning(message, { title, timeout })
 *   - toast.dismiss(id)
 *
 * `timeout` por defecto: 4000ms (pasar `0` para toasts persistentes).
 */

const DEFAULT_TIMEOUT = 4000;

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);
  const timersRef = useRef(new Map());

  const dismiss = useCallback((id) => {
    const timer = timersRef.current.get(id);
    if (timer) {
      clearTimeout(timer);
      timersRef.current.delete(id);
    }
    setToasts((current) => current.filter((item) => item.id !== id));
  }, []);

  const push = useCallback(
    (type, message, options = {}) => {
      const { title, timeout = DEFAULT_TIMEOUT } = options;
      const id = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
      const entry = { id, type, message, title };
      setToasts((current) => [...current, entry]);

      if (timeout > 0) {
        const timer = setTimeout(() => dismiss(id), timeout);
        timersRef.current.set(id, timer);
      }

      return id;
    },
    [dismiss],
  );

  const api = useMemo(
    () => ({
      success: (message, options) => push('success', message, options),
      error: (message, options) => push('error', message, options),
      info: (message, options) => push('info', message, options),
      warning: (message, options) => push('warning', message, options),
      dismiss,
    }),
    [push, dismiss],
  );

  return (
    <ToastContext.Provider value={api}>
      {children}
      <ToastViewport toasts={toasts} onDismiss={dismiss} />
    </ToastContext.Provider>
  );
}
