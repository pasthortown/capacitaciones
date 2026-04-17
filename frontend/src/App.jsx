import { Routes, Route, Navigate } from 'react-router-dom';
import AppLayout from './layouts/AppLayout.jsx';
import LoginPage from './pages/LoginPage.jsx';
import CapacitacionesPage from './pages/capacitaciones/CapacitacionesPage.jsx';
import AsistentesPage from './pages/asistentes/AsistentesPage.jsx';
import CapacitadorPage from './pages/capacitador/CapacitadorPage.jsx';
import InscripcionPage from './pages/inscripcion/InscripcionPage.jsx';
import ResponsablePage from './pages/responsable/ResponsablePage.jsx';
import ResponsablesPage from './pages/responsables/ResponsablesPage.jsx';
import ModalidadesPage from './pages/catalogos/ModalidadesPage.jsx';
import TiposActividadPage from './pages/catalogos/TiposActividadPage.jsx';
import AreasPage from './pages/catalogos/AreasPage.jsx';
import NumeracionPage from './pages/configuracion/NumeracionPage.jsx';
import ProtectedRoute from './auth/ProtectedRoute.jsx';

/**
 * Rutas raíz de la aplicación.
 *
 * Fase 3:
 *  - `/` redirige a `/capacitaciones` (el dashboard de cards es el Home real).
 *  - `HomePage.jsx` se conserva para uso futuro (dashboard ampliado).
 *
 * Rutas protegidas:
 *   /capacitaciones              → Listado de capacitaciones (dashboard)
 *   /capacitaciones/:id/asistentes → Placeholder Fase 5
 *   /catalogos/modalidades       → Modalidades
 *   /catalogos/tipos-actividad   → Tipos de actividad
 *   /catalogos/areas             → Áreas
 *   /configuracion/numeracion    → Configuración del contador
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
      {/* Públicas (sin sidebar / sin guard admin) */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/capacitador" element={<CapacitadorPage />} />
      <Route path="/responsable" element={<ResponsablePage />} />
      <Route path="/inscripcion" element={<InscripcionPage />} />

      {/* Protegidas */}
      <Route element={<ProtectedRoute />}>
        <Route element={<AppLayout />}>
          {/* Home real → /capacitaciones */}
          <Route index element={<Navigate to="/capacitaciones" replace />} />

          {/* Capacitaciones */}
          <Route path="/capacitaciones" element={<CapacitacionesPage />} />
          <Route
            path="/capacitaciones/:id/asistentes"
            element={<AsistentesPage />}
          />

          {/* Responsables (catálogo global) */}
          <Route path="/responsables" element={<ResponsablesPage />} />

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
