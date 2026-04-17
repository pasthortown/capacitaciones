import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from './useAuth.js';
import Spinner from '../components/Spinner/Spinner.jsx';

/**
 * Wrapper de rutas que requieren sesión admin.
 *
 *  - Si el provider está cargando el bootstrap (`isLoading`), muestra un
 *    spinner centrado. Sin esto, al recargar una ruta protegida con token
 *    válido habría un parpadeo hacia /login.
 *  - Si no hay sesión, redirige a `/login` preservando `from` para volver
 *    al destino original tras el login exitoso.
 *  - Si hay sesión, renderiza el Outlet (layout + ruta hija).
 */
export default function ProtectedRoute() {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return <Spinner fullscreen size={40} label="Cargando sesión..." />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <Outlet />;
}
