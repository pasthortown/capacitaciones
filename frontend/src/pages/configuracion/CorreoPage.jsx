import { useState } from 'react';
import { Server, BellRing } from 'lucide-react';
import CorreoServidorTab from './CorreoServidorTab.jsx';

/**
 * Configuración de correo: servidor/remitente/copias y reglas por tipo de notificación.
 * mail_sender toma los cambios en menos de un minuto (cache de 60 s).
 */
export default function CorreoPage() {
  const [tab, setTab] = useState('servidor');

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Configuración de correo</h1>
          <p className="page-header__subtitle">
            Cuenta desde la que salen las notificaciones, qué avisos se envían y con qué asunto.
          </p>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 'var(--spacing-2)', marginBottom: 'var(--spacing-4)', flexWrap: 'wrap' }}>
        {[
          { id: 'servidor', label: 'Servidor y remitente', icon: Server },
          { id: 'notificaciones', label: 'Notificaciones', icon: BellRing },
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

      {tab === 'servidor' && <CorreoServidorTab />}
      {tab === 'notificaciones' && <p className="text-secondary">Disponible en el siguiente paso.</p>}
    </div>
  );
}
