import { useCallback, useEffect, useState } from 'react';
import { Save, Send, Info, AlertTriangle } from 'lucide-react';
import configuracionService from '../../services/configuracion.js';
import TextField from '../../components/Forms/TextField.jsx';
import Toggle from '../../components/Forms/Toggle.jsx';
import Modal from '../../components/Modal/Modal.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import { resumirErrorSmtp } from './errorSmtp.js';

const EMPTY = {
  smtpHost: '', smtpPort: '587', smtpUser: '', password: '', quitarPassword: false,
  usarTls: true, remitenteCorreo: '', remitenteNombre: '', ccGlobal: '', bccGlobal: '',
};

function toForm(dto) {
  return {
    ...EMPTY,
    smtpHost: dto?.smtpHost || '',
    smtpPort: String(dto?.smtpPort ?? 587),
    smtpUser: dto?.smtpUser || '',
    usarTls: dto?.usarTls ?? true,
    remitenteCorreo: dto?.remitenteCorreo || '',
    remitenteNombre: dto?.remitenteNombre || '',
    ccGlobal: dto?.ccGlobal || '',
    bccGlobal: dto?.bccGlobal || '',
  };
}

function toPayload(form) {
  return {
    smtpHost: form.smtpHost.trim(),
    smtpPort: Number(form.smtpPort),
    smtpUser: form.smtpUser.trim() || null,
    password: form.quitarPassword ? null : (form.password || null),
    quitarPassword: form.quitarPassword,
    usarTls: form.usarTls,
    remitenteCorreo: form.remitenteCorreo.trim(),
    remitenteNombre: form.remitenteNombre.trim() || null,
    ccGlobal: form.ccGlobal.trim() || null,
    bccGlobal: form.bccGlobal.trim() || null,
  };
}

/** Mapea `errores` del backend (claves PascalCase del DTO) a las claves del formulario. */
function mapErrores(errores) {
  const out = {};
  Object.entries(errores || {}).forEach(([k, v]) => {
    out[k.charAt(0).toLowerCase() + k.slice(1)] = v;
  });
  return out;
}

/** Error del correo de prueba: resumen en español, sugerencia y detalle técnico desplegable. */
function ErrorPrueba({ mensaje }) {
  const { resumen, sugerencia, detalle } = resumirErrorSmtp(mensaje);
  return (
    <div className="alert__message">
      <div>{resumen}</div>
      {sugerencia && <div style={{ marginTop: 'var(--spacing-1)' }}>{sugerencia}</div>}
      {detalle && (
        <details style={{ marginTop: 'var(--spacing-2)' }}>
          <summary style={{ cursor: 'pointer' }}>Ver detalle</summary>
          <pre style={{
            whiteSpace: 'pre-wrap', wordBreak: 'break-all', maxHeight: 200, overflowY: 'auto',
            fontSize: 'var(--font-size-xs, 12px)', margin: 'var(--spacing-2) 0 0',
          }}>{detalle}</pre>
        </details>
      )}
    </div>
  );
}

