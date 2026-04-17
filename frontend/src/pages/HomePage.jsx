/**
 * Página de bienvenida (placeholder).
 * Será reemplazada por el Dashboard de Capacitaciones en la fase 3.
 */
export default function HomePage() {
  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Bienvenido a Capacitaciones</h1>
          <p className="page-header__subtitle">
            Sistema de registro y gestión de capacitaciones.
          </p>
        </div>
      </div>

      <div className="card">
        <div className="card__body">
          <div className="empty-state">
            <div className="empty-state__title">Scaffolding listo</div>
            <p className="empty-state__description">
              Los módulos funcionales (catálogos, capacitaciones, configuración de
              numeración, inscripciones) se implementarán en las siguientes fases.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
