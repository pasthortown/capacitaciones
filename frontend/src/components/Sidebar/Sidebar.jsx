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
  UserCheck,
  FolderOpen,
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
 * - Footer: sólo botón "Cerrar sesión". El bloque del usuario autenticado
 *   se renderiza en el header superior (ver `AppLayout.jsx`), siguiendo el
 *   patrón `.header__user` del design system (style/example.html).
 *
 * Usa las clases `.sidebar*` del design system.
 */
export default function Sidebar({ collapsed = false }) {
  const location = useLocation();
  const navigate = useNavigate();
  const { logout } = useAuth();

  const isCatalogoActive = location.pathname.startsWith('/catalogos');
  const isConfigActive = location.pathname.startsWith('/configuracion');
  const isResponsablesActive = location.pathname.startsWith('/responsables');
  const isRepositorioActive = location.pathname.startsWith('/repositorio');
  // "Capacitaciones" se resalta cuando la ruta empieza con /capacitaciones
  // y NO con /catalogos. Como son prefijos disjuntos en el router, basta con
  // comprobar el primero. Se incluye también el home `/` que redirige a
  // /capacitaciones (por si la ruta no se resolvió aún).
  const isCapacitacionesActive =
    location.pathname === '/' ||
    location.pathname.startsWith('/capacitaciones');

  const [catalogosOpen, setCatalogosOpen] = useState(isCatalogoActive);
  const [configOpen, setConfigOpen] = useState(isConfigActive);

  // Si el sidebar está colapsado no tiene sentido abrir submenús; los
  // sub-ítems no caben y confundirían. Al colapsar los cerramos implícitamente.
  const submenusVisible = !collapsed;

  const toggleCatalogos = () => setCatalogosOpen((v) => !v);
  const toggleConfig = () => setConfigOpen((v) => !v);

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <nav className="sidebar">
      {/* Logo / branding. Cuando la sidebar está colapsada usamos la versión
          reducida (logo_min.png) para que el símbolo siga siendo visible sin
          desbordar el carril. */}
      <div className="sidebar__logo" style={{ textAlign: 'center' }}>
        <img
          src={`${import.meta.env.BASE_URL}${collapsed ? 'logo_min.png' : 'logo.png'}`}
          alt="Capacitados — tecnología con propósito"
          style={
            collapsed
              ? { width: 36, height: 36, objectFit: 'contain', display: 'block', margin: '0 auto' }
              : { width: '100%', maxWidth: 200, height: 'auto', display: 'block', margin: '0 auto' }
          }
        />
      </div>

      {/* Sección principal */}
      <div className="sidebar__section">
        <div className="sidebar__section-title">PRINCIPAL</div>
        <ul className="sidebar__nav">
          <li className="sidebar__nav-item">
            <NavLink
              to="/capacitaciones"
              title="Capacitaciones"
              className={() =>
                `sidebar__nav-link${
                  isCapacitacionesActive ? ' sidebar__nav-link--active' : ''
                }`
              }
            >
              <GraduationCap className="sidebar__nav-icon" />
              <span>Capacitaciones</span>
            </NavLink>
          </li>

          {/* Responsables (catálogo global) */}
          <li className="sidebar__nav-item">
            <NavLink
              to="/responsables"
              title="Responsables"
              className={() =>
                `sidebar__nav-link${
                  isResponsablesActive ? ' sidebar__nav-link--active' : ''
                }`
              }
            >
              <UserCheck className="sidebar__nav-icon" />
              <span>Responsables</span>
            </NavLink>
          </li>

          {/* Repositorio de recursos */}
          <li className="sidebar__nav-item">
            <NavLink
              to="/repositorio"
              title="Repositorio"
              className={() =>
                `sidebar__nav-link${
                  isRepositorioActive ? ' sidebar__nav-link--active' : ''
                }`
              }
            >
              <FolderOpen className="sidebar__nav-icon" />
              <span>Repositorio</span>
            </NavLink>
          </li>

          {/* Grupo colapsable: Catálogos. Cuando la sidebar está colapsada,
              el botón actúa como link directo al primer ítem (/catalogos/modalidades)
              para no dejar al usuario sin acceso. */}
          <li className="sidebar__nav-item">
            <button
              type="button"
              onClick={() => {
                if (collapsed) navigate('/catalogos/modalidades');
                else toggleCatalogos();
              }}
              title="Catálogos"
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
                <ChevronDown className="sidebar__nav-chevron" width={16} height={16} />
              ) : (
                <ChevronRight className="sidebar__nav-chevron" width={16} height={16} />
              )}
            </button>

            {submenusVisible && catalogosOpen && (
              <ul className="sidebar__nav" style={{ marginTop: 4, paddingLeft: 20 }}>
                <li className="sidebar__nav-item">
                  <NavLink to="/catalogos/modalidades" title="Modalidades" className={navLinkClass}>
                    <Layers className="sidebar__nav-icon" />
                    <span>Modalidades</span>
                  </NavLink>
                </li>
                <li className="sidebar__nav-item">
                  <NavLink to="/catalogos/tipos-actividad" title="Tipos de actividad" className={navLinkClass}>
                    <ListChecks className="sidebar__nav-icon" />
                    <span>Tipos de actividad</span>
                  </NavLink>
                </li>
                <li className="sidebar__nav-item">
                  <NavLink to="/catalogos/areas" title="Áreas" className={navLinkClass}>
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
              onClick={() => {
                if (collapsed) navigate('/configuracion/numeracion');
                else toggleConfig();
              }}
              title="Configuración"
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
                <ChevronDown className="sidebar__nav-chevron" width={16} height={16} />
              ) : (
                <ChevronRight className="sidebar__nav-chevron" width={16} height={16} />
              )}
            </button>

            {submenusVisible && configOpen && (
              <ul className="sidebar__nav" style={{ marginTop: 4, paddingLeft: 20 }}>
                <li className="sidebar__nav-item">
                  <NavLink to="/configuracion/numeracion" title="Numeración" className={navLinkClass}>
                    <Hash className="sidebar__nav-icon" />
                    <span>Numeración</span>
                  </NavLink>
                </li>
              </ul>
            )}
          </li>
        </ul>
      </div>

      {/* Footer: solo botón cerrar sesión (el bloque usuario vive en el header superior, ver AppLayout). */}
      <div className="sidebar__footer">
        <button
          type="button"
          onClick={handleLogout}
          title="Cerrar sesión"
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
