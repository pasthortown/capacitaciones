import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Handshake, LayoutDashboard, History, Plus, Pencil, Trash2, RotateCcw,
  Download, Search, Trash, UploadCloud,
} from 'lucide-react';
import DataTable from '../../components/Table/DataTable.jsx';
import Modal from '../../components/Modal/Modal.jsx';
import TextField from '../../components/Forms/TextField.jsx';
import Toggle from '../../components/Forms/Toggle.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import { confirm as swalConfirm } from '../../utils/swal.js';
import conveniosService from '../../services/convenios.js';
import styles from './ConveniosPage.module.css';

/**
 * Entrenamiento → Convenios. Tres pestañas:
 *  - Convenios: CRUD (búsqueda + inactivos + "Nuevo Convenio"); ítems de costo + devengo + anexo firmado.
 *  - Dashboard: indicadores (por definir).
 *  - Historial por Colaborador: filtra por cédula los convenios vigentes y sus montos pendientes.
 */

const MESES_OPCIONES = [
  { value: 0, label: 'No aplica' },
  { value: 12, label: '12 meses' },
  { value: 24, label: '24 meses' },
  { value: 36, label: '36 meses' },
];

const money = (n) =>
  n == null ? '—' : new Intl.NumberFormat('es-EC', { style: 'currency', currency: 'USD' }).format(Number(n));
const fechaCorta = (s) => (s ? String(s).slice(0, 10) : '—');

const ESTADOS = ['Vigente', 'Devengado', 'Cobrado', 'Anulado'];

const emptyForm = {
  cedula: '', nombreColaborador: '', origenColaborador: '', cargoColaborador: '', areaColaborador: '',
  titulo: '', descripcion: '', tipo: '', tipoCurso: '', nombreCurso: '', marca: '',
  solicitadoPor: '', autorizadoPor: '',
  fecha: '', mesesADevengar: 0, estado: 'Vigente',
  items: [],
};

const emptyItem = { tipo: '', valor: '', devengable: true, observacion: '' };

