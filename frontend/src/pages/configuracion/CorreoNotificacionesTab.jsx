import { useCallback, useEffect, useState } from 'react';
import { Save } from 'lucide-react';
import configuracionService from '../../services/configuracion.js';
import Toggle from '../../components/Forms/Toggle.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';

/** Pestaña: activar/desactivar cada notificación y personalizar su asunto. */
export default function CorreoNotificacionesTab() {
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [items, setItems] = useState([]);
  const [saving, setSaving] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const response = await configuracionService.getNotificaciones();
      setItems((response || []).map((n) => ({ ...n, asuntoPersonalizado: n.asuntoPersonalizado || '' })));
    } catch (error) {
      toast.error(error?.message || 'No se pudieron cargar las notificaciones.');
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const update = (plantilla, patch) =>
    setItems((list) => list.map((n) => (n.plantilla === plantilla ? { ...n, ...patch } : n)));

  const doSave = async () => {
    setSaving(true);
    try {
      await configuracionService.updateNotificaciones(items.map((n) => ({
        plantilla: n.plantilla,
        activo: n.activo,
        asuntoPersonalizado: n.asuntoPersonalizado.trim() || null,
      })));
      toast.success('Notificaciones actualizadas. Se aplican en menos de un minuto.');
      await fetchData();
    } catch (error) {
      toast.error(error?.body?.message || error?.message || 'No se pudieron guardar las notificaciones.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div style={{ padding: 'var(--spacing-6)', display: 'flex', justifyContent: 'center' }}>
        <Spinner size={32} label="Cargando notificaciones..." />
      </div>
    );
  }

  return (
    <div className="card">
      <div className="card__header">
        <h3 className="card__title">Notificaciones</h3>
        <p className="card__subtitle">
          Un aviso desactivado no se envía (y no se reenvía al reactivarlo). Deja el asunto vacío para usar el de
          siempre; puedes usar variables como <code>{'{{ tema }}'}</code>.
        </p>
      </div>
      <div className="card__body" style={{ display: 'grid', gap: 'var(--spacing-4)' }}>
        {items.map((n) => (
          <div key={n.plantilla} style={{ display: 'grid', gap: 'var(--spacing-2)', paddingBottom: 'var(--spacing-3)', borderBottom: '1px solid var(--color-border, #e5e7eb)' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 'var(--spacing-3)' }}>
              <strong>{n.nombre}</strong>
              <Toggle
                label=""
                ariaLabel={`Activar ${n.nombre}`}
                name={`activo-${n.plantilla}`}
                checked={n.activo}
                onChange={(value) => update(n.plantilla, { activo: value })}
                disabled={saving}
              />
            </div>
            <input
              className="form-input"
              aria-label={`Asunto de ${n.nombre}`}
              value={n.asuntoPersonalizado}
              onChange={(e) => update(n.plantilla, { asuntoPersonalizado: e.target.value })}
              placeholder={n.asuntoActual}
              maxLength={500}
              disabled={saving || !n.activo}
            />
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              {n.variables.map((v) => (
                <code key={v} className="text-xs" style={{ background: 'var(--color-bg-main, #f3f4f6)', padding: '2px 6px', borderRadius: 4 }}>
                  {`{{ ${v} }}`}
                </code>
              ))}
            </div>
          </div>
        ))}
        <div className="form-actions">
          <div className="form-actions__right">
            <button type="button" className="btn btn--primary" onClick={doSave} disabled={saving}>
              {saving ? <Spinner size={14} label="Guardando..." /> : <Save width={16} height={16} />}
              <span style={{ marginLeft: 8 }}>{saving ? 'Guardando...' : 'Guardar cambios'}</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
