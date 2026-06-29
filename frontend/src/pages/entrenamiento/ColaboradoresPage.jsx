import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Pencil, Trash2, RotateCcw, Users, Building2 } from 'lucide-react';
import DataTable from '../../components/Table/DataTable.jsx';
import Modal from '../../components/Modal/Modal.jsx';
import TextField from '../../components/Forms/TextField.jsx';
import Toggle from '../../components/Forms/Toggle.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import { confirm as swalConfirm } from '../../utils/swal.js';
import colaboradoresService from '../../services/colaboradores.js';

/**
 * Entrenamiento → Colaboradores.
 *
 * Dos pestañas:
 *  - "DOS (ControlTareas)": colaboradores internos traídos del API de ControlTareas. Solo lectura
 *    (se administran en ese sistema).
 *  - "Externos": personas ajenas a DOS, administradas localmente con CRUD completo.
 *
 * Una cédula que ya exista en DOS no puede registrarse como externo (lo valida el backend).
 */

const SEXOS = ['MASCULINO', 'FEMENINO'];
const ESTADOS_CIVILES = ['SOLTERO/A', 'CASADO/A', 'DIVORCIADO/A', 'VIUDO/A', 'UNIÓN LIBRE'];

const emptyForm = {
  cedula: '',
  name: '',
  society: '',
  city: '',
  workArea: '',
  address: '',
  phone: '',
  sex: '',
  birthDate: '',
  province: '',
  maritalStatus: '',
  jobPosition: '',
  email: '',
};

const fechaCorta = (s) => (s ? String(s).slice(0, 10) : '—');

