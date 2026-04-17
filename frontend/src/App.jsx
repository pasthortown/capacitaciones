import { Routes, Route } from 'react-router-dom';
import AppLayout from './layouts/AppLayout.jsx';
import HomePage from './pages/HomePage.jsx';

/**
 * Rutas raíz de la aplicación.
 *
 * Placeholder de módulos pendientes (se implementarán en fases siguientes):
 *  - /catalogos      -> CRUD catálogos (Modalidad, TipoActividad, Área)
 *  - /configuracion  -> Configuración de numeración CAP-PC-REG-###
 *
 * `/` redirige al dashboard de capacitaciones (por ahora HomePage).
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
        <Route
          path="/catalogos"
          element={
            <PlaceholderPage
              titulo="Catálogos"
              descripcion="Modalidad, Tipo de Actividad y Áreas."
            />
          }
        />
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
