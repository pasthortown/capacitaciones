import { useContext } from 'react';
import { AuthContext } from './AuthContext.jsx';

/**
 * Hook de acceso al contexto de autenticación.
 *
 * Fuera del `AuthProvider` retorna un stub no autenticado — útil para
 * tests o componentes montados antes del provider.
 */
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    return {
      user: null,
      token: null,
      isAuthenticated: false,
      isLoading: false,
      login: async () => {
        throw new Error('AuthProvider no disponible.');
      },
      logout: () => null,
    };
  }
  return ctx;
}

export default useAuth;