export default function ColaboradoresPage() {
  const toast = useToast();

  const [tab, setTab] = useState('dos'); // 'dos' | 'externos'
  const [buscar, setBuscar] = useState('');
  const [incluirInactivos, setIncluirInactivos] = useState(false);

  // DOS (ControlTareas)
  const [dosItems, setDosItems] = useState([]);
  const [dosDisponible, setDosDisponible] = useState(true);
  const [dosLoading, setDosLoading] = useState(false);

  // Externos (local)
  const [extItems, setExtItems] = useState([]);
  const [extLoading, setExtLoading] = useState(false);

  // Modal CRUD externo
  const [formOpen, setFormOpen] = useState(false);
  const [mode, setMode] = useState('create');
  const [editingId, setEditingId] = useState(null);
  const [form, setForm] = useState(emptyForm);
  const [errors, setErrors] = useState({});
  const [submitting, setSubmitting] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);

  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const fetchDos = useCallback(async () => {
    setDosLoading(true);
    try {
      const res = await colaboradoresService.listDos({ incluirInactivos });
      if (!mountedRef.current) return;
      setDosDisponible(res?.integracionDisponible !== false);
      setDosItems(Array.isArray(res?.items) ? res.items : []);
    } catch (err) {
      if (!mountedRef.current) return;
      setDosItems([]);
      toast.error(err?.message || 'No se pudieron cargar los colaboradores de DOS.');
    } finally {
      if (mountedRef.current) setDosLoading(false);
    }
  }, [incluirInactivos, toast]);

  const fetchExternos = useCallback(async () => {
    setExtLoading(true);
    try {
      const data = await colaboradoresService.listExternos({ incluirInactivos });
      if (!mountedRef.current) return;
      setExtItems(Array.isArray(data) ? data : []);
    } catch (err) {
      if (!mountedRef.current) return;
      setExtItems([]);
      toast.error(err?.message || 'No se pudieron cargar los colaboradores externos.');
    } finally {
      if (mountedRef.current) setExtLoading(false);
    }
  }, [incluirInactivos, toast]);

  useEffect(() => {
    if (tab === 'dos') fetchDos();
    else fetchExternos();
  }, [tab, fetchDos, fetchExternos]);

  // Filtro cliente (≥2 caracteres) sobre cualquier campo visible.
  const filtrar = (rows) => {
    const q = buscar.trim().toLowerCase();
    if (q.length < 2) return rows;
    return rows.filter((r) =>
      [r.cedula, r.name, r.jobPosition, r.workArea, r.society, r.city, r.phone, r.email]
        .some((v) => String(v ?? '').toLowerCase().includes(q)),
    );
  };

  const dosFiltrados = useMemo(() => filtrar(dosItems), [dosItems, buscar]);
  const extFiltrados = useMemo(() => filtrar(extItems), [extItems, buscar]);

  // ---------- Columnas compartidas ----------
  const baseColumns = useMemo(
    () => [
      { key: 'cedula', header: 'Cédula', accessor: (r) => r?.cedula || '—' },
      { key: 'name', header: 'Nombre', accessor: (r) => r?.name || '—' },
      { key: 'jobPosition', header: 'Cargo', accessor: (r) => r?.jobPosition || '—' },
      { key: 'workArea', header: 'Área', accessor: (r) => r?.workArea || '—' },
      { key: 'society', header: 'Sociedad', accessor: (r) => r?.society || '—' },
      { key: 'city', header: 'Ciudad', accessor: (r) => r?.city || '—' },
      { key: 'phone', header: 'Teléfono', accessor: (r) => r?.phone || '—' },
      { key: 'email', header: 'Correo', accessor: (r) => r?.email || '—' },
      {
        key: 'isActive',
        header: 'Estado',
        width: '110px',
        accessor: (r) => (
          <span className={`badge ${r?.isActive ? 'badge--active' : 'badge--inactive'}`}>
            {r?.isActive ? 'Activo' : 'Inactivo'}
          </span>
        ),
      },
    ],
    [],
  );

  // ---------- CRUD externos ----------
  const setField = (k, v) => {
    setForm((f) => ({ ...f, [k]: v }));
    if (errors[k]) setErrors((e) => ({ ...e, [k]: undefined }));
  };

  const openCreate = () => {
    setForm(emptyForm);
    setErrors({});
    setMode('create');
    setEditingId(null);
    setFormOpen(true);
  };

  const openEdit = async (row) => {
    setMode('edit');
    setEditingId(row.id);
    setErrors({});
    setForm({
      cedula: row.cedula || '',
      name: row.name || '',
      society: row.society || '',
      city: row.city || '',
      workArea: row.workArea || '',
      address: row.address || '',
      phone: row.phone || '',
      sex: row.sex || '',
      birthDate: fechaCorta(row.birthDate) === '—' ? '' : fechaCorta(row.birthDate),
      province: row.province || '',
      maritalStatus: row.maritalStatus || '',
      jobPosition: row.jobPosition || '',
      email: row.email || '',
    });
    setFormOpen(true);

    // Trae el detalle (por si la fila del listado no tuviera todos los campos).
    setLoadingDetail(true);
    try {
      const d = await colaboradoresService.getExterno(row.id);
      if (!mountedRef.current) return;
      setForm({
        cedula: d.cedula || '',
        name: d.name || '',
        society: d.society || '',
        city: d.city || '',
        workArea: d.workArea || '',
        address: d.address || '',
        phone: d.phone || '',
        sex: d.sex || '',
        birthDate: fechaCorta(d.birthDate) === '—' ? '' : fechaCorta(d.birthDate),
        province: d.province || '',
        maritalStatus: d.maritalStatus || '',
        jobPosition: d.jobPosition || '',
        email: d.email || '',
      });
    } catch {
      /* nos quedamos con los datos de la fila */
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
    const ced = form.cedula.trim();
    if (!ced) next.cedula = 'La cédula es obligatoria.';
    else if (ced.length > 20) next.cedula = 'Máximo 20 caracteres.';
    if (!form.name.trim()) next.name = 'El nombre es obligatorio.';
    const em = form.email.trim();
    if (em && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(em)) next.email = 'Correo inválido.';
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSubmit = async (e) => {
    e?.preventDefault?.();
    if (submitting) return;
    if (!validate()) return;

    const payload = {
      cedula: form.cedula.trim(),
      name: form.name.trim(),
      society: form.society.trim() || null,
      city: form.city.trim() || null,
      workArea: form.workArea.trim() || null,
      address: form.address.trim() || null,
      phone: form.phone.trim() || null,
      sex: form.sex || null,
      birthDate: form.birthDate || null,
      province: form.province.trim() || null,
      maritalStatus: form.maritalStatus || null,
      jobPosition: form.jobPosition.trim() || null,
      email: form.email.trim() || null,
    };

    setSubmitting(true);
    try {
      if (mode === 'edit' && editingId) {
        await colaboradoresService.updateExterno(editingId, payload);
        toast.success('Colaborador externo actualizado.');
      } else {
        await colaboradoresService.createExterno(payload);
        toast.success('Colaborador externo creado.');
      }
      setFormOpen(false);
      await fetchExternos();
    } catch (err) {
      const code = err instanceof HttpError ? err.body?.error : null;
      if (code === 'CEDULA_PERTENECE_A_DOS') {
        toast.error('Esa cédula pertenece a un colaborador de DOS (ControlTareas); no puede registrarse como externo.');
      } else if (code === 'CEDULA_DUPLICADA') {
        toast.error('Ya existe un colaborador externo con esa cédula.');
      } else if (code === 'CONTROLTAREAS_NO_DISPONIBLE') {
        toast.error('No se pudo verificar la cédula contra ControlTareas. Intenta de nuevo en unos minutos.');
      } else {
        toast.error(err?.message || 'No se pudo guardar el colaborador.');
      }
    } finally {
      if (mountedRef.current) setSubmitting(false);
    }
  };

  const handleDelete = async (row) => {
    const ok = await swalConfirm({
      title: 'Eliminar colaborador externo',
      text: `"${row.name}" se marcará como inactivo. Podrás reactivarlo luego.`,
      icon: 'warning',
      confirmText: 'Sí, eliminar',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!ok) return;
    try {
      await colaboradoresService.deleteExterno(row.id);
      toast.success('Colaborador externo eliminado.');
      await fetchExternos();
    } catch (err) {
      toast.error(err?.message || 'No se pudo eliminar.');
    }
  };

  const handleReactivate = async (row) => {
    try {
      await colaboradoresService.updateExterno(row.id, {
        cedula: row.cedula,
        name: row.name,
        society: row.society,
        city: row.city,
        workArea: row.workArea,
        address: row.address,
        phone: row.phone,
        sex: row.sex,
        birthDate: fechaCorta(row.birthDate) === '—' ? null : fechaCorta(row.birthDate),
        province: row.province,
        maritalStatus: row.maritalStatus,
        jobPosition: row.jobPosition,
        email: row.email,
        activo: true,
      });
      toast.success('Colaborador externo reactivado.');
      await fetchExternos();
    } catch (err) {
      toast.error(err?.message || 'No se pudo reactivar.');
    }
  };

  const tabBtn = (id, label, Icon) => (
    <button
      type="button"
      className={`btn ${tab === id ? 'btn--primary' : 'btn--ghost'}`}
      onClick={() => {
        setBuscar('');
        setTab(id);
      }}
      aria-pressed={tab === id}
    >
      <Icon width={16} height={16} />
      <span>{label}</span>
    </button>
  );

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Colaboradores</h1>
          <p className="page-header__subtitle">
            Colaboradores de DOS (desde ControlTareas, solo lectura) y externos administrados aquí.
          </p>
        </div>
      </div>

      {/* Pestañas */}
      <div style={{ display: 'flex', gap: 'var(--spacing-2)', marginBottom: 'var(--spacing-4)' }}>
        {tabBtn('dos', 'DOS (ControlTareas)', Building2)}
        {tabBtn('externos', 'Externos', Users)}
      </div>

      {/* Toolbar */}
      <div className="toolbar">
        <div className="toolbar__filters" style={{ display: 'flex', gap: 'var(--spacing-3)', alignItems: 'center' }}>
          <input
            className="form-input"
            placeholder="Filtrar… (mín. 2 caracteres)"
            value={buscar}
            onChange={(e) => setBuscar(e.target.value)}
            style={{ minWidth: 260 }}
          />
          <Toggle label="Mostrar inactivos" checked={incluirInactivos} onChange={setIncluirInactivos} />
        </div>
        <div className="toolbar__actions">
          {tab === 'externos' && (
            <button type="button" className="btn btn--primary" onClick={openCreate}>
              <Plus width={16} height={16} />
              <span>Nuevo externo</span>
            </button>
          )}
        </div>
      </div>

      {/* Aviso si la integración con ControlTareas está deshabilitada */}
      {tab === 'dos' && !dosDisponible && !dosLoading && (
        <div className="card" style={{ marginBottom: 'var(--spacing-4)' }}>
          <div className="card__body">
            <p className="text-secondary" style={{ margin: 0 }}>
              La integración con ControlTareas no está configurada (sin URL/credenciales). No se
              pueden mostrar los colaboradores de DOS por ahora.
            </p>
          </div>
        </div>
      )}

      {/* Tabla */}
      <div className="card">
        <div className="card__body" style={{ padding: 0 }}>
          {tab === 'dos' ? (
            <DataTable
              columns={baseColumns}
              rows={dosFiltrados}
              loading={dosLoading}
              emptyMessage="No hay colaboradores de DOS para mostrar."
            />
          ) : (
            <DataTable
              columns={baseColumns}
              rows={extFiltrados}
              loading={extLoading}
              emptyMessage="No hay colaboradores externos registrados."
              actions={(row) => (
                <>
                  <button
                    type="button"
                    className="btn btn--ghost btn--sm btn--icon"
                    onClick={() => openEdit(row)}
                    title="Editar"
                    aria-label={`Editar ${row.name}`}
                  >
                    <Pencil width={16} height={16} />
                  </button>
                  {row?.isActive ? (
                    <button
                      type="button"
                      className="btn btn--ghost btn--sm btn--icon"
                      onClick={() => handleDelete(row)}
                      title="Eliminar (marcar inactivo)"
                      aria-label={`Eliminar ${row.name}`}
                    >
                      <Trash2 width={16} height={16} />
                    </button>
                  ) : (
                    <button
                      type="button"
                      className="btn btn--ghost btn--sm btn--icon"
                      onClick={() => handleReactivate(row)}
                      title="Reactivar"
                      aria-label={`Reactivar ${row.name}`}
                    >
                      <RotateCcw width={16} height={16} />
                    </button>
                  )}
                </>
              )}
            />
          )}
        </div>
      </div>

      {/* Modal crear/editar externo */}
      <Modal
        isOpen={formOpen}
        onClose={closeForm}
        title={mode === 'edit' ? 'Editar colaborador externo' : 'Nuevo colaborador externo'}
        footer={
          <>
            <button type="button" className="btn btn--secondary" onClick={closeForm} disabled={submitting}>
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
            <Spinner size={32} label="Cargando colaborador..." />
          </div>
        ) : (
          <form onSubmit={handleSubmit} noValidate>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--spacing-3)' }}>
              <TextField
                label="Cédula / Identificación"
                name="cedula"
                value={form.cedula}
                required
                maxLength={20}
                disabled={mode === 'edit'}
                onChange={(v) => setField('cedula', v)}
                error={errors.cedula}
                helper={mode === 'edit' ? 'La cédula no se puede cambiar.' : undefined}
              />
              <TextField
                label="Nombre completo"
                name="name"
                value={form.name}
                required
                maxLength={200}
                onChange={(v) => setField('name', v)}
                error={errors.name}
              />
              <TextField label="Cargo" name="jobPosition" value={form.jobPosition} maxLength={150} onChange={(v) => setField('jobPosition', v)} />
              <TextField label="Área de trabajo" name="workArea" value={form.workArea} maxLength={100} onChange={(v) => setField('workArea', v)} />
              <TextField label="Sociedad / Empresa" name="society" value={form.society} maxLength={150} onChange={(v) => setField('society', v)} />
              <TextField label="Correo" name="email" type="email" value={form.email} maxLength={200} onChange={(v) => setField('email', v)} error={errors.email} />
              <TextField label="Teléfono" name="phone" value={form.phone} maxLength={50} onChange={(v) => setField('phone', v)} />

              <div className="form-group">
                <label className="form-label" htmlFor="col-sex">Sexo</label>
                <select id="col-sex" className="form-input" value={form.sex} onChange={(e) => setField('sex', e.target.value)}>
                  <option value="">—</option>
                  {SEXOS.map((s) => <option key={s} value={s}>{s}</option>)}
                </select>
              </div>

              <div className="form-group">
                <label className="form-label" htmlFor="col-civil">Estado civil</label>
                <select id="col-civil" className="form-input" value={form.maritalStatus} onChange={(e) => setField('maritalStatus', e.target.value)}>
                  <option value="">—</option>
                  {ESTADOS_CIVILES.map((s) => <option key={s} value={s}>{s}</option>)}
                </select>
              </div>

              <div className="form-group">
                <label className="form-label" htmlFor="col-birth">Fecha de nacimiento</label>
                <input id="col-birth" type="date" className="form-input" value={form.birthDate} onChange={(e) => setField('birthDate', e.target.value)} />
              </div>

              <TextField label="Provincia" name="province" value={form.province} maxLength={100} onChange={(v) => setField('province', v)} />
              <TextField label="Ciudad" name="city" value={form.city} maxLength={100} onChange={(v) => setField('city', v)} />
              <TextField label="Dirección" name="address" value={form.address} maxLength={300} onChange={(v) => setField('address', v)} />
            </div>
            <button type="submit" style={{ display: 'none' }} aria-hidden="true" />
          </form>
        )}
      </Modal>
    </div>
  );
}
