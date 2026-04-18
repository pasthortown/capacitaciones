import { useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Menu } from 'lucide-react';
import Sidebar from '../components/Sidebar/Sidebar.jsx';
import { useAuth } from '../auth/useAuth.js';

const SIDEBAR_COLLAPSED_KEY = 'capacitaciones.sidebarCollapsed';

function readCollapsedPreference() {
  try {
    return window.localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === '1';
  } catch {
    return false;
  }
}

/**
 * Layout principal de la app admin.
 * - Sidebar fija a la izquierda, colapsable vía botón hamburguesa en el header.
 *   El estado se persiste en `localStorage` para que se recuerde entre sesiones.
 *   Cuando está colapsada muestra solo iconos; el nombre completo se entrega
 *   como tooltip nativo (`title`) en cada NavLink (ver Sidebar.jsx).
 * - Body scrollable con <Outlet/> (contenido de la ruta activa).
 * - Header superior con el botón hamburguesa y, a la derecha, el bloque del
 *   usuario autenticado (avatar con inicial + nombre + rol) usando las clases
 *   `.header__user*` del design system (ver style/example.html).
 */
export default function AppLayout() {
  const { user } = useAuth();
  const email = user?.email || '';
  const displayName = user?.nombres || email || 'Admin';
  const role = user?.role || user?.rol || 'Administrador';
  const initial = (displayName || '?').trim().charAt(0).toUpperCase();

  const [collapsed, setCollapsed] = useState(readCollapsedPreference);

  useEffect(() => {
    try {
      window.localStorage.setItem(SIDEBAR_COLLAPSED_KEY, collapsed ? '1' : '0');
    } catch {
      // storage bloqueado — la preferencia se perderá al recargar.
    }
  }, [collapsed]);

  const toggleSidebar = () => setCollapsed((v) => !v);

  return (
    <div className="layout">
      <aside
        className={`layout__sidebar${collapsed ? ' layout__sidebar--collapsed' : ''}`}
      >
        <Sidebar collapsed={collapsed} />
      </aside>

      <main
        className={`layout__main${collapsed ? ' layout__main--expanded' : ''}`}
      >
        <header className="layout__header">
          <div className="header">
            <div className="header__left">
              <button
                type="button"
                className="header__toggle"
                onClick={toggleSidebar}
                aria-label={collapsed ? 'Expandir menú' : 'Colapsar menú'}
                aria-expanded={!collapsed}
                title={collapsed ? 'Expandir menú' : 'Colapsar menú'}
              >
                <Menu width={24} height={24} />
              </button>
            </div>

            <div className="header__right">
              <div className="header__user" title={email || displayName}>
                <div
                  className="header__user-avatar"
                  aria-hidden="true"
                  style={{
                    width: 40,
                    height: 40,
                    borderRadius: '50%',
                    background: 'var(--color-primary)',
                    color: 'var(--color-primary-contrast, #fff)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontWeight: 700,
                    fontSize: 'var(--font-size-md, 1rem)',
                    flexShrink: 0,
                  }}
                >
                  {initial}
                </div>
                <div className="header__user-info">
                  <div className="header__user-name">{displayName}</div>
                  <div className="header__user-role">{role}</div>
                </div>
              </div>
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
