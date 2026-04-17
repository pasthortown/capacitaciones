import { Routes, Route, Navigate } from 'react-router-dom';
import AppLayout from './layouts/AppLayout.jsx';
import HomePage from './pages/HomePage.jsx';
import LoginPage from './pages/LoginPage.jsx';
import ModalidadesPage from './pages/catalogos/ModalidadesPage.jsx';
import TiposActividadPage from './pages/catalogos/TiposActividadPage.jsx';
import AreasPage from './pages/catalogos/AreasPage.jsx';
import NumeracionPage from './pages/configuracion/NumeracionPage.jsx';
import ProtectedRoute from './auth/ProtectedRoute.jsx';

/**
 * Rutas raíz de la aplicación.
 *
 * Fase 2:
 *  - `/login` es pública (sin layout admin).
 *  - Todo lo demás vive detrás de `<ProtectedRoute>` que envuelve al `AppLayout`.
 *
 * Rutas protegidas:
 *   /                        → HomePage
 *   /catalogos/modalidades   → Modalidades
 *   /catalogos/tipos-actividad → Tipos de actividad
 *   /catalogos/areas         → Áreas
 *   /configuracion           → Numeración CAP-PC-REG-###
 *   /capacitaciones          → Placeholder (fase 3)
 */
function PlaceholderPage({ titulo, descripcion }) {
  return (
    <div className="placeholder-page">
      <div className="page-header">
        <div>
          <h1 className="page-header__title">{titulo}</h1>
          {descripcion && <p className="page-header__subtitle">{descripcion}</p>}
        </div>
      </div>
      <div className="card">
        <div className="card__body">
          <div className="empty-state">
            <div className="empty-state__title">En construcción</div>
            <p className="empty-state__description">
              Este módulo será implementado en una fase posterior.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

export default function App() {
  return (
    <Routes>
      {/* Pública */}
      <Route path="/login" element={<LoginPage />} />

      {/* Protegidas */}
      <Route element={<ProtectedRoute />}>
        <Route element={<AppLayout />}>
          <Route index element={<HomePage />} />

          {/* Catálogos */}
          <Route
            path="/catalogos"
            element={<Navigate to="/catalogos/modalidades" replace />}
          />
          <Route path="/catalogos/modalidades" element={<ModalidadesPage />} />
          <Route path="/catalogos/tipos-actividad" element={<TiposActividadPage />} />
          <Route path="/catalogos/areas" element={<AreasPage />} />

          {/* Configuración */}
          <Route
            path="/configuracion"
            element={<Navigate to="/configuracion/numeracion" replace />}
          />
          <Route path="/configuracion/numeracion" element={<NumeracionPage />} />

          {/* Pendiente fase 3 */}
          <Route
            path="/capacitaciones"
            element={
              <PlaceholderPage
                titulo="Capacitaciones"
                descripcion="Listado y gestión de capacitaciones."
              />
            }
          />

          <Route
            path="*"
            element={
              <PlaceholderPage
                titulo="Página no encontrada"
                descripcion="La ruta solicitada no existe."
              />
            }
          />
        </Route>
      </Route>
    </Routes>
  );
}
