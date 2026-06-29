import { useState } from 'react';
import { Handshake, LayoutDashboard, History } from 'lucide-react';

/**
 * Entrenamiento → Convenios.
 *
 * Barra de pestañas (mismo patrón que Colaboradores):
 *  - Convenios: gestión de convenios (CRUD/listado) — por definir.
 *  - Dashboard: indicadores/resumen — por definir.
 *  - Historial por Colaborador: trazabilidad por persona — por definir.
 *
 * El contenido de cada pestaña se implementará a medida que se definan los requisitos.
 */

const TABS = [
  { id: 'convenios', label: 'Convenios', icon: Handshake },
  { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'historial', label: 'Historial por Colaborador', icon: History },
];

function Placeholder({ titulo }) {
  return (
    <div className="card">
      <div className="card__body">
        <div className="empty-state">
          <div className="empty-state__title">{titulo}</div>
          <p className="empty-state__description">
            Esta sección se implementará próximamente.
          </p>
        </div>
      </div>
    </div>
  );
}

export default function ConveniosPage() {
  const [tab, setTab] = useState('convenios');

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Convenios</h1>
          <p className="page-header__subtitle">
            Gestión de convenios, indicadores e historial por colaborador.
          </p>
        </div>
      </div>

      {/* Pestañas */}
      <div style={{ display: 'flex', gap: 'var(--spacing-2)', marginBottom: 'var(--spacing-4)', flexWrap: 'wrap' }}>
        {TABS.map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            type="button"
            className={`btn ${tab === id ? 'btn--primary' : 'btn--ghost'}`}
            onClick={() => setTab(id)}
            aria-pressed={tab === id}
          >
            <Icon width={16} height={16} />
            <span>{label}</span>
          </button>
        ))}
      </div>

      {tab === 'convenios' && <Placeholder titulo="Convenios" />}
      {tab === 'dashboard' && <Placeholder titulo="Dashboard" />}
      {tab === 'historial' && <Placeholder titulo="Historial por Colaborador" />}
    </div>
  );
}
