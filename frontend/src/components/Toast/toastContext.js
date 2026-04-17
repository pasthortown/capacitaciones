import { createContext } from 'react';

/**
 * Contexto raíz de Toast. Se declara en un archivo propio (sin componentes)
 * para mantener la compatibilidad con React Fast Refresh.
 *
 * El valor del contexto es el API ({ success, error, info, warning, dismiss })
 * que inyecta `ToastProvider`, o `null` si se consume fuera del provider.
 */
export const ToastContext = createContext(null);
