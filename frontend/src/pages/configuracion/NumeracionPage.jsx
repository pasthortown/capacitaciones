import { useState } from 'react';
import { GraduationCap, Handshake } from 'lucide-react';
import NumeracionCapacitacionesTab from './NumeracionCapacitacionesTab.jsx';
import NumeracionConveniosTab from './NumeracionConveniosTab.jsx';

/**
 * Configuración de numeración con dos pestañas: el contador de capacitaciones
 * (CAP-PC-REG-###) y el de convenios (GIC-EC-REG-###). Cada uno con su propio backend.
 */
export default function NumeracionPage() {
  const [tab, setTab] = useState('capacitaciones');

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Configuración de numeración</h1>
          <p className="page-header__subtitle">
            Contadores de código. Al crear una capacitación o un convenio, el backend
            toma y avanza el contador correspondiente en transacción.
          </p>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 'var(--spacing-2)', marginBottom: 'var(--spacing-4)', flexWrap: 'wrap' }}>
        {[
          { id: 'capacitaciones', label: 'Capacitaciones', icon: GraduationCap },
          { id: 'convenios', label: 'Convenios', icon: Handshake },
        ].map(({ id, label, icon: Icon }) => (
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

      {tab === 'capacitaciones' && <NumeracionCapacitacionesTab />}
      {tab === 'convenios' && <NumeracionConveniosTab />}
    </div>
  );
}
