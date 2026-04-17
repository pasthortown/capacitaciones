import { NavLink } from 'react-router-dom';
import { GraduationCap, Library, Settings, LogOut } from 'lucide-react';

/**
 * Construye la clase del link según el estado activo que entrega NavLink.
 */
function navLinkClass({ isActive }) {
  return [
    'sidebar__nav-link',
    isActive ? 'sidebar__nav-link--active' : '',
  ]
    .filter(Boolean)
    .join(' ');
}

/**
 * Navegación lateral de la app.
 * Usa las clases `.sidebar*` del design system.
 */
export default function Sidebar() {
  return (
    <nav className="sidebar">
      {/* Logo / branding */}
      <div className="sidebar__logo">
        <h3 style={{ margin: 0, fontWeight: 700 }}>Capacitaciones</h3>
        <p className="text-xs text-secondary" style={{ margin: 0 }}>
          Registro y gestión
        </p>
      </div>

      {/* Sección principal */}
      <div className="sidebar__section">
        <div className="sidebar__section-title">PRINCIPAL</div>
        <ul className="sidebar__nav">
          <li className="sidebar__nav-item">
            <NavLink to="/" end className={navLinkClass}>
              <GraduationCap className="sidebar__nav-icon" />
              <span>Capacitaciones</span>
            </NavLink>
          </li>
          <li className="sidebar__nav-item">
            <NavLink to="/catalogos" className={navLinkClass}>
              <Library className="sidebar__nav-icon" />
              <span>Catálogos</span>
            </NavLink>
          </li>
          <li className="sidebar__nav-item">
            <NavLink to="/configuracion" className={navLinkClass}>
              <Settings className="sidebar__nav-icon" />
              <span>Configuración</span>
            </NavLink>
          </li>
        </ul>
      </div>

      {/* Footer: cerrar sesión (placeholder, se activa cuando haya auth) */}
      <div className="sidebar__footer">
        <a href="#" className="sidebar__nav-link sidebar__nav-link--logout">
          <LogOut className="sidebar__nav-icon" />
          <span>Cerrar sesión</span>
        </a>
      </div>
    </nav>
  );
}
