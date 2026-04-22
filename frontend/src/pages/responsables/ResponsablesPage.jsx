import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Pencil, Trash2, Link2, Check, RotateCcw } from 'lucide-react';
import DataTable from '../../components/Table/DataTable.jsx';
import Modal from '../../components/Modal/Modal.jsx';
import TextField from '../../components/Forms/TextField.jsx';
import Toggle from '../../components/Forms/Toggle.jsx';
import SignaturePad from '../../components/SignaturePad/SignaturePad.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { formatFechaHora } from '../../utils/formatters.js';
import { buildPublicUrl } from '../../utils/urls.js';
import { confirm as swalConfirm } from '../../utils/swal.js';
import responsablesService from '../../services/responsables.js';
import styles from './ResponsablesPage.module.css';

/**
 * Pantalla admin CRUD del catálogo de Responsables.
 *
 * - Tabla con Nombres, Cargo, Empresa, Firma (✓/✗) y Estado.
 * - Toolbar: toggle "Mostrar inactivos" + botón "Nuevo responsable".
 * - Acciones por fila: copiar link firmado, editar, eliminar (baja lógica).
 * - Modal crear/editar con SignaturePad opcional (la firma también puede
 *   cargarla el responsable desde su link firmado).
 */
export default function ResponsablesPage() {
  const toast = useToast();

  // Lista -------------------------------------------------------------------
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [includeInactive, setIncludeInactive] = useState(false);

  // Modal CRUD --------------------------------------------------------------
  const [formOpen, setFormOpen] = useState(false);
  const [mode, setMode] = useState('create'); // 'create' | 'edit'
  const [editingId, setEditingId] = useState(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const [nombres, setNombres] = useState('');
  const [cargo, setCargo] = useState('');
  const [empresa, setEmpresa] = useState('');
  const [email, setEmail] = useState('');
  const [firma, setFirma] = useState(null);
  const [errors, setErrors] = useState({});

  // Copiar link -------------------------------------------------------------
  const [generandoLinkId, setGenerandoLinkId] = useState(null);

  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const fetchList = useCallback(
    async (flag) => {
      setLoading(true);
      try {
        const data = await responsablesService.list(flag);
        if (mountedRef.current) {
          setItems(Array.isArray(data) ? data : []);
        }
      } catch (err) {
        if (mountedRef.current) setItems([]);
        toast.error(err?.message || 'No se pudieron cargar los responsables.');
      } finally {
        if (mountedRef.current) setLoading(false);
      }
    },
    [toast],
  );

  useEffect(() => {
    fetchList(includeInactive);
  }, [fetchList, includeInactive]);

  const sortedItems = useMemo(() => {
    if (!Array.isArray(items)) return [];
    return [...items].sort((a, b) =>
      (a?.nombres || '').localeCompare(b?.nombres || '', 'es', { sensitivity: 'base' }),
    );
  }, [items]);

  // ---------- CRUD actions ----------
  const resetForm = () => {
    setNombres('');
    setCargo('');
    setEmpresa('');
    setEmail('');
    setFirma(null);
    setErrors({});
  };

  const openCreate = () => {
    resetForm();
    setMode('create');
    setEditingId(null);
    setFormOpen(true);
  };

  const openEdit = async (row) => {
    resetForm();
    setMode('edit');
    setEditingId(row.id);
    setNombres(row.nombres || '');
    setCargo(row.cargo || '');
    setEmpresa(row.empresa || '');
    setEmail(row.email || '');
    setFormOpen(true);

    // Fetch del detalle para obtener la firma base64
    setLoadingDetail(true);
    try {
      const detail = await responsablesService.get(row.id);
      if (mountedRef.current) {
        setNombres(detail.nombres || '');
        setCargo(detail.cargo || '');
        setEmpresa(detail.empresa || '');
        setEmail(detail.email || '');
        setFirma(detail.firma || null);
      }
    } catch (err) {
      toast.error(err?.message || 'No se pudo cargar el responsable.');
    } finally {
      if (mountedRef.current) setLoadingDetail(false);
    }
  };

  const closeForm = () => {
    if (submitting) return;
    setFormOpen(false);
  };

  const validate = () => {
    const next = {};
    if (!nombres.trim()) next.nombres = 'Los nombres son obligatorios.';
    else if (nombres.length > 255) next.nombres = 'Máximo 255 caracteres.';
    if (!cargo.trim()) next.cargo = 'El cargo es obligatorio.';
    else if (cargo.length > 255) next.cargo = 'Máximo 255 caracteres.';
    if (!empresa.trim()) next.empresa = 'La empresa es obligatoria.';
    else if (empresa.length > 255) next.empresa = 'Máximo 255 caracteres.';
    const emailTrim = email.trim();
    if (!emailTrim) {
      next.email = 'El correo es obligatorio.';
    } else if (emailTrim.length > 320) {
      next.email = 'Máximo 320 caracteres.';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(emailTrim)) {
      next.email = 'Formato de correo inválido.';
    }
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSubmit = async (event) => {
    event?.preventDefault?.();
    if (submitting) return;
    if (!validate()) return;

    const payload = {
      nombres: nombres.trim(),
      cargo: cargo.trim(),
      empresa: empresa.trim(),
      email: email.trim(),
      firma: firma || null,
    };

    setSubmitting(true);
    try {
      if (mode === 'edit' && editingId) {
        await responsablesService.update(editingId, payload);
        toast.success('Responsable actualizado.');
      } else {
        await responsablesService.create(payload);
        toast.success('Responsable creado.');
      }
      setFormOpen(false);
      await fetchList(includeInactive);
    } catch (err) {
      toast.error(err?.message || 'No se pudo guardar el responsable.');
    } finally {
      if (mountedRef.current) setSubmitting(false);
    }
  };

  const handleDelete = async (row) => {
    const confirmed = await swalConfirm({
      title: 'Eliminar responsable',
      text: `"${row.nombres}" se marcará como inactivo. Podrás reactivarlo luego.`,
      icon: 'warning',
      confirmText: 'Sí, eliminar',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmed) return;
    try {
      await responsablesService.del(row.id);
      toast.success('Responsable eliminado.');
      await fetchList(includeInactive);
    } catch (err) {
      toast.error(err?.message || 'No se pudo eliminar el responsable.');
    }
  };

  const handleReactivate = async (row) => {
    try {
      // El backend acepta `activo` opcional en el PUT — si llega true y la entidad
      // está inactiva, la reactiva. No tocamos firma (null = conservar).
      await responsablesService.update(row.id, {
        nombres: row.nombres,
        cargo: row.cargo,
        empresa: row.empresa,
        email: row.email,
        activo: true,
      });
      toast.success('Responsable reactivado.');
      await fetchList(includeInactive);
    } catch (err) {
      toast.error(err?.message || 'No se pudo reactivar el responsable.');
    }
  };

  // ---------- Copiar link firmado ----------
  const copyToClipboard = async (url) => {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(url);
      return;
    }
    const tmp = document.createElement('textarea');
    tmp.value = url;
    tmp.setAttribute('readonly', '');
    tmp.style.position = 'absolute';
    tmp.style.left = '-9999px';
    document.body.appendChild(tmp);
    tmp.select();
    document.execCommand('copy');
    document.body.removeChild(tmp);
  };

  const formatExpiresAt = (iso) => {
    if (!iso) return '';
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) return '';
    return formatFechaHora(date);
  };

  const handleCopyLink = async (row) => {
    if (!row?.id || generandoLinkId === row.id) return;
    setGenerandoLinkId(row.id);
    try {
      const { url, expiresAt } = await responsablesService.generateLink(row.id);
      const fullUrl = buildPublicUrl(url);
      await copyToClipboard(fullUrl);
      const fecha = formatExpiresAt(expiresAt);
      toast.success(
        fecha
          ? `Enlace copiado para ${row.nombres} (expira: ${fecha})`
          : `Enlace copiado para ${row.nombres}`,
      );
    } catch (err) {
      toast.error(err?.message || 'No se pudo generar el enlace.');
    } finally {
      if (mountedRef.current) setGenerandoLinkId(null);
    }
  };

  // ---------- Columnas ----------
  const columns = useMemo(
    () => [
      {
        key: 'nombres',
        header: 'Nombres',
        accessor: (row) => row?.nombres || '—',
      },
      {
        key: 'cargo',
        header: 'Cargo',
        accessor: (row) => row?.cargo || '—',
      },
      {
        key: 'empresa',
        header: 'Empresa',
        accessor: (row) => row?.empresa || '—',
      },
      {
        key: 'email',
        header: 'Correo',
        accessor: (row) => row?.email || '—',
      },
      {
        key: 'tieneFirma',
        header: 'Firma',
        width: '110px',
        align: 'center',
        accessor: (row) =>
          row?.tieneFirma ? (
            <span
              className={`${styles.signatureBadge} ${styles.signatureBadgeOk}`}
              title="Firma cargada"
            >
              <Check width={14} height={14} />
            </span>
          ) : (
            <span
              className={`${styles.signatureBadge} ${styles.signatureBadgeMissing}`}
              title="Sin firma"
            >
              ✗
            </span>
          ),
      },
      {
        key: 'activo',
        header: 'Estado',
        width: '120px',
        accessor: (row) => (
          <span className={`badge ${row?.activo ? 'badge--active' : 'badge--inactive'}`}>
            {row?.activo ? 'Activo' : 'Inactivo'}
          </span>
        ),
      },
    ],
    [],
  );

  return (
    <div>
      {/* Header */}
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Responsables</h1>
          <p className="page-header__subtitle">
            Catálogo global de firmantes. Se asignan por capacitación desde el modal.
          </p>
        </div>
      </div>

      {/* Toolbar */}
      <div className="toolbar">
        <div className="toolbar__filters">
          <Toggle
            label="Mostrar inactivos"
            checked={includeInactive}
            onChange={setIncludeInactive}
          />
        </div>
        <div className="toolbar__actions">
          <button type="button" className="btn btn--primary" onClick={openCreate}>
            <Plus width={16} height={16} />
            <span>Nuevo responsable</span>
          </button>
        </div>
      </div>

      {/* Tabla */}
      <div className="card">
        <div className="card__body" style={{ padding: 0 }}>
          <DataTable
            columns={columns}
            rows={sortedItems}
            loading={loading}
            emptyMessage="No hay responsables registrados."
            actions={(row) => (
              <>
                <button
                  type="button"
                  className="btn btn--ghost btn--sm btn--icon"
                  onClick={() => handleCopyLink(row)}
                  disabled={generandoLinkId === row.id}
                  aria-label={`Copiar enlace de ${row.nombres}`}
                  title="Copiar enlace firmado"
                >
                  <Link2 width={16} height={16} />
                </button>
                <button
                  type="button"
                  className="btn btn--ghost btn--sm btn--icon"
                  onClick={() => openEdit(row)}
                  aria-label={`Editar ${row.nombres}`}
                  title="Editar"
                >
                  <Pencil width={16} height={16} />
                </button>
                {row?.activo ? (
                  <button
                    type="button"
                    className="btn btn--ghost btn--sm btn--icon"
                    onClick={() => handleDelete(row)}
                    aria-label={`Eliminar ${row.nombres}`}
                    title="Eliminar (marcar como inactivo)"
                  >
                    <Trash2 width={16} height={16} />
                  </button>
                ) : (
                  <button
                    type="button"
                    className="btn btn--ghost btn--sm btn--icon"
                    onClick={() => handleReactivate(row)}
                    aria-label={`Reactivar ${row.nombres}`}
                    title="Reactivar"
                  >
                    <RotateCcw width={16} height={16} />
                  </button>
                )}
              </>
            )}
          />
        </div>
      </div>

      {/* Modal crear / editar */}
      <Modal
        isOpen={formOpen}
        onClose={closeForm}
        className={styles.wideModal}
        title={mode === 'edit' ? 'Editar responsable' : 'Nuevo responsable'}
        footer={
          <>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={closeForm}
              disabled={submitting}
            >
              Cancelar
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleSubmit}
              disabled={submitting || loadingDetail}
            >
              {submitting ? 'Guardando...' : 'Guardar'}
            </button>
          </>
        }
      >
        {loadingDetail ? (
          <div style={{ padding: 24, textAlign: 'center' }}>
            <Spinner size={32} label="Cargando responsable..." />
          </div>
        ) : (
          <form onSubmit={handleSubmit} noValidate>
            <div className={styles.note}>
              La firma la puede cargar el responsable desde su link firmado.
              También puedes subirla aquí si ya cuentas con ella.
            </div>

            <div className={styles.grid2}>
              <div className={styles.fullSpan}>
                <TextField
                  label="Nombres"
                  name="nombres"
                  value={nombres}
                  required
                  maxLength={255}
                  onChange={(v) => {
                    setNombres(v);
                    if (errors.nombres) setErrors((e) => ({ ...e, nombres: undefined }));
                  }}
                  error={errors.nombres}
                />
              </div>
              <TextField
                label="Cargo"
                name="cargo"
                value={cargo}
                required
                maxLength={255}
                onChange={(v) => {
                  setCargo(v);
                  if (errors.cargo) setErrors((e) => ({ ...e, cargo: undefined }));
                }}
                error={errors.cargo}
              />
              <TextField
                label="Empresa"
                name="empresa"
                value={empresa}
                required
                maxLength={255}
                onChange={(v) => {
                  setEmpresa(v);
                  if (errors.empresa) setErrors((e) => ({ ...e, empresa: undefined }));
                }}
                error={errors.empresa}
              />
              <div className={styles.fullSpan}>
                <TextField
                  label="Correo electrónico"
                  name="email"
                  type="email"
                  value={email}
                  required
                  maxLength={320}
                  onChange={(v) => {
                    setEmail(v);
                    if (errors.email) setErrors((e) => ({ ...e, email: undefined }));
                  }}
                  error={errors.email}
                />
              </div>
            </div>

            <div className={styles.signatureSection}>
              <label className={styles.smallLabel}>Firma (opcional)</label>
              <SignaturePad
                value={firma}
                onChange={setFirma}
                width={400}
                height={150}
              />
            </div>

            {/* Submit oculto para Enter */}
            <button type="submit" style={{ display: 'none' }} aria-hidden="true" />
          </form>
        )}
      </Modal>
    </div>
  );
}
