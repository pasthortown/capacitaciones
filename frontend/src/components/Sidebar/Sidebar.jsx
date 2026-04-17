import { useState } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import {
  GraduationCap,
  Library,
  Settings,
  LogOut,
  ChevronDown,
  ChevronRight,
  Layers,
  ListChecks,
  Building2,
} from 'lucide-react';

/**
 * Construye la clase del link según el estado activo que entrega NavLink.
 */
function navLinkClass({ isActive }) {
  return ['sidebar__nav-link', isActive ? 'sidebar__nav-link--active' : '']
    .filter(Boolean)
    .join(' ');
}

/**
 * Navegación lateral de la app.
 * Usa las clases `.sidebar*` del design system.
 *
 * Incluye un grupo colapsable "Catálogos" con 3 entradas (Modalidades,
 * Tipos de Actividad, Áreas) que se expande por defecto si la ruta actual
 * pertenece al grupo.
 */
export default function Sidebar() {
  const location = useLocation();
  const isCatalogoActive = location.pathname.startsWith('/catalogos');
  const [catalogosOpen, setCatalogosOpen] = useState(isCatalogoActive);

  const toggleCatalogos = () => setCatalogosOpen((v) => !v);

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

          {/* Grupo colapsable: Catálogos */}
          <li className="sidebar__nav-item">
            <button
              type="button"
              onClick={toggleCatalogos}
              className={`sidebar__nav-link${
                isCatalogoActive ? ' sidebar__nav-link--active' : ''
              }`}
              style={{
                width: '100%',
                background: 'none',
                border: 'none',
                cursor: 'pointer',
                textAlign: 'left',
                justifyContent: 'flex-start',
              }}
              aria-expanded={catalogosOpen}
            >
              <Library className="sidebar__nav-icon" />
              <span style={{ flex: 1 }}>Catálogos</span>
              {catalogosOpen ? (
                <ChevronDown width={16} height={16} />
              ) : (
                <ChevronRight width={16} height={16} />
              )}
            </button>

            {catalogosOpen && (
              <ul
                className="sidebar__nav"
                style={{ marginTop: 4, paddingLeft: 20 }}
              >
                <li className="sidebar__nav-item">
                  <NavLink to="/catalogos/modalidades" className={navLinkClass}>
                    <Layers className="sidebar__nav-icon" />
                    <span>Modalidades</span>
                  </NavLink>
                </li>
                <li className="sidebar__nav-item">
                  <NavLink
                    to="/catalogos/tipos-actividad"
                    className={navLinkClass}
                  >
                    <ListChecks className="sidebar__nav-icon" />
                    <span>Tipos de actividad</span>
                  </NavLink>
                </li>
                <li className="sidebar__nav-item">
                  <NavLink to="/catalogos/areas" className={navLinkClass}>
                    <Building2 className="sidebar__nav-icon" />
                    <span>Áreas</span>
                  </NavLink>
                </li>
              </ul>
            )}
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