export default function ConveniosPage() {
  const toast = useToast();
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

      <div style={{ display: 'flex', gap: 'var(--spacing-2)', marginBottom: 'var(--spacing-4)', flexWrap: 'wrap' }}>
        {[
          { id: 'convenios', label: 'Convenios', icon: Handshake },
          { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
          { id: 'historial', label: 'Historial por Colaborador', icon: History },
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

      {tab === 'convenios' && <ConveniosTab toast={toast} />}
      {tab === 'historial' && <HistorialTab toast={toast} />}
      {tab === 'dashboard' && (
        <div className="card">
          <div className="card__body">
            <div className="empty-state">
              <div className="empty-state__title">Dashboard</div>
              <p className="empty-state__description">Indicadores de convenios — por definir.</p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

/* ----------------------------- Pestaña Convenios ----------------------------- */
function ConveniosTab({ toast }) {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [buscar, setBuscar] = useState('');
  const [incluirInactivos, setIncluirInactivos] = useState(false);

  const [formOpen, setFormOpen] = useState(false);
  const [mode, setMode] = useState('create');
  const [editing, setEditing] = useState(null); // convenio en edición (dto)
  const [form, setForm] = useState(emptyForm);
  const [errors, setErrors] = useState({});
  const [submitting, setSubmitting] = useState(false);
  const [buscandoColab, setBuscandoColab] = useState(false);
  const [anexoBusy, setAnexoBusy] = useState(false);
  const [uploadPct, setUploadPct] = useState(0);
  const [uploadName, setUploadName] = useState('');
  const [dragOver, setDragOver] = useState(false);
  const fileRef = useRef(null);

  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => { mountedRef.current = false; };
  }, []);

  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const data = await conveniosService.list({ incluirInactivos });
      if (mountedRef.current) setItems(Array.isArray(data) ? data : []);
    } catch (err) {
      if (mountedRef.current) { setItems([]); toast.error(err?.message || 'No se pudieron cargar los convenios.'); }
    } finally {
      if (mountedRef.current) setLoading(false);
    }
  }, [incluirInactivos, toast]);

  useEffect(() => { fetchList(); }, [fetchList]);

  const filtered = useMemo(() => {
    const q = buscar.trim().toLowerCase();
    if (q.length < 2) return items;
    return items.filter((r) =>
      [r.nombreColaborador, r.cedulaColaborador, r.titulo, r.tipo, r.nombreCurso, r.marca]
        .some((v) => String(v ?? '').toLowerCase().includes(q)));
  }, [items, buscar]);

  const setField = (k, v) => {
    setForm((f) => ({ ...f, [k]: v }));
    if (errors[k]) setErrors((e) => ({ ...e, [k]: undefined }));
  };

  const openCreate = () => {
    setForm({ ...emptyForm, items: [] });
    setErrors({});
    setMode('create');
    setEditing(null);
    setFormOpen(true);
  };

  const openEdit = (row) => {
    setMode('edit');
    setEditing(row);
    setErrors({});
    setForm({
      cedula: row.cedulaColaborador || '',
      nombreColaborador: row.nombreColaborador || '',
      origenColaborador: row.origenColaborador || '',
      cargoColaborador: row.cargoColaborador || '',
      areaColaborador: row.areaColaborador || '',
      titulo: row.titulo || '',
      descripcion: row.descripcion || '',
      tipo: row.tipo || '',
      tipoCurso: row.tipoCurso || '',
      nombreCurso: row.nombreCurso || '',
      marca: row.marca || '',
      solicitadoPor: row.solicitadoPor || '',
      autorizadoPor: row.autorizadoPor || '',
      fecha: fechaCorta(row.fecha) === '—' ? '' : fechaCorta(row.fecha),
      mesesADevengar: row.mesesADevengar ?? 0,
      estado: row.estado || 'Vigente',
      items: (row.items || []).map((i) => ({
        tipo: i.tipo || '', valor: String(i.valor ?? ''), devengable: i.devengable !== false, observacion: i.observacion || '',
      })),
    });
    setFormOpen(true);
  };

  const closeForm = () => { if (!submitting && !anexoBusy) setFormOpen(false); };

  const buscarColaborador = async () => {
    const ced = form.cedula.trim();
    if (!ced) { setErrors((e) => ({ ...e, cedula: 'Ingresa la cédula.' })); return; }
    setBuscandoColab(true);
    try {
      const r = await conveniosService.buscarColaborador(ced);
      if (!mountedRef.current) return;
      if (!r) {
        setForm((f) => ({ ...f, nombreColaborador: '', origenColaborador: '' }));
        setErrors((e) => ({ ...e, cedula: 'No existe un colaborador (DOS ni externo) con esa cédula.' }));
      } else {
        setForm((f) => ({ ...f, nombreColaborador: r.name, origenColaborador: r.origen }));
        setErrors((e) => ({ ...e, cedula: undefined }));
      }
    } catch (err) {
      toast.error(err?.message || 'No se pudo buscar el colaborador.');
    } finally {
      if (mountedRef.current) setBuscandoColab(false);
    }
  };

  // ----- Ítems de costo -----
  const addItem = () => setForm((f) => ({ ...f, items: [...f.items, { ...emptyItem }] }));
  const removeItem = (idx) => setForm((f) => ({ ...f, items: f.items.filter((_, i) => i !== idx) }));
  const setItem = (idx, k, v) =>
    setForm((f) => ({ ...f, items: f.items.map((it, i) => (i === idx ? { ...it, [k]: v } : it)) }));

  const totalItems = useMemo(
    () => form.items.reduce((s, it) => s + (Number(it.valor) || 0), 0), [form.items]);
  const totalDevengable = useMemo(
    () => form.items.filter((it) => it.devengable).reduce((s, it) => s + (Number(it.valor) || 0), 0), [form.items]);

  const validate = () => {
    const next = {};
    if (!form.cedula.trim()) next.cedula = 'La cédula es obligatoria.';
    else if (!form.nombreColaborador) next.cedula = 'Busca y valida el colaborador.';
    if (!form.titulo.trim()) next.titulo = 'El título es obligatorio.';
    if (!form.fecha) next.fecha = 'La fecha es obligatoria.';
    for (const it of form.items) {
      if (!String(it.tipo).trim() || it.valor === '' || Number.isNaN(Number(it.valor))) {
        next.items = 'Cada ítem requiere tipo y valor numérico.';
        break;
      }
    }
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const buildPayload = () => ({
    cedula: form.cedula.trim(),
    titulo: form.titulo.trim(),
    descripcion: form.descripcion.trim() || null,
    tipo: form.tipo.trim() || null,
    tipoCurso: form.tipoCurso.trim() || null,
    nombreCurso: form.nombreCurso.trim() || null,
    marca: form.marca.trim() || null,
    solicitadoPor: form.solicitadoPor.trim() || null,
    autorizadoPor: form.autorizadoPor.trim() || null,
    fecha: form.fecha,
    mesesADevengar: Number(form.mesesADevengar) || 0,
    estado: form.estado || 'Vigente',
    items: form.items.map((it) => ({
      tipo: it.tipo.trim(),
      valor: Number(it.valor) || 0,
      devengable: !!it.devengable,
      observacion: it.observacion.trim() || null,
    })),
  });

  const handleSubmit = async (e) => {
    e?.preventDefault?.();
    if (submitting) return;
    if (!validate()) return;
    setSubmitting(true);
    try {
      if (mode === 'edit' && editing) {
        const updated = await conveniosService.update(editing.id, buildPayload());
        toast.success('Convenio actualizado.');
        setEditing(updated);
        await fetchList();
      } else {
        const created = await conveniosService.create(buildPayload());
        toast.success('Convenio creado. Ahora puedes adjuntar el convenio firmado.');
        // Pasamos a modo edición del recién creado para permitir adjuntar el anexo.
        setMode('edit');
        setEditing(created);
        await fetchList();
      }
    } catch (err) {
      const code = err instanceof HttpError ? err.body?.error : null;
      if (code === 'COLABORADOR_NO_ENCONTRADO') toast.error('La cédula no corresponde a ningún colaborador.');
      else toast.error(err?.message || 'No se pudo guardar el convenio.');
    } finally {
      if (mountedRef.current) setSubmitting(false);
    }
  };

  const handleDelete = async (row) => {
    const ok = await swalConfirm({
      title: 'Eliminar convenio',
      text: `El convenio "${row.titulo}" se marcará como inactivo.`,
      icon: 'warning', confirmText: 'Sí, eliminar', cancelText: 'Cancelar', danger: true,
    });
    if (!ok) return;
    try { await conveniosService.remove(row.id); toast.success('Convenio eliminado.'); await fetchList(); }
    catch (err) { toast.error(err?.message || 'No se pudo eliminar.'); }
  };

  const handleReactivate = async (row) => {
    try {
      await conveniosService.update(row.id, {
        cedula: row.cedulaColaborador, titulo: row.titulo, descripcion: row.descripcion,
        tipo: row.tipo, tipoCurso: row.tipoCurso, nombreCurso: row.nombreCurso, marca: row.marca,
        solicitadoPor: row.solicitadoPor, autorizadoPor: row.autorizadoPor,
        fecha: fechaCorta(row.fecha), mesesADevengar: row.mesesADevengar, estado: row.estado,
        items: (row.items || []).map((i) => ({ tipo: i.tipo, valor: i.valor, devengable: i.devengable, observacion: i.observacion })),
        activo: true,
      });
      toast.success('Convenio reactivado.'); await fetchList();
    } catch (err) { toast.error(err?.message || 'No se pudo reactivar.'); }
  };

  const handleDescargarAnexo = async (convenioId, anexo) => {
    try { await conveniosService.descargarAnexo(convenioId, anexo.id, anexo.nombreOriginal || `anexo-${anexo.id}`); }
    catch (err) { toast.error(err?.message || 'No se pudo descargar el anexo.'); }
  };

  // ----- Anexos múltiples dentro del modal (solo en edición) -----
  const subirAnexo = async (file) => {
    if (!file || !editing) return;
    setAnexoBusy(true);
    setUploadName(file.name);
    setUploadPct(0);
    try {
      const updated = await conveniosService.subirAnexo(editing.id, file, (pct) => {
        if (mountedRef.current) setUploadPct(pct);
      });
      if (updated) setEditing(updated);
      toast.success('Anexo cargado.');
      await fetchList();
    } catch (err) {
      toast.error(err?.message || 'No se pudo cargar el anexo.');
    } finally {
      if (mountedRef.current) { setAnexoBusy(false); setUploadName(''); setUploadPct(0); }
      if (fileRef.current) fileRef.current.value = '';
    }
  };

  const eliminarAnexo = async (anexoId) => {
    if (!editing) return;
    setAnexoBusy(true);
    try {
      await conveniosService.eliminarAnexo(editing.id, anexoId);
      const updated = await conveniosService.get(editing.id);
      setEditing(updated);
      toast.success('Anexo eliminado.');
      await fetchList();
    } catch (err) {
      toast.error(err?.message || 'No se pudo eliminar el anexo.');
    } finally {
      if (mountedRef.current) setAnexoBusy(false);
    }
  };

  const columns = useMemo(() => [
    {
      key: 'colaborador', header: 'Colaborador',
      accessor: (r) => (
        <div>
          <div>{r.nombreColaborador}</div>
          <div className="text-secondary" style={{ fontSize: 12 }}>{r.cedulaColaborador} · {r.origenColaborador}</div>
        </div>
      ),
    },
    { key: 'titulo', header: 'Título', accessor: (r) => r.titulo || '—' },
    { key: 'nombreCurso', header: 'Curso', accessor: (r) => r.nombreCurso || '—' },
    { key: 'fecha', header: 'Fecha', accessor: (r) => fechaCorta(r.fecha) },
    { key: 'mesesADevengar', header: 'Meses', align: 'center', accessor: (r) => (r.mesesADevengar ? r.mesesADevengar : 'N/A') },
    { key: 'montoTotal', header: 'Monto total', accessor: (r) => money(r.montoTotal) },
    {
      key: 'montoPendiente', header: 'Pendiente',
      accessor: (r) => (r.estado === 'Cobrado' ? `${money(r.montoCobrado)} (cobrado)` : money(r.montoPendiente)),
    },
    {
      key: 'estado', header: 'Estado', width: '110px',
      accessor: (r) => {
        const cls = r.estado === 'Vigente' ? 'badge--active'
          : r.estado === 'Anulado' ? 'badge--inactive' : '';
        return <span className={`badge ${cls}`}>{r.estado}</span>;
      },
    },
    {
      key: 'anexos', header: 'Anexos', align: 'center', width: '80px',
      accessor: (r) => (r.anexos?.length ? <span className="badge">{r.anexos.length}</span> : '—'),
    },
  ], []);

  return (
    <>
      <div className="toolbar">
        <div className="toolbar__filters" style={{ display: 'flex', gap: 'var(--spacing-3)', alignItems: 'center' }}>
          <input className="form-input" placeholder="Filtrar… (mín. 2 caracteres)" value={buscar}
            onChange={(e) => setBuscar(e.target.value)} style={{ minWidth: 260 }} />
          <Toggle label="Mostrar inactivos" checked={incluirInactivos} onChange={setIncluirInactivos} />
        </div>
        <div className="toolbar__actions">
          <button type="button" className="btn btn--primary" onClick={openCreate}>
            <Plus width={16} height={16} /><span>Nuevo Convenio</span>
          </button>
        </div>
      </div>

      <div className="card">
        <div className="card__body" style={{ padding: 0 }}>
          <DataTable
            columns={columns}
            rows={filtered}
            loading={loading}
            emptyMessage="No hay convenios registrados."
            actions={(row) => (
              <>
                <button type="button" className="btn btn--ghost btn--sm btn--icon"
                  onClick={() => openEdit(row)} title="Editar / anexos" aria-label="Editar">
                  <Pencil width={16} height={16} />
                </button>
                {row.activo ? (
                  <button type="button" className="btn btn--ghost btn--sm btn--icon"
                    onClick={() => handleDelete(row)} title="Eliminar" aria-label="Eliminar">
                    <Trash2 width={16} height={16} />
                  </button>
                ) : (
                  <button type="button" className="btn btn--ghost btn--sm btn--icon"
                    onClick={() => handleReactivate(row)} title="Reactivar" aria-label="Reactivar">
                    <RotateCcw width={16} height={16} />
                  </button>
                )}
              </>
            )}
          />
        </div>
      </div>

      <Modal
        isOpen={formOpen}
        onClose={closeForm}
        className={styles.wideModal}
        title={mode === 'edit' ? 'Editar convenio' : 'Nuevo convenio'}
        footer={
          <>
            <button type="button" className="btn btn--secondary" onClick={closeForm} disabled={submitting || anexoBusy}>Cerrar</button>
            <button type="button" className="btn btn--primary" onClick={handleSubmit} disabled={submitting || anexoBusy}>
              {submitting ? 'Guardando...' : 'Guardar'}
            </button>
          </>
        }
      >
        <form onSubmit={handleSubmit} noValidate>
          {/* Colaborador */}
          <div className={styles.grid2}>
            <div style={{ display: 'flex', gap: 'var(--spacing-2)', alignItems: 'flex-end' }}>
              <div style={{ flex: 1 }}>
                <TextField label="Cédula del colaborador" name="cedula" value={form.cedula} required maxLength={20}
                  onChange={(v) => setField('cedula', v)} error={errors.cedula} />
              </div>
              <button type="button" className="btn btn--secondary" onClick={buscarColaborador} disabled={buscandoColab} style={{ marginBottom: 2 }}>
                <Search width={16} height={16} /><span>{buscandoColab ? '...' : 'Buscar'}</span>
              </button>
            </div>
            <TextField label="Colaborador" name="nombreColaborador" value={form.nombreColaborador} disabled
              helper={form.origenColaborador ? `Origen: ${form.origenColaborador}` : 'Busca por cédula para validar'} onChange={() => {}} />

            <TextField label="Título del convenio" name="titulo" value={form.titulo} required maxLength={250}
              onChange={(v) => setField('titulo', v)} error={errors.titulo} />
            <TextField label="Tipo de convenio" name="tipo" value={form.tipo} maxLength={150} onChange={(v) => setField('tipo', v)} />

            <TextField label="Tipo de curso" name="tipoCurso" value={form.tipoCurso} maxLength={150} onChange={(v) => setField('tipoCurso', v)} />
            <TextField label="Nombre del curso" name="nombreCurso" value={form.nombreCurso} maxLength={250} onChange={(v) => setField('nombreCurso', v)} />
            <TextField label="Marca" name="marca" value={form.marca} maxLength={150} onChange={(v) => setField('marca', v)} />

            <TextField label="Solicitado por" name="solicitadoPor" value={form.solicitadoPor} maxLength={200} onChange={(v) => setField('solicitadoPor', v)} />
            <TextField label="Autorizado por" name="autorizadoPor" value={form.autorizadoPor} maxLength={200} onChange={(v) => setField('autorizadoPor', v)} />

            <div className="form-group" style={{ position: 'static' }}>
              <label className="form-label" style={{ position: 'static' }} htmlFor="cv-fecha">Fecha *</label>
              <input id="cv-fecha" type="date" className="form-input" value={form.fecha} onChange={(e) => setField('fecha', e.target.value)} />
              {errors.fecha && <div className="form-helper form-helper--error">{errors.fecha}</div>}
            </div>

            <div className="form-group" style={{ position: 'static' }}>
              <label className="form-label" style={{ position: 'static' }} htmlFor="cv-meses">Meses a devengar</label>
              <select id="cv-meses" className="form-input" value={form.mesesADevengar} onChange={(e) => setField('mesesADevengar', Number(e.target.value))}>
                {MESES_OPCIONES.map((m) => <option key={m.value} value={m.value}>{m.label}</option>)}
              </select>
            </div>

            <div className="form-group" style={{ position: 'static' }}>
              <label className="form-label" style={{ position: 'static' }} htmlFor="cv-estado">Estado</label>
              <select id="cv-estado" className="form-input" value={form.estado} onChange={(e) => setField('estado', e.target.value)}>
                {ESTADOS.map((s) => <option key={s} value={s}>{s}</option>)}
              </select>
            </div>

            <div className={`form-group ${styles.fullSpan}`} style={{ position: 'static' }}>
              <label className="form-label" style={{ position: 'static' }} htmlFor="cv-desc">Descripción</label>
              <textarea id="cv-desc" className="form-input" rows={2} value={form.descripcion} onChange={(e) => setField('descripcion', e.target.value)} />
            </div>
          </div>

          {/* Ítems de costo */}
          <h4 style={{ marginTop: 'var(--spacing-4)', marginBottom: 0 }}>Detalle de costos</h4>
          {errors.items && <div className="form-helper form-helper--error">{errors.items}</div>}
          <table className={styles.itemsTable}>
            <thead>
              <tr>
                <th>Tipo</th><th style={{ width: 130 }}>Valor</th><th style={{ width: 90 }}>Devengable</th><th>Observación</th><th style={{ width: 40 }} />
              </tr>
            </thead>
            <tbody>
              {form.items.length === 0 && (<tr><td colSpan={5} className="text-secondary">Sin ítems. Agrega material, examen, etc.</td></tr>)}
              {form.items.map((it, idx) => (
                <tr key={idx}>
                  <td><input className="form-input" value={it.tipo} placeholder="Material, examen…" onChange={(e) => setItem(idx, 'tipo', e.target.value)} /></td>
                  <td><input className="form-input" type="number" step="0.01" min="0" value={it.valor} onChange={(e) => setItem(idx, 'valor', e.target.value)} /></td>
                  <td style={{ textAlign: 'center' }}><input type="checkbox" checked={it.devengable} onChange={(e) => setItem(idx, 'devengable', e.target.checked)} /></td>
                  <td><input className="form-input" value={it.observacion} onChange={(e) => setItem(idx, 'observacion', e.target.value)} /></td>
                  <td><button type="button" className="btn btn--ghost btn--sm btn--icon" onClick={() => removeItem(idx)} title="Quitar"><Trash width={16} height={16} /></button></td>
                </tr>
              ))}
            </tbody>
          </table>
          <button type="button" className="btn btn--secondary btn--sm" onClick={addItem} style={{ marginTop: 'var(--spacing-2)' }}>
            <Plus width={14} height={14} /><span>Agregar ítem</span>
          </button>
          <div className={styles.totalsBar}>
            <span className="badge">Total: {money(totalItems)}</span>
            <span className="badge badge--active">Devengable: {money(totalDevengable)}</span>
          </div>

          {/* Anexos (convenio firmado, formulario de cobro firmado, etc.) — múltiples */}
          <h4 style={{ marginTop: 'var(--spacing-4)', marginBottom: 'var(--spacing-2)' }}>Anexos (convenio firmado, formulario de cobro, etc.)</h4>
          {mode !== 'edit' ? (
            <p className="text-secondary" style={{ margin: 0 }}>Guarda el convenio para poder adjuntar documentos firmados.</p>
          ) : (
            <>
              {(editing?.anexos || []).length > 0 && (
                <ul style={{ listStyle: 'none', padding: 0, margin: '0 0 var(--spacing-2)' }}>
                  {editing.anexos.map((a) => (
                    <li key={a.id} style={{ display: 'flex', gap: 'var(--spacing-2)', alignItems: 'center', padding: '4px 0' }}>
                      <span style={{ flex: 1, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{a.nombreOriginal}</span>
                      <button type="button" className="btn btn--ghost btn--sm btn--icon" title="Descargar"
                        onClick={() => handleDescargarAnexo(editing.id, a)} disabled={anexoBusy}>
                        <Download width={16} height={16} />
                      </button>
                      <button type="button" className="btn btn--ghost btn--sm btn--icon" title="Eliminar"
                        onClick={() => eliminarAnexo(a.id)} disabled={anexoBusy}>
                        <Trash width={16} height={16} />
                      </button>
                    </li>
                  ))}
                </ul>
              )}

              {anexoBusy && uploadName ? (
                <div style={{ margin: 'var(--spacing-2) 0' }}>
                  <div style={{ fontSize: 13, marginBottom: 4 }}>Subiendo <strong>{uploadName}</strong> — {uploadPct}%</div>
                  <div style={{ height: 8, background: 'var(--color-border, #e5e7eb)', borderRadius: 4, overflow: 'hidden' }}>
                    <div style={{ width: `${uploadPct}%`, height: '100%', background: 'var(--color-primary, #2563eb)', transition: 'width .15s' }} />
                  </div>
                </div>
              ) : (
                <div
                  className={`dropzone${dragOver ? ' dropzone--active' : ''}`}
                  role="button" tabIndex={0}
                  onClick={() => { if (!anexoBusy) fileRef.current?.click(); }}
                  onKeyDown={(e) => { if ((e.key === 'Enter' || e.key === ' ') && !anexoBusy) { e.preventDefault(); fileRef.current?.click(); } }}
                  onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
                  onDragLeave={() => setDragOver(false)}
                  onDrop={(e) => { e.preventDefault(); setDragOver(false); if (!anexoBusy) { const f = e.dataTransfer?.files?.[0]; if (f) subirAnexo(f); } }}
                >
                  <div className="dropzone__icon"><UploadCloud width={28} height={28} /></div>
                  <div className="dropzone__title">Agregar anexo</div>
                  <div className="dropzone__text">Arrastra y suelta el archivo o haz clic para buscar</div>
                  <input ref={fileRef} type="file" style={{ display: 'none' }} onChange={(e) => { const f = e.target.files?.[0]; if (f) subirAnexo(f); }} disabled={anexoBusy} />
                </div>
              )}
            </>
          )}

          <button type="submit" style={{ display: 'none' }} aria-hidden="true" />
        </form>
      </Modal>
    </>
  );
}

/* ----------------------- Pestaña Historial por Colaborador ----------------------- */
function HistorialTab({ toast }) {
  const [cedula, setCedula] = useState('');
  const [rows, setRows] = useState(null); // null = aún no buscó
  const [loading, setLoading] = useState(false);
  const [colaborador, setColaborador] = useState(null);

  const buscar = async () => {
    const ced = cedula.trim();
    if (!ced) { toast.error('Ingresa la cédula del colaborador.'); return; }
    setLoading(true);
    try {
      const [data, colab] = await Promise.all([
        conveniosService.historial(ced, true),
        conveniosService.buscarColaborador(ced),
      ]);
      setRows(Array.isArray(data) ? data : []);
      setColaborador(colab);
    } catch (err) {
      setRows([]);
      toast.error(err?.message || 'No se pudo cargar el historial.');
    } finally {
      setLoading(false);
    }
  };

  const totalPendiente = useMemo(
    () => (rows || []).reduce((s, r) => s + (Number(r.montoPendiente) || 0), 0), [rows]);

  // Datos de colaborador para el encabezado: del lookup o del primer convenio (snapshot).
  const info = colaborador
    ? { name: colaborador.name, cargo: '', area: '' }
    : (rows && rows[0]
      ? { name: rows[0].nombreColaborador, cargo: rows[0].cargoColaborador, area: rows[0].areaColaborador }
      : null);

  const columns = useMemo(() => [
    { key: 'titulo', header: 'Convenio', accessor: (r) => r.titulo || '—' },
    { key: 'solicitadoPor', header: 'Solicitado por', accessor: (r) => r.solicitadoPor || '—' },
    { key: 'autorizadoPor', header: 'Aprobado por', accessor: (r) => r.autorizadoPor || '—' },
    { key: 'fecha', header: 'Fecha', accessor: (r) => fechaCorta(r.fecha) },
    { key: 'mesesPendientes', header: 'Meses pend.', align: 'center', accessor: (r) => (r.aplicaDevengo ? r.mesesPendientes : '—') },
    { key: 'porcentajePendiente', header: '% pend.', align: 'right', accessor: (r) => (r.aplicaDevengo ? `${r.porcentajePendiente}%` : '—') },
    { key: 'montoPendiente', header: 'Monto pendiente', align: 'right', accessor: (r) => money(r.montoPendiente) },
  ], []);

  return (
    <>
      <div className="toolbar">
        <div className="toolbar__filters" style={{ display: 'flex', gap: 'var(--spacing-2)', alignItems: 'flex-end' }}>
          <div style={{ minWidth: 240 }}>
            <TextField label="Cédula del colaborador" name="hist-cedula" value={cedula} onChange={setCedula} />
          </div>
          <button type="button" className="btn btn--primary" onClick={buscar} disabled={loading} style={{ marginBottom: 2 }}>
            <Search width={16} height={16} /><span>{loading ? 'Buscando…' : 'Buscar'}</span>
          </button>
        </div>
      </div>

      {rows !== null && (
        <>
          <div className="card">
            <div className="card__header">
              <div>
                <h2 className="card__title">
                  {colaborador ? `${colaborador.name} (${colaborador.cedula})` : info ? `${info.name} (${cedula})` : `Cédula ${cedula}`}
                </h2>
                {info && (info.cargo || info.area) && (
                  <p className="text-secondary" style={{ margin: 0, fontSize: 13 }}>
                    {[info.cargo, info.area].filter(Boolean).join(' · ')}
                  </p>
                )}
              </div>
              <span className="badge badge--inactive">Total por devengar: {money(totalPendiente)}</span>
            </div>
            <div className="card__body" style={{ padding: 0 }}>
              <DataTable columns={columns} rows={rows} loading={loading}
                emptyMessage="Este colaborador no tiene convenios con saldo por devengar." />
            </div>
          </div>

          {/* Detalle de costos: lo que el colaborador aún no devenga, por convenio */}
          {(rows || []).length > 0 && (
            <div className="card" style={{ marginTop: 'var(--spacing-4)' }}>
              <div className="card__header"><h2 className="card__title">Detalle de costos pendientes por devengar</h2></div>
              <div className="card__body">
                {rows.map((cv) => (
                  <div key={cv.id} style={{ marginBottom: 'var(--spacing-4)' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
                      <strong>{cv.titulo}</strong>
                      <span>
                        {cv.aplicaDevengo
                          ? `${cv.mesesPendientes} de ${cv.mesesADevengar} meses pendientes (${cv.porcentajePendiente}%)`
                          : 'No aplica devengo'}
                      </span>
                    </div>
                    <table className={styles.itemsTable}>
                      <thead>
                        <tr><th>Tipo de costo</th><th style={{ textAlign: 'right' }}>Valor</th><th style={{ textAlign: 'center' }}>Devengable</th><th>Observación</th></tr>
                      </thead>
                      <tbody>
                        {(cv.items || []).map((it) => (
                          <tr key={it.id}>
                            <td>{it.tipo}</td>
                            <td style={{ textAlign: 'right' }}>{money(it.valor)}</td>
                            <td style={{ textAlign: 'center' }}>{it.devengable ? 'Sí' : 'No'}</td>
                            <td>{it.observacion || '—'}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                    <div className={styles.totalsBar}>
                      <span className="badge">Base devengable: {money(cv.montoDevengable)}</span>
                      <span className="badge badge--inactive">Pendiente por devengar: {money(cv.montoPendiente)}</span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </>
  );
}
