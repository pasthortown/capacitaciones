import { Routes, Route, Navigate } from 'react-router-dom';
import AppLayout from './layouts/AppLayout.jsx';
import HomePage from './pages/HomePage.jsx';
import ModalidadesPage from './pages/catalogos/ModalidadesPage.jsx';
import TiposActividadPage from './pages/catalogos/TiposActividadPage.jsx';
import AreasPage from './pages/catalogos/AreasPage.jsx';

/**
 * Rutas raíz de la aplicación.
 *
 * Fase 1 entrega las pantallas de catálogos:
 *   /catalogos/modalidades
 *   /catalogos/tipos-actividad
 *   /catalogos/areas
 *
 * `/catalogos` sin hijo redirige a `/catalogos/modalidades`.
 *
 * Placeholder de módulos pendientes (se implementarán en fases siguientes):
 *  - /capacitaciones -> Dashboard de capacitaciones (fase 3).
 *  - /configuracion  -> Numeración CAP-PC-REG-### (fase 2).
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

        {/* Módulos pendientes */}
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
          path="/configuracion"
          element={
            <PlaceholderPage
              titulo="Configuración"
              descripcion="Numeración CAP-PC-REG-### y parámetros del sistema."
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
    </Routes>
  );
}