/** Pestaña: servidor SMTP, remitente y copias globales. */
export default function CorreoServidorTab() {
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState(null);
  const [form, setForm] = useState(EMPTY);
  const [errors, setErrors] = useState({});
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState(null);
  const [confirmOpen, setConfirmOpen] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const response = await configuracionService.getCorreo();
      setData(response);
      setForm(toForm(response));
      setErrors({});
    } catch (error) {
      toast.error(error?.message || 'No se pudo cargar la configuración de correo.');
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const set = (field) => (value) => setForm((f) => ({ ...f, [field]: value }));

  const handleError = (error, fallback) => {
    if (error instanceof HttpError && error.status === 400) {
      setErrors(mapErrores(error?.body?.errores));
      toast.error(error?.body?.message || fallback);
    } else {
      toast.error(error?.body?.message || error?.message || fallback);
    }
  };

  const doSave = async () => {
    setConfirmOpen(false);
    setSaving(true);
    try {
      await configuracionService.updateCorreo(toPayload(form));
      toast.success('Configuración de correo guardada. Se aplica en menos de un minuto.');
      await fetchData();
    } catch (error) {
      handleError(error, 'No se pudo guardar la configuración.');
    } finally {
      setSaving(false);
    }
  };

  const doTest = async () => {
    setTesting(true);
    setTestResult(null);
    setErrors({});
    try {
      const result = await configuracionService.probarCorreo(toPayload(form));
      setTestResult(result);
    } catch (error) {
      handleError(error, 'No se pudo enviar el correo de prueba.');
    } finally {
      setTesting(false);
    }
  };

  if (loading) {
    return (
      <div style={{ padding: 'var(--spacing-6)', display: 'flex', justifyContent: 'center' }}>
        <Spinner size={32} label="Cargando configuración..." />
      </div>
    );
  }

  const busy = saving || testing;
  const passwordPlaceholder = data?.tienePassword ? '•••• (sin cambios)' : '';

  return (
    <div>
      {data && !data.configurado && (
        <div className="alert alert--info" role="status" style={{ marginBottom: 'var(--spacing-4)' }}>
          <Info className="alert__icon" width={20} height={20} />
          <div className="alert__content">
            <div className="alert__message">
              Actualmente se usa la configuración del servidor (.env). Al guardar, esta configuración la reemplaza.
            </div>
          </div>
        </div>
      )}
      {data?.passwordInvalida && (
        <div className="alert alert--warning" role="alert" style={{ marginBottom: 'var(--spacing-4)' }}>
          <AlertTriangle className="alert__icon" width={20} height={20} />
          <div className="alert__content">
            <div className="alert__message">
              La contraseña guardada no se puede leer. Vuelve a ingresarla; mientras tanto se usa la del servidor (.env).
            </div>
          </div>
        </div>
      )}

      <form onSubmit={(e) => { e.preventDefault(); if (!busy) setConfirmOpen(true); }} noValidate>
        <div className="card" style={{ marginBottom: 'var(--spacing-4)' }}>
          <div className="card__header">
            <h3 className="card__title">Servidor SMTP</h3>
          </div>
          <div className="card__body" style={{ display: 'grid', gap: 'var(--spacing-3)', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))' }}>
            <TextField label="Servidor" name="smtpHost" value={form.smtpHost} onChange={set('smtpHost')}
              placeholder="smtp.office365.com" error={errors.smtpHost} disabled={busy} required />
            <TextField label="Puerto" name="smtpPort" type="number" value={form.smtpPort} onChange={set('smtpPort')}
              placeholder="587" error={errors.smtpPort} disabled={busy} required />
            <TextField label="Usuario (opcional)" name="smtpUser" value={form.smtpUser} onChange={set('smtpUser')}
              helper="Si se deja vacío se usa el correo del remitente." error={errors.smtpUser} disabled={busy} />
            <TextField label="Contraseña" name="password" type="password" value={form.password} onChange={set('password')}
              placeholder={passwordPlaceholder} autoComplete="new-password"
              helper={data?.tienePassword ? 'Déjala vacía para conservar la actual (si no cambias servidor, puerto ni usuario).' : undefined}
              error={errors.password} disabled={busy || form.quitarPassword} />
            <Toggle label="Sin contraseña (relay)" name="quitarPassword" checked={form.quitarPassword}
              onChange={set('quitarPassword')} disabled={busy} />
            <Toggle label="Usar TLS (STARTTLS)" name="usarTls" checked={form.usarTls}
              onChange={set('usarTls')} disabled={busy} />
          </div>
        </div>

        <div className="card" style={{ marginBottom: 'var(--spacing-4)' }}>
          <div className="card__header">
            <h3 className="card__title">Remitente y copias</h3>
            <p className="card__subtitle">Las copias se agregan a todas las notificaciones. Separa varios correos con coma.</p>
          </div>
          <div className="card__body" style={{ display: 'grid', gap: 'var(--spacing-3)', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))' }}>
            <TextField label="Correo del remitente" name="remitenteCorreo" value={form.remitenteCorreo}
              onChange={set('remitenteCorreo')} placeholder="capacitaciones@dos.com.ec"
              error={errors.remitenteCorreo} disabled={busy} required />
            <TextField label="Nombre del remitente" name="remitenteNombre" value={form.remitenteNombre}
              onChange={set('remitenteNombre')} placeholder="CapacitaDOS" error={errors.remitenteNombre} disabled={busy} />
            <TextField label="CC global" name="ccGlobal" value={form.ccGlobal} onChange={set('ccGlobal')}
              placeholder="talento@dos.com.ec" error={errors.ccGlobal} disabled={busy} />
            <TextField label="BCC global" name="bccGlobal" value={form.bccGlobal} onChange={set('bccGlobal')}
              error={errors.bccGlobal} disabled={busy} />
          </div>
        </div>

        {testResult && (
          <div className={`alert ${testResult.ok ? 'alert--success' : 'alert--error'}`} role="status"
            style={{ marginBottom: 'var(--spacing-4)' }}>
            <div className="alert__content">
              <div className="alert__title">{testResult.ok ? 'Prueba exitosa' : 'La prueba falló'}</div>
              {testResult.ok ? (
                <div className="alert__message">{testResult.mensaje}</div>
              ) : (
                <ErrorPrueba mensaje={testResult.mensaje} />
              )}
            </div>
          </div>
        )}

        <div className="form-actions">
          <div className="form-actions__right" style={{ display: 'flex', gap: 'var(--spacing-2)' }}>
            <button type="button" className="btn btn--ghost" onClick={doTest} disabled={busy}>
              {testing ? <Spinner size={14} label="Enviando..." /> : <Send width={16} height={16} />}
              <span style={{ marginLeft: 8 }}>{testing ? 'Enviando...' : 'Enviar correo de prueba'}</span>
            </button>
            <button type="submit" className="btn btn--primary" disabled={busy}>
              {saving ? <Spinner size={14} label="Guardando..." /> : <Save width={16} height={16} />}
              <span style={{ marginLeft: 8 }}>{saving ? 'Guardando...' : 'Guardar'}</span>
            </button>
          </div>
        </div>
      </form>

      <Modal
        isOpen={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        title="Confirmar cambios de correo"
        footer={(
          <>
            <button type="button" className="btn btn--ghost" onClick={() => setConfirmOpen(false)}>Cancelar</button>
            <button type="button" className="btn btn--primary" onClick={doSave}>Guardar</button>
          </>
        )}
      >
        <p>
          Todas las notificaciones empezarán a salir con esta configuración en menos de un minuto.
          Te recomendamos enviar un correo de prueba antes de guardar.
        </p>
      </Modal>
    </div>
  );
}
