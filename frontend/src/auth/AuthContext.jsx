import { createContext } from 'react';

/**
 * Contexto raíz de Auth. Se declara en un archivo propio (sin componentes)
 * para mantener la compatibilidad con React Fast Refresh.
 *
 * El valor del contexto es el API:
 *   { user, token, isAuthenticated, isLoading, login, logout }
 * o `null` si se consume fuera del provider.
 */
export const AuthContext = createContext(null);
