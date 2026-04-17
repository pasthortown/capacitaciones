import { useCallback, useEffect, useState } from 'react';
import { Save, AlertTriangle } from 'lucide-react';
import configuracionService from '../../services/configuracion.js';
import TextField from '../../components/Forms/TextField.jsx';
import Modal from '../../components/Modal/Modal.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';

/**
 * Formatea un número entero como `CAP-PC-REG-###` (3 dígitos con ceros).
 */
function formatCodigo(numero, formato = 'CAP-PC-REG-###') {
  if (numero === null || numero === undefined || Number.isNaN(Number(numero))) {
    return formato;
  }
  const padded = String(Math.max(0, Math.min(999, Number(numero)))).padStart(3, '0');
  return formato.replace(/#+/, padded);
}

function formatFecha(iso) {
  if (!iso) return '—';
  try {
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) return iso;
    return date.toLocaleString('es-EC', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

export default function NumeracionPage() {
  const toast = useToast();

  const [loading, setLoading] = useState(true);
  const [data, setData] = useState(null); // { siguienteNumero, ultimaActualizacion, formato }
  const [inputValue, setInputValue] = useState('');
  const [inputError, setInputError] = useState('');
  const [saving, setSaving] = useState(false);

  // Confirmación para bajar el contador.
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [pendingValue, setPendingValue] = useState(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const response = await configuracionService.getNumeracion();
      setData(response);
      setInputValue(String(response?.siguienteNumero ?? ''));
      setInputError('');
    } catch (error) {
      toast.error(error?.message || 'No se pudo cargar la configuración.');
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const parseInput = () => {
    const trimmed = (inputValue || '').trim();
    if (!trimmed) {
      return { error: 'Ingresa un número entre 1 y 999.' };
    }
    const parsed = Number(trimmed);
    if (!Number.isInteger(parsed)) {
      return { error: 'El valor debe ser un entero.' };
    }
    if (parsed < 1 || parsed > 999) {
      return { error: 'El valor debe estar entre 1 y 999.' };
    }
    return { value: parsed };
  };

  const doSave = async (value) => {
    setSaving(true);
    try {
      await configuracionService.updateNumeracion(value);
      toast.success('Configuración actualizada.');
      await fetchData();
    } catch (error) {
      if (error instanceof HttpError && error.status === 400) {
        const backendMsg =
          error?.body?.message || error?.body?.title || error?.message;
        setInputError(backendMsg || 'Valor inválido.');
        toast.error(backendMsg || 'Valor inválido.');
      } else {
        toast.error(error?.message || 'No se pudo guardar.');
      }
    } finally {
      setSaving(false);
    }
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (saving || !data) return;

    const { value, error } = parseInput();
    if (error) {
      setInputError(error);
      return;
    }
    setInputError('');

    const current = Number(data.siguienteNumero);
    if (Number.isInteger(current) && value < current) {
      setPendingValue(value);
      setConfirmOpen(true);
      return;
    }

    await doSave(value);
  };

  const confirmDowngrade = async () => {
    const value = pendingValue;
    setConfirmOpen(false);
    setPendingValue(null);
    if (value !== null) {
      await doSave(value);
    }
  };

  const cancelDowngrade = () => {
    setConfirmOpen(false);
    setPendingValue(null);
  };

  if (loading) {
    return (
      <div style={{ padding: 'var(--spacing-6)', display: 'flex', justifyContent: 'center' }}>
        <Spinner size={32} label="Cargando configuración..." />
      </div>
    );
  }

  const formato = data?.formato || 'CAP-PC-REG-###';
  const proximoCodigo = formatCodigo(data?.siguienteNumero, formato);

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Configuración de numeración</h1>
          <p className="page-header__subtitle">
            Contador del código de capacitaciones. Al crear una nueva capacitación,
            el backend toma y avanza este contador en transacción.
          </p>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 'var(--spacing-4)' }}>
        <div className="card__body">
          <div style={{ display: 'grid', gap: 'var(--spacing-3)' }}>
            <div>
              <div className="text-xs text-secondary">Formato</div>
              <div style={{ fontFamily: 'var(--font-family-display)', fontSize: 'var(--font-size-lg)' }}>
                {formato}
              </div>
            </div>
            <div>
              <div className="text-xs text-secondary">Próximo código a asignar</div>
              <div
                style={{
                  fontFamily: 'var(--font-family-display)',
                  fontSize: 'var(--font-size-2xl)',
                  fontWeight: 700,
                  color: 'var(--color-primary)',
                }}
              >
                {proximoCodigo}
              </div>
            </div>
            <div>
              <div className="text-xs text-secondary">Última actualización</div>
              <div>{formatFecha(data?.ultimaActualizacion)}</div>
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card__header">
          <h3 className="card__title">Ajustar contador</h3>
          <p className="card__subtitle">
            Define el siguiente número que se asignará. Bajar el valor puede
            generar colisiones con capacitaciones ya emitidas.
          </p>
        </div>
        <div className="card__body">
          <form onSubmit={handleSubmit} noValidate>
            <div style={{ maxWidth: 280 }}>
              <TextField
                label="Siguiente número (1-999)"
                name="siguienteNumero"
                type="number"
                value={inputValue}
                onChange={setInputValue}
                placeholder="1"
                error={inputError}
                disabled={saving}
                required
              />
            </div>

            <div className="form-actions" style={{ marginTop: 'var(--spacing-4)' }}>
              <div className="form-actions__right">
                <button type="submit" className="btn btn--primary" disabled={saving}>
                  {saving ? (
                    <Spinner size={14} label="Guardando..." />
                  ) : (
                    <Save width={16} height={16} />
                  )}
                  <span style={{ marginLeft: 8 }}>{saving ? 'Guardando...' : 'Guardar'}</span>
                </button>
              </div>
            </div>
          </form>
        </div>
      </div>

      <Modal
        isOpen={confirmOpen}
        onClose={cancelDowngrade}
        title="Confirmar reducción del contador"
        footer={(
          <>
            <button type="button" className="btn btn--ghost" onClick={cancelDowngrade} disabled={saving}>
              Cancelar
            </button>
            <button type="button" className="btn btn--danger" onClick={confirmDowngrade} disabled={saving}>
              {saving ? 'Guardando...' : 'Continuar'}
            </button>
          </>
        )}
      >
        <div className="alert alert--warning" role="alert" style={{ marginBottom: 'var(--spacing-3)' }}>
          <AlertTriangle className="alert__icon" width={20} height={20} />
          <div className="alert__content">
            <div className="alert__title">Precaución</div>
            <div className="alert__message">
              Bajar el contador puede causar colisiones con capacitaciones existentes.
              ¿Deseas continuar?
            </div>
          </div>
        </div>
        <p>
          Valor actual: <strong>{data?.siguienteNumero}</strong><br />
          Nuevo valor: <strong>{pendingValue}</strong>
        </p>
      </Modal>
    </div>
  );
}
