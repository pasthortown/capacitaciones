import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Save } from 'lucide-react';
import SignaturePad from '../../components/SignaturePad/SignaturePad.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import { getPerfil, updatePerfil } from '../../services/responsable.js';
import styles from './ResponsablePage.module.css';

/**
 * Pantalla pública (sin sidebar ni guard admin) del Responsable.
 *
 * Flujo (mismo patrón que CapacitadorPage):
 *  - Lee `?token=...` del querystring.
 *  - GET /responsable/perfil con ese Bearer → muestra los datos actuales
 *    y permite editar nombres, cargo, empresa y firma.
 *  - PUT /responsable/perfil al guardar. Firma OBLIGATORIA (backend la exige).
 *
 * Aislamiento: el servicio `responsable.js` usa fetch directo (no http.js),
 * así un 401 del responsable NO dispara `auth:expired` del admin.
 */
export default function ResponsablePage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const toast = useToast();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [data, setData] = useState(null);
  const [form, setForm] = useState({
    nombres: '',
    cargo: '',
    empresa: '',
    firma: null,
  });

  const hydrateFromData = useCallback((dto) => {
    setForm({
      nombres: dto?.nombres ?? '',
      cargo: dto?.cargo ?? '',
      empresa: dto?.empresa ?? '',
      firma: dto?.firma ?? null,
    });
  }, []);

  const mapError = (err) => {
    if (err instanceof HttpError) {
      if (err.status === 401 || err.status === 403 || err.status === 404) {
        return 'Enlace inválido o expirado. Solicita uno nuevo al administrador.';
      }
    }
    return err?.message || 'Ocurrió un error inesperado.';
  };

  useEffect(() => {
    if (!token) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError('');
    getPerfil(token)
      .then((dto) => {
        if (cancelled) return;
        setData(dto);
        hydrateFromData(dto);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(mapError(err));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [token, hydrateFromData]);

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (saving || !token) return;

    // Validaciones cliente (el backend es la fuente de verdad)
    const nombres = form.nombres?.trim();
    const cargo = form.cargo?.trim();
    const empresa = form.empresa?.trim();
    if (!nombres) {
      const m = 'Los nombres son obligatorios.';
      setError(m);
      toast.error(m);
      return;
    }
    if (!cargo) {
      const m = 'El cargo es obligatorio.';
      setError(m);
      toast.error(m);
      return;
    }
    if (!empresa) {
      const m = 'La empresa es obligatoria.';
      setError(m);
      toast.error(m);
      return;
    }
    if (!form.firma) {
      const m = 'La firma es obligatoria.';
      setError(m);
      toast.error(m);
      return;
    }

    setSaving(true);
    setError('');
    try {
      const payload = {
        nombres,
        cargo,
        empresa,
        firma: form.firma,
      };
      const updated = await updatePerfil(token, payload);
      setData(updated);
      hydrateFromData(updated);
      toast.success('Datos guardados');
    } catch (err) {
      const message = mapError(err);
      setError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  // --- Render: sin token ---
  if (!token) {
    return (
      <div className={styles.page}>
        <div className={`${styles.fullMessage} ${styles.alert} ${styles.alertError}`}>
          Enlace inválido. Solicita al administrador uno nuevo.
        </div>
      </div>
    );
  }

  // --- Render: cargando ---
  if (loading) {
    return (
      <div className={styles.page}>
        <div className={styles.loadingWrap}>
          <Spinner size={36} label="Cargando responsable..." />
        </div>
      </div>
    );
  }

  // --- Render: error de carga (sin data) ---
  if (error && !data) {
    return (
      <div className={styles.page}>
        <div className={`${styles.fullMessage} ${styles.alert} ${styles.alertError}`}>
          {error}
        </div>
      </div>
    );
  }

  // --- Render: OK ---
  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Responsable</h1>
        <p className={styles.subtitle}>
          Completa tus datos y carga tu firma. Estos datos se usarán en los
          certificados emitidos por las capacitaciones en las que figures como
          firmante.
        </p>
      </header>

      <div className={styles.container}>
        <section className={styles.card} aria-label="Mis datos">
          <h2 className={styles.sectionTitle}>Mis datos</h2>

          <form onSubmit={handleSubmit} noValidate>
            <div className={styles.twoCols}>
              <div className={styles.formRow}>
                <label className={styles.formLabel} htmlFor="nombres">
                  Nombres
                </label>
                <input
                  id="nombres"
                  type="text"
                  className="form-input"
                  value={form.nombres}
                  onChange={(e) =>
                    setForm((prev) => ({ ...prev, nombres: e.target.value }))
                  }
                  placeholder="Ej. Juan Pérez"
                  disabled={saving}
                  maxLength={255}
                  required
                />
              </div>
              <div className={styles.formRow}>
                <label className={styles.formLabel} htmlFor="cargo">
                  Cargo
                </label>
                <input
                  id="cargo"
                  type="text"
                  className="form-input"
                  value={form.cargo}
                  onChange={(e) =>
                    setForm((prev) => ({ ...prev, cargo: e.target.value }))
                  }
                  placeholder="Ej. Jefe de Seguridad Industrial"
                  disabled={saving}
                  maxLength={255}
                  required
                />
              </div>
            </div>

            <div className={styles.formRow}>
              <label className={styles.formLabel} htmlFor="empresa">
                Empresa
              </label>
              <input
                id="empresa"
                type="text"
                className="form-input"
                value={form.empresa}
                onChange={(e) =>
                  setForm((prev) => ({ ...prev, empresa: e.target.value }))
                }
                placeholder="Ej. DOS S.A."
                disabled={saving}
                maxLength={255}
                required
              />
            </div>

            <div className={styles.formRow}>
              <label className={styles.formLabel}>Firma</label>
              <SignaturePad
                value={form.firma}
                onChange={(dataUrl) =>
                  setForm((prev) => ({ ...prev, firma: dataUrl }))
                }
                width={400}
                height={150}
                disabled={saving}
              />
            </div>

            {error && (
              <div
                className={`${styles.alert} ${styles.alertError}`}
                role="alert"
                style={{ marginTop: 12 }}
              >
                {error}
              </div>
            )}

            <div className={styles.actions}>
              <button
                type="submit"
                className="btn btn--primary"
                disabled={saving}
              >
                {saving ? (
                  <Spinner size={16} label="Guardando..." />
                ) : (
                  <Save width={16} height={16} />
                )}
                <span>{saving ? 'Guardando...' : 'Guardar'}</span>
              </button>
            </div>
          </form>
        </section>
      </div>
    </div>
  );
}
