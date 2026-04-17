import { useState } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
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
  Hash,
  User,
} from 'lucide-react';
import { useAuth } from '../../auth/useAuth.js';

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
 * - Grupo colapsable "Catálogos" (Modalidades, Tipos de actividad, Áreas).
 * - Grupo colapsable "Configuración" (Numeración).
 * - Footer con bloque del usuario (avatar + email) y botón de cerrar sesión.
 *
 * Usa las clases `.sidebar*` del design system.
 */
export default function Sidebar() {
  const location = useLocation();
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  const isCatalogoActive = location.pathname.startsWith('/catalogos');
  const isConfigActive = location.pathname.startsWith('/configuracion');

  const [catalogosOpen, setCatalogosOpen] = useState(isCatalogoActive);
  const [configOpen, setConfigOpen] = useState(isConfigActive);

  const toggleCatalogos = () => setCatalogosOpen((v) => !v);
  const toggleConfig = () => setConfigOpen((v) => !v);

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  const email = user?.email || '';
  const displayName = user?.nombres || email || 'Admin';
  const initial = (displayName || '?').trim().charAt(0).toUpperCase();

  return (
    <nav className="sidebar">
      {/* Logo / branding */}
      <div className="sidebar__logo">
        <h3 style={{ margin: 0, fontWeight: 700, fontFamily: 'var(--font-family-heading)' }}>
          Capacitaciones
        </h3>
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
              <ul className="sidebar__nav" style={{ marginTop: 4, paddingLeft: 20 }}>
                <li className="sidebar__nav-item">
                  <NavLink to="/catalogos/modalidades" className={navLinkClass}>
                    <Layers className="sidebar__nav-icon" />
                    <span>Modalidades</span>
                  </NavLink>
                </li>
                <li className="sidebar__nav-item">
                  <NavLink to="/catalogos/tipos-actividad" className={navLinkClass}>
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

          {/* Grupo colapsable: Configuración */}
          <li className="sidebar__nav-item">
            <button
              type="button"
              onClick={toggleConfig}
              className={`sidebar__nav-link${
                isConfigActive ? ' sidebar__nav-link--active' : ''
              }`}
              style={{
                width: '100%',
                background: 'none',
                border: 'none',
                cursor: 'pointer',
                textAlign: 'left',
                justifyContent: 'flex-start',
              }}
              aria-expanded={configOpen}
            >
              <Settings className="sidebar__nav-icon" />
              <span style={{ flex: 1 }}>Configuración</span>
              {configOpen ? (
                <ChevronDown width={16} height={16} />
              ) : (
                <ChevronRight width={16} height={16} />
              )}
            </button>

            {configOpen && (
              <ul className="sidebar__nav" style={{ marginTop: 4, paddingLeft: 20 }}>
                <li className="sidebar__nav-item">
                  <NavLink to="/configuracion/numeracion" className={navLinkClass}>
                    <Hash className="sidebar__nav-icon" />
                    <span>Numeración</span>
                  </NavLink>
                </li>
              </ul>
            )}
          </li>
        </ul>
      </div>

      {/* Footer: bloque de usuario + cerrar sesión */}
      <div className="sidebar__footer">
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 'var(--spacing-3, 12px)',
            padding: 'var(--spacing-3, 12px)',
            marginBottom: 'var(--spacing-2, 8px)',
            background: 'var(--color-bg-subtle, rgba(0,0,0,0.03))',
            borderRadius: 'var(--radius-md, 8px)',
          }}
        >
          <div
            aria-hidden="true"
            style={{
              width: 36,
              height: 36,
              borderRadius: '50%',
              background: 'var(--color-primary, #2563eb)',
              color: '#fff',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontWeight: 700,
              flexShrink: 0,
            }}
          >
            {initial || <User width={18} height={18} />}
          </div>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div
              style={{
                fontSize: 'var(--font-size-sm, 0.875rem)',
                fontWeight: 600,
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}
              title={displayName}
            >
              {displayName}
            </div>
            {email && email !== displayName && (
              <div
                className="text-xs text-secondary"
                style={{
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                }}
                title={email}
              >
                {email}
              </div>
            )}
          </div>
        </div>

        <button
          type="button"
          onClick={handleLogout}
          className="sidebar__nav-link sidebar__nav-link--logout"
          style={{
            width: '100%',
            background: 'none',
            border: 'none',
            cursor: 'pointer',
            textAlign: 'left',
            justifyContent: 'flex-start',
          }}
        >
          <LogOut className="sidebar__nav-icon" />
          <span>Cerrar sesión</span>
        </button>
      </div>
    </nav>
  );
}
