import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AuthContext } from './AuthContext.jsx';
import http, { AUTH_STORAGE_KEY, HttpError } from '../services/http.js';

/**
 * AuthProvider — estado global de autenticación del admin.
 *
 * API expuesta por `useAuth()`:
 *   - user:            { id, email, nombres, roles } | null
 *   - token:           string | null
 *   - isAuthenticated: boolean
 *   - isLoading:       boolean (true mientras valida un token existente)
 *   - login(email, password): Promise<void>
 *   - logout():        void
 *
 * Flujo:
 *  1. Al montar, si hay un token en localStorage, pide `GET /auth/me`.
 *     Si responde 200, guarda el user. Si responde 401, limpia el token.
 *  2. `login()` hace `POST /auth/login`; si éxito, guarda token y luego
 *     carga el user con `/auth/me`.
 *  3. `logout()` limpia token y user.
 *  4. Escucha el evento global `auth:expired` (emitido por http.js cuando
 *     cualquier request falla con 401) y limpia el estado — el router
 *     se encarga de redirigir a `/login` via ProtectedRoute.
 */
function readTokenFromStorage() {
  try {
    return localStorage.getItem(AUTH_STORAGE_KEY);
  } catch {
    return null;
  }
}

function writeTokenToStorage(token) {
  try {
    if (token) {
      localStorage.setItem(AUTH_STORAGE_KEY, token);
    } else {
      localStorage.removeItem(AUTH_STORAGE_KEY);
    }
  } catch {
    /* storage bloqueado: no hay nada que hacer */
  }
}

export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => readTokenFromStorage());
  const [user, setUser] = useState(null);
  const [isLoading, setIsLoading] = useState(Boolean(readTokenFromStorage()));

  // Evita setState tras unmount durante el bootstrap async.
  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const clearSession = useCallback(() => {
    writeTokenToStorage(null);
    setToken(null);
    setUser(null);
  }, []);

  const fetchMe = useCallback(async () => {
    try {
      const me = await http.get('/auth/me');
      if (mountedRef.current) {
        setUser(me);
      }
      return me;
    } catch (error) {
      if (error instanceof HttpError && error.status === 401) {
        if (mountedRef.current) {
          clearSession();
        }
      }
      throw error;
    }
  }, [clearSession]);

  // Bootstrap: si hay token al montar, valida con /auth/me.
  useEffect(() => {
    const existingToken = readTokenFromStorage();
    if (!existingToken) {
      setIsLoading(false);
      return;
    }
    let cancelled = false;
    (async () => {
      try {
        await fetchMe();
      } catch {
        /* fetchMe ya limpió la sesión si corresponde */
      } finally {
        if (!cancelled && mountedRef.current) {
          setIsLoading(false);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
    // Solo en el mount.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Escucha 401 globales emitidos por http.js.
  useEffect(() => {
    const handleExpired = () => {
      clearSession();
    };
    window.addEventListener('auth:expired', handleExpired);
    return () => window.removeEventListener('auth:expired', handleExpired);
  }, [clearSession]);

  const login = useCallback(
    async (usuario, password) => {
      const response = await http.post('/auth/login', { usuario, password });
      const nextToken = response?.token;
      if (!nextToken) {
        throw new HttpError('Respuesta de login inválida.', { status: 500 });
      }
      writeTokenToStorage(nextToken);
      if (mountedRef.current) {
        setToken(nextToken);
      }
      // Cargar user fresco desde /auth/me (fuente de verdad).
      try {
        await fetchMe();
      } catch (error) {
        // Si /auth/me falla justo después del login, invalida todo.
        clearSession();
        throw error;
      }
    },
    [fetchMe, clearSession],
  );

  const logout = useCallback(() => {
    clearSession();
  }, [clearSession]);

  const value = useMemo(
    () => ({
      user,
      token,
      isAuthenticated: Boolean(token && user),
      isLoading,
      login,
      logout,
    }),
    [user, token, isLoading, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export default AuthProvider;
