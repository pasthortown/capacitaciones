import { Outlet } from 'react-router-dom';
import Sidebar from '../components/Sidebar/Sidebar.jsx';

/**
 * Layout principal de la app admin.
 * - Sidebar fijo a la izquierda (~240px) con la navegación.
 * - Body scrollable con <Outlet/> (contenido de la ruta activa).
 * - Header superior con el título de la app.
 *
 * Utiliza las clases `.layout`, `.layout__sidebar`, `.layout__main`,
 * `.layout__header`, `.layout__content` del design system.
 */
export default function AppLayout() {
  return (
    <div className="layout">
      <aside className="layout__sidebar">
        <Sidebar />
      </aside>

      <main className="layout__main">
        <header className="layout__header">
          <div className="header">
            <div className="header__left">
              <h2 className="text-lg font-semibold" style={{ margin: 0 }}>
                Capacitaciones
              </h2>
            </div>
            <div className="header__right">
              {/* Placeholder para el avatar/usuario logueado en fases futuras */}
            </div>
          </div>
        </header>

        <div className="layout__content">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
