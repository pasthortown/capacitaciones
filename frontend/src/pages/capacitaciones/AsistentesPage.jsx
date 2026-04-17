import { Link, useParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';

/**
 * Placeholder de la vista de asistentes por capacitación.
 * Fase 5: aquí se listarán los inscritos + descarga de certificado.
 */
export default function AsistentesPage() {
  const { id } = useParams();

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Asistentes</h1>
          <p className="page-header__subtitle">
            Capacitación: <code>{id}</code>
          </p>
        </div>
        <Link to="/capacitaciones" className="btn btn--secondary">
          <ArrowLeft width={16} height={16} />
          <span>Volver al listado</span>
        </Link>
      </div>

      <div className="card">
        <div className="card__body">
          <div className="empty-state">
            <div className="empty-state__title">Próximamente — Fase 5</div>
            <p className="empty-state__description">
              En la fase 5 se incorporará el listado de asistentes inscritos con
              descarga de certificado por fila (solo para capacitaciones finalizadas).
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
