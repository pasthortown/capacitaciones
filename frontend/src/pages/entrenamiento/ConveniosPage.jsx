import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Handshake, LayoutDashboard, History, Plus, Pencil, Trash2, RotateCcw,
  Download, Search, Trash, UploadCloud, Printer, FileDown, UserMinus,
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
import colaboradoresService from '../../services/colaboradores.js';
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

const TIPOS_EVENTO = [
  'Curso o capacitación', 'Certificación', 'Examen de certificación',
  'Diplomado', 'Programa especializado', 'Material de estudio',
];
const RESULTADOS = ['Aprobado', 'En curso', 'Pendiente', 'No aprobado'];

// --- Clasificación (espejo de ConvenioMapper en backend, solo para vista previa) ---
const norm = (s) => (s || '').trim().toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '');
function clasificar(tipo) {
  const t = norm(tipo);
  if (['examen de certificacion', 'certificacion', 'material de estudio'].includes(t)) return 'Certificaciones y exámenes';
  if (t === 'curso o capacitacion') return 'Cursos y capacitaciones';
  if (['diplomado', 'programa especializado'].includes(t)) return 'Diplomados o programas especializados';
  return 'Revisar';
}
const modalidadDe = (clasif) =>
  clasif === 'Certificaciones y exámenes' ? 'Reintegro escalonado especial' : 'Reintegro proporcional mensual';
function plazoSugerido(clasif, valor) {
  const v = Number(valor) || 0;
  if (v < 60) return { meses: 0, texto: 'N/A (valor < $60)' };
  if (clasif === 'Certificaciones y exámenes') return { meses: 36, texto: '36 meses' };
  if (clasif === 'Cursos y capacitaciones') {
    if (v <= 500) return { meses: 12, texto: '12 meses' };
    if (v <= 1500) return { meses: 24, texto: '24 meses' };
    if (v <= 4000) return { meses: 36, texto: '36 meses' };
    return { meses: -1, texto: 'Anexo especial' };
  }
  if (clasif === 'Diplomados o programas especializados') {
    if (v <= 1500) return { meses: 24, texto: '24 meses' };
    if (v <= 4000) return { meses: 36, texto: '36 meses' };
    return { meses: -1, texto: 'Anexo especial' };
  }
  return { meses: 0, texto: 'Revisar' };
}

// Agrupa convenios por cadena de referencia: cada convenio se asocia a la raíz de su cadena,
// siguiendo `convenioReferenciaId` mientras el padre esté presente en el conjunto. Devuelve
// grupos { rootId, root (titular), items[] }.
function agruparPorCadena(rows) {
  const list = Array.isArray(rows) ? rows : [];
  const byId = new Map(list.map((r) => [r.id, r]));
  const rootOf = (r) => {
    let cur = r;
    let guard = 0;
    while (cur.convenioReferenciaId && byId.has(cur.convenioReferenciaId) && guard < 50) {
      cur = byId.get(cur.convenioReferenciaId);
      guard += 1;
    }
    return cur.id;
  };
  const groups = new Map();
  for (const r of list) {
    const root = rootOf(r);
    if (!groups.has(root)) groups.set(root, []);
    groups.get(root).push(r);
  }
  return [...groups.entries()].map(([rootId, items]) => ({ rootId, root: byId.get(rootId) || items[0], items }));
}

/**
 * Renderiza convenios agrupados por cadena de referencia. Las cadenas (2+ convenios enlazados)
 * se muestran como bloques con encabezado (código/nombre del convenio raíz) y subtotal de costos;
 * los convenios sueltos van en una sola tabla. Si no hay cadenas, se comporta como una tabla simple.
 */
function GruposConvenios({ rows, columns, loading, montoKey, subtotalLabel, emptyMessage }) {
  const grupos = useMemo(() => agruparPorCadena(rows), [rows]);
  const cadenas = grupos.filter((g) => g.items.length > 1);
  const sueltos = grupos.filter((g) => g.items.length === 1).flatMap((g) => g.items);
  const sum = (arr) => arr.reduce((s, x) => s + (Number(x[montoKey]) || 0), 0);

  if (cadenas.length === 0) {
    return <DataTable columns={columns} rows={sueltos} loading={loading} emptyMessage={emptyMessage} />;
  }
  return (
    <div style={{ padding: 'var(--spacing-3)' }}>
      {cadenas.map((g) => (
        <div key={g.rootId} style={{ marginBottom: 'var(--spacing-4)', border: '1px solid var(--color-border, #e5e7eb)', borderRadius: 8, overflow: 'hidden' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, flexWrap: 'wrap', padding: '8px 12px', background: 'var(--color-surface-alt, #f8fafc)' }}>
            <strong>
              Cadena: {g.root.codigoRegistro || '—'} — {g.root.nombreCurso || 'Sin curso'}{' '}
              <span className="text-secondary" style={{ fontWeight: 400, fontSize: 12 }}>({g.items.length} convenios)</span>
            </strong>
            <span className="badge badge--inactive">{subtotalLabel}: {money(sum(g.items))}</span>
          </div>
          <DataTable columns={columns} rows={g.items} loading={loading} emptyMessage={emptyMessage} />
        </div>
      ))}
      {sueltos.length > 0 && (
        <div>
          <div className="text-secondary" style={{ padding: '4px 2px', fontSize: 13 }}>Convenios individuales</div>
          <DataTable columns={columns} rows={sueltos} loading={loading} emptyMessage={emptyMessage} />
        </div>
      )}
    </div>
  );
}

/**
 * Selector Sí/No como grupo de botones (radio), en lugar de un switch con checkbox oculto.
 * Evita el mecanismo del checkbox `position:absolute` que, al recibir foco dentro del modal con
 * scroll, disparaba un reajuste de layout que colapsaba el cuerpo del modal en Chromium.
 */
function SiNoGroup({ label, value, onChange, name }) {
  const base = { padding: '6px 18px', border: '1px solid var(--color-border, #d1d5db)', cursor: 'pointer', background: '#fff', fontWeight: 600 };
  const on = { background: 'var(--color-primary, #e4003a)', color: '#fff', borderColor: 'var(--color-primary, #e4003a)' };
  return (
    <div className="form-group" style={{ position: 'static' }}>
      <label className="form-label" style={{ position: 'static', display: 'block', marginBottom: 6 }}>{label}</label>
      <div role="radiogroup" aria-label={label} style={{ display: 'inline-flex' }}>
        <button type="button" role="radio" aria-checked={value === true} name={name}
          onClick={() => onChange(true)}
          style={{ ...base, ...(value === true ? on : {}), borderRadius: '8px 0 0 8px' }}>Sí</button>
        <button type="button" role="radio" aria-checked={value === false}
          onClick={() => onChange(false)}
          style={{ ...base, ...(value === false ? on : {}), borderLeft: 'none', borderRadius: '0 8px 8px 0' }}>No</button>
      </div>
    </div>
  );
}

// Carga (una vez) todos los colaboradores DOS + Externos para filtrar en el cliente. Reusable.
function useColaboradoresCache() {
  const [colaboradores, setColaboradores] = useState([]);
  const [loading, setLoading] = useState(false);
  const [dosDisponible, setDosDisponible] = useState(true);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    (async () => {
      setLoading(true);
      try {
        const [dosRes, extRes] = await Promise.all([
          colaboradoresService.listDos({}).catch(() => ({ integracionDisponible: false, items: [] })),
          colaboradoresService.listExternos({}).catch(() => []),
        ]);
        if (!mounted.current) return;
        const norm = (e, origen) => ({
          cedula: String(e.cedula || '').trim(), name: e.name || '',
          cargo: e.jobPosition || '', area: e.workArea || '', empresa: e.society || '', origen,
        });
        const dos = (dosRes?.items || []).map((e) => norm(e, 'DOS'));
        const ext = (Array.isArray(extRes) ? extRes : []).map((e) => norm(e, 'Externo'));
        setDosDisponible(dosRes?.integracionDisponible !== false);
        const map = new Map();
        [...dos, ...ext].forEach((c) => { if (c.cedula && !map.has(c.cedula)) map.set(c.cedula, c); });
        setColaboradores([...map.values()]);
      } finally {
        if (mounted.current) setLoading(false);
      }
    })();
    return () => { mounted.current = false; };
  }, []);
  return { colaboradores, loading, dosDisponible };
}

// Combobox de colaborador: filtra en memoria por nombre o cédula y sugiere coincidencias.
function ColaboradorCombo({ colaboradores, loading, onSelect, minWidth = 320 }) {
  const [q, setQ] = useState('');
  const matches = useMemo(() => {
    const s = q.trim().toLowerCase();
    if (s.length < 2) return [];
    return colaboradores
      .filter((c) => c.name.toLowerCase().includes(s) || c.cedula.toLowerCase().includes(s))
      .slice(0, 25);
  }, [colaboradores, q]);
  return (
    <div className={styles.combo} style={{ minWidth }}>
      <div className={styles.comboInputWrap}>
        <Search width={16} height={16} className={styles.comboIcon} />
        <input className="form-input" style={{ paddingLeft: 34 }} autoComplete="off"
          placeholder={loading ? 'Cargando colaboradores…' : 'Busca por nombre o cédula…'}
          value={q} onChange={(e) => setQ(e.target.value)} />
      </div>
      {q.trim().length >= 2 && (
        <div className={styles.comboMenu}>
          {matches.length === 0 ? (
            <div className={styles.comboEmpty}>Sin coincidencias.</div>
          ) : matches.map((c) => (
            <button type="button" key={`${c.origen}-${c.cedula}`} className={styles.comboItem}
              onClick={() => { onSelect(c); setQ(''); }}>
              <span>{c.name}</span>
              <span className="text-secondary" style={{ fontSize: 12 }}>
                {c.cedula} · {c.origen}{c.cargo ? ` · ${c.cargo}` : ''}
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

const emptyForm = {
  cedula: '', nombreColaborador: '', origenColaborador: '', cargoColaborador: '', areaColaborador: '',
  empresaColaborador: '', centroCostos: '', jefeInmediato: '', relacionLaboral: '',
  fechaFirma: '',
  descripcion: '', tipo: '', nombreCurso: '', marca: '',
  esContinuacion: false, convenioReferenciaId: null,
  fechaInicioCurso: '', fechaFinCurso: '', horas: '', resultado: '', convenioFirmado: false,
  solicitadoPor: '', autorizadoPor: '',
  fecha: '', valorAsumidoEmpresa: '', mesesADevengar: 0, estado: 'Vigente',
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
          { id: 'desvinculacion', label: 'Desvinculación', icon: UserMinus },
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
      {tab === 'dashboard' && <DashboardTab toast={toast} />}
      {tab === 'desvinculacion' && <DesvinculacionTab toast={toast} />}
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
  // Cache en memoria de colaboradores (DOS + Externos) para filtrar en el cliente sin ir al backend.
  const [allColaboradores, setAllColaboradores] = useState([]);
  const [colabDosDisponible, setColabDosDisponible] = useState(true);
  const [colabQuery, setColabQuery] = useState('');   // filtro del picker de colaborador
  const [refQuery, setRefQuery] = useState('');        // filtro del picker de convenio de referencia
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

  // Carga (una sola vez) todos los colaboradores DOS + Externos y los cachea en memoria para
  // filtrar el picker en el cliente, sin consultar el backend en cada búsqueda.
  const cargarColaboradores = useCallback(async () => {
    setBuscandoColab(true);
    try {
      const [dosRes, extRes] = await Promise.all([
        colaboradoresService.listDos({}).catch(() => ({ integracionDisponible: false, items: [] })),
        colaboradoresService.listExternos({}).catch(() => []),
      ]);
      if (!mountedRef.current) return;
      const norm = (e, origen) => ({
        cedula: String(e.cedula || '').trim(),
        name: e.name || '',
        cargo: e.jobPosition || '',
        area: e.workArea || '',
        empresa: e.society || '',
        origen,
      });
      const dos = (dosRes?.items || []).map((e) => norm(e, 'DOS'));
      const ext = (Array.isArray(extRes) ? extRes : []).map((e) => norm(e, 'Externo'));
      setColabDosDisponible(dosRes?.integracionDisponible !== false);
      // Dedup por cédula (un externo con misma cédula no debería existir, pero por si acaso).
      const map = new Map();
      [...dos, ...ext].forEach((c) => { if (c.cedula && !map.has(c.cedula)) map.set(c.cedula, c); });
      setAllColaboradores([...map.values()]);
    } finally {
      if (mountedRef.current) setBuscandoColab(false);
    }
  }, []);

  useEffect(() => { cargarColaboradores(); }, [cargarColaboradores]);

  const filtered = useMemo(() => {
    const q = buscar.trim().toLowerCase();
    if (q.length < 2) return items;
    return items.filter((r) =>
      [r.nombreColaborador, r.cedulaColaborador, r.tipo, r.nombreCurso, r.marca]
        .some((v) => String(v ?? '').toLowerCase().includes(q)));
  }, [items, buscar]);

  // Coincidencias del picker de colaborador (filtro en memoria por nombre o cédula).
  const colabMatches = useMemo(() => {
    const q = colabQuery.trim().toLowerCase();
    if (q.length < 2) return [];
    return allColaboradores
      .filter((c) => c.name.toLowerCase().includes(q) || c.cedula.toLowerCase().includes(q))
      .slice(0, 25);
  }, [allColaboradores, colabQuery]);

  // Coincidencias del picker de convenio de referencia (filtro por código o nombre del curso),
  // excluyendo el propio convenio en edición.
  const refMatches = useMemo(() => {
    const q = refQuery.trim().toLowerCase();
    return items
      .filter((r) => r.id !== editing?.id)
      .filter((r) => {
        if (q.length < 1) return false;
        return String(r.codigoRegistro || '').toLowerCase().includes(q)
          || String(r.nombreCurso || '').toLowerCase().includes(q);
      })
      .slice(0, 25);
  }, [items, refQuery, editing]);

  const refSeleccionado = useMemo(
    () => (form.convenioReferenciaId ? items.find((r) => r.id === form.convenioReferenciaId) : null),
    [items, form.convenioReferenciaId]);

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
    const d = (s) => (fechaCorta(s) === '—' ? '' : fechaCorta(s));
    setForm({
      cedula: row.cedulaColaborador || '',
      nombreColaborador: row.nombreColaborador || '',
      origenColaborador: row.origenColaborador || '',
      cargoColaborador: row.cargoColaborador || '',
      areaColaborador: row.areaColaborador || '',
      empresaColaborador: row.empresaColaborador || '',
      centroCostos: row.centroCostos || '',
      jefeInmediato: row.jefeInmediato || '',
      relacionLaboral: row.relacionLaboral || '',
      fechaFirma: d(row.fechaFirma),
      descripcion: row.descripcion || '',
      tipo: row.tipo || '',
      nombreCurso: row.nombreCurso || '',
      marca: row.marca || '',
      esContinuacion: !!row.convenioReferenciaId,
      convenioReferenciaId: row.convenioReferenciaId || null,
      fechaInicioCurso: d(row.fechaInicioCurso),
      fechaFinCurso: d(row.fechaFinCurso),
      horas: row.horas != null ? String(row.horas) : '',
      resultado: row.resultado || '',
      convenioFirmado: !!row.convenioFirmado,
      solicitadoPor: row.solicitadoPor || '',
      autorizadoPor: row.autorizadoPor || '',
      fecha: d(row.fecha),
      valorAsumidoEmpresa: row.valorAsumidoEmpresa != null ? String(row.valorAsumidoEmpresa) : '',
      mesesADevengar: row.mesesADevengar ?? 0,
      estado: row.estado || 'Vigente',
      items: (row.items || []).map((i) => ({
        tipo: i.tipo || '', valor: String(i.valor ?? ''), devengable: i.devengable !== false, observacion: i.observacion || '',
      })),
    });
    setFormOpen(true);
  };

  const closeForm = () => { if (!submitting && !anexoBusy) setFormOpen(false); };

  // Selecciona un colaborador del cache: fija cédula/nombre/origen y pre-llena cargo/área/empresa
  // (editables). El usuario puede complementar los que la fuente no tenga.
  const selectColaborador = (c) => {
    setForm((f) => ({
      ...f,
      cedula: c.cedula,
      nombreColaborador: c.name,
      origenColaborador: c.origen,
      cargoColaborador: c.cargo || '',
      areaColaborador: c.area || '',
      empresaColaborador: c.empresa || '',
    }));
    setColabQuery('');
    setErrors((e) => ({ ...e, cedula: undefined }));
  };

  const limpiarColaborador = () => {
    setForm((f) => ({
      ...f, cedula: '', nombreColaborador: '', origenColaborador: '',
      cargoColaborador: '', areaColaborador: '', empresaColaborador: '',
    }));
    setColabQuery('');
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

  // Vista previa de la clasificación automática (el backend recalcula al guardar).
  const calcPreview = useMemo(() => {
    const clasif = clasificar(form.tipo);
    return { clasif, modalidad: modalidadDe(clasif), plazo: plazoSugerido(clasif, form.valorAsumidoEmpresa) };
  }, [form.tipo, form.valorAsumidoEmpresa]);

  const validate = () => {
    const next = {};
    if (!form.cedula.trim() || !form.nombreColaborador) next.cedula = 'Busca y selecciona el colaborador (por nombre o cédula).';
    if (form.esContinuacion && !form.convenioReferenciaId) next.referencia = 'Selecciona el convenio previo del que este es parte/continuación.';
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
    cargoColaborador: form.cargoColaborador.trim() || null,
    areaColaborador: form.areaColaborador.trim() || null,
    empresaColaborador: form.empresaColaborador.trim() || null,
    centroCostos: form.centroCostos.trim() || null,
    jefeInmediato: form.jefeInmediato.trim() || null,
    relacionLaboral: form.relacionLaboral.trim() || null,
    fechaFirma: form.fechaFirma || null,
    descripcion: form.descripcion.trim() || null,
    tipo: form.tipo.trim() || null,
    nombreCurso: form.nombreCurso.trim() || null,
    marca: form.marca.trim() || null,
    convenioReferenciaId: form.esContinuacion ? (form.convenioReferenciaId || null) : null,
    fechaInicioCurso: form.fechaInicioCurso || null,
    fechaFinCurso: form.fechaFinCurso || null,
    horas: form.horas === '' ? null : Number(form.horas),
    resultado: form.resultado.trim() || null,
    convenioFirmado: !!form.convenioFirmado,
    solicitadoPor: form.solicitadoPor.trim() || null,
    autorizadoPor: form.autorizadoPor.trim() || null,
    fecha: form.fecha,
    valorAsumidoEmpresa: Number(form.valorAsumidoEmpresa) || 0,
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
      text: `El convenio ${row.codigoRegistro || ''} (${row.nombreCurso || 'sin curso'}) se marcará como inactivo.`,
      icon: 'warning', confirmText: 'Sí, eliminar', cancelText: 'Cancelar', danger: true,
    });
    if (!ok) return;
    try { await conveniosService.remove(row.id); toast.success('Convenio eliminado.'); await fetchList(); }
    catch (err) { toast.error(err?.message || 'No se pudo eliminar.'); }
  };

  const handleReactivate = async (row) => {
    try {
      const d = (s) => (fechaCorta(s) === '—' ? null : fechaCorta(s));
      await conveniosService.update(row.id, {
        cedula: row.cedulaColaborador, descripcion: row.descripcion,
        cargoColaborador: row.cargoColaborador, areaColaborador: row.areaColaborador, empresaColaborador: row.empresaColaborador,
        centroCostos: row.centroCostos, jefeInmediato: row.jefeInmediato, relacionLaboral: row.relacionLaboral,
        fechaFirma: d(row.fechaFirma),
        tipo: row.tipo, nombreCurso: row.nombreCurso, marca: row.marca,
        convenioReferenciaId: row.convenioReferenciaId || null,
        fechaInicioCurso: d(row.fechaInicioCurso), fechaFinCurso: d(row.fechaFinCurso),
        horas: row.horas, resultado: row.resultado, convenioFirmado: !!row.convenioFirmado,
        solicitadoPor: row.solicitadoPor, autorizadoPor: row.autorizadoPor,
        fecha: fechaCorta(row.fecha), valorAsumidoEmpresa: row.valorAsumidoEmpresa,
        mesesADevengar: row.mesesADevengar, estado: row.estado,
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

  const [imprimiendoId, setImprimiendoId] = useState(null);
  const handleImprimir = async (row) => {
    setImprimiendoId(row.id);
    try {
      const fallback = `${row.codigoRegistro || 'Convenio'}.pdf`;
      await conveniosService.imprimir(row.id, fallback);
    } catch (err) {
      if (err instanceof HttpError && err.status === 503) toast.error('El servicio de emisión no está disponible. Intenta en unos minutos.');
      else toast.error(err?.message || 'No se pudo generar el PDF del convenio.');
    } finally {
      if (mountedRef.current) setImprimiendoId(null);
    }
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
    { key: 'codigoRegistro', header: 'Código', width: '140px', accessor: (r) => r.codigoRegistro || '—' },
    {
      key: 'colaborador', header: 'Colaborador',
      accessor: (r) => (
        <div>
          <div>{r.nombreColaborador}</div>
          <div className="text-secondary" style={{ fontSize: 12 }}>{r.cedulaColaborador} · {r.origenColaborador}</div>
        </div>
      ),
    },
    { key: 'nombreCurso', header: 'Curso / Certificación / Exámen', accessor: (r) => r.nombreCurso || '—' },
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
                  onClick={() => handleImprimir(row)} title="Imprimir convenio (PDF)" aria-label="Imprimir"
                  disabled={imprimiendoId === row.id}>
                  <Printer width={16} height={16} />
                </button>
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
            <div className={styles.fullSpan}><h4 style={{ margin: 0 }}>Colaborador</h4></div>

            {/* Buscador con filtrado en memoria (nombre o cédula) — sin ir al backend por búsqueda. */}
            <div className={`${styles.fullSpan} ${styles.combo}`}>
              <label className="form-label form-label--required" style={{ position: 'static' }} htmlFor="cv-colab-q">
                Colaborador (busca por nombre o cédula)
              </label>
              {form.nombreColaborador ? (
                <div className={styles.comboSelected}>
                  <div>
                    <strong>{form.nombreColaborador}</strong>
                    <span className="text-secondary" style={{ marginLeft: 8, fontSize: 12 }}>
                      {form.cedula} · {form.origenColaborador || '—'}
                    </span>
                  </div>
                  <button type="button" className="btn btn--ghost btn--sm" onClick={limpiarColaborador}>Cambiar</button>
                </div>
              ) : (
                <>
                  <div className={styles.comboInputWrap}>
                    <Search width={16} height={16} className={styles.comboIcon} />
                    <input id="cv-colab-q" className={`form-input${errors.cedula ? ' form-input--error' : ''}`}
                      style={{ paddingLeft: 34 }} autoComplete="off"
                      placeholder={buscandoColab ? 'Cargando colaboradores…' : 'Escribe al menos 2 caracteres…'}
                      value={colabQuery} onChange={(e) => setColabQuery(e.target.value)} />
                  </div>
                  {colabQuery.trim().length >= 2 && (
                    <div className={styles.comboMenu}>
                      {colabMatches.length === 0 ? (
                        <div className={styles.comboEmpty}>
                          Sin coincidencias{colabDosDisponible ? '' : ' (integración DOS no disponible)'}.
                        </div>
                      ) : colabMatches.map((c) => (
                        <button type="button" key={`${c.origen}-${c.cedula}`} className={styles.comboItem}
                          onClick={() => selectColaborador(c)}>
                          <span>{c.name}</span>
                          <span className="text-secondary" style={{ fontSize: 12 }}>
                            {c.cedula} · {c.origen}{c.cargo ? ` · ${c.cargo}` : ''}
                          </span>
                        </button>
                      ))}
                    </div>
                  )}
                </>
              )}
              {errors.cedula && <div className="form-helper form-helper--error">{errors.cedula}</div>}
            </div>

            {/* Cargo/Área/Empresa: editables (pre-llenados desde la fuente, complementables). */}
            <TextField label="Cargo" name="cargoColaborador" value={form.cargoColaborador} maxLength={150}
              onChange={(v) => setField('cargoColaborador', v)} helper="Editable: pre-llenado desde la fuente." />
            <TextField label="Área / Departamento" name="areaColaborador" value={form.areaColaborador} maxLength={150}
              onChange={(v) => setField('areaColaborador', v)} helper="Editable." />
            <TextField label="Empresa" name="empresaColaborador" value={form.empresaColaborador} maxLength={200}
              onChange={(v) => setField('empresaColaborador', v)} helper="Editable." />
            <TextField label="Centro de costos" name="centroCostos" value={form.centroCostos} maxLength={150}
              onChange={(v) => setField('centroCostos', v)} />
            <TextField label="Jefe inmediato" name="jefeInmediato" value={form.jefeInmediato} maxLength={200}
              onChange={(v) => setField('jefeInmediato', v)} />
            <TextField label="Relación laboral" name="relacionLaboral" value={form.relacionLaboral} maxLength={100}
              onChange={(v) => setField('relacionLaboral', v)} />

            <div className="form-group" style={{ position: 'static' }}>
              <label className="form-label" style={{ position: 'static' }} htmlFor="cv-firma">Fecha de firma</label>
              <input id="cv-firma" type="date" className="form-input" value={form.fechaFirma} onChange={(e) => setField('fechaFirma', e.target.value)} />
            </div>

            {/* Evento formativo */}
            <div className={styles.fullSpan} style={{ marginTop: 'var(--spacing-2)' }}><h4 style={{ margin: 0 }}>Evento formativo</h4></div>
            <div className="form-group" style={{ position: 'static' }}>
              <label className="form-label" style={{ position: 'static' }} htmlFor="cv-tipo">Tipo de evento</label>
              <select id="cv-tipo" className="form-input" value={form.tipo} onChange={(e) => setField('tipo', e.target.value)}>
                <option value="">— Selecciona —</option>
                {TIPOS_EVENTO.map((t) => <option key={t} value={t}>{t}</option>)}
                {form.tipo && !TIPOS_EVENTO.includes(form.tipo) && <option value={form.tipo}>{form.tipo}</option>}
              </select>
            </div>

            <TextField label="Nombre de Curso / Certificación / Exámen" name="nombreCurso" value={form.nombreCurso} maxLength={250} onChange={(v) => setField('nombreCurso', v)} />
            <TextField label="Marca" name="marca" value={form.marca} maxLength={150} onChange={(v) => setField('marca', v)} />

            <div className="form-group" style={{ position: 'static' }}>
              <label className="form-label" style={{ position: 'static' }} htmlFor="cv-ini">Fecha inicio del curso</label>
              <input id="cv-ini" type="date" className="form-input" value={form.fechaInicioCurso} onChange={(e) => setField('fechaInicioCurso', e.target.value)} />
            </div>
            <div className="form-group" style={{ position: 'static' }}>
              <label className="form-label" style={{ position: 'static' }} htmlFor="cv-fin">Fecha fin / aprobación</label>
              <input id="cv-fin" type="date" className="form-input" value={form.fechaFinCurso} onChange={(e) => setField('fechaFinCurso', e.target.value)} />
            </div>
            <TextField label="Horas" name="horas" type="number" value={form.horas} onChange={(v) => setField('horas', v)} />
            <div className="form-group" style={{ position: 'static' }}>
              <label className="form-label" style={{ position: 'static' }} htmlFor="cv-resultado">Resultado</label>
              <select id="cv-resultado" className="form-input" value={form.resultado} onChange={(e) => setField('resultado', e.target.value)}>
                <option value="">— Selecciona —</option>
                {RESULTADOS.map((r) => <option key={r} value={r}>{r}</option>)}
              </select>
            </div>

            <TextField label="Solicitado por" name="solicitadoPor" value={form.solicitadoPor} maxLength={200} onChange={(v) => setField('solicitadoPor', v)} />
            <TextField label="Autorizado por" name="autorizadoPor" value={form.autorizadoPor} maxLength={200} onChange={(v) => setField('autorizadoPor', v)} />

            <div className={`form-group ${styles.fullSpan}`} style={{ position: 'static' }}>
              <SiNoGroup label="Convenio firmado (marcar al cargar el documento firmado)" name="convenioFirmado"
                value={!!form.convenioFirmado} onChange={(v) => setField('convenioFirmado', v)} />
            </div>

            <div className={`form-group ${styles.fullSpan}`} style={{ position: 'static' }}>
              <label className="form-label" style={{ position: 'static' }} htmlFor="cv-desc">Descripción</label>
              <textarea id="cv-desc" className="form-input" rows={2} value={form.descripcion} onChange={(e) => setField('descripcion', e.target.value)} />
            </div>

            {/* Referencia a un convenio previo (parte / continuación). Full-span al final para no
                partir la grilla de 2 columnas. */}
            <div className={`form-group ${styles.fullSpan}`} style={{ position: 'static', marginBottom: 0 }}>
              <SiNoGroup label="¿Es parte o continuación de un convenio previo?" name="esContinuacion"
                value={!!form.esContinuacion}
                onChange={(v) => setForm((f) => ({ ...f, esContinuacion: v, convenioReferenciaId: v ? f.convenioReferenciaId : null }))} />
            </div>
            {form.esContinuacion && (
              <div className={`${styles.fullSpan} ${styles.combo}`}>
                <label className="form-label" style={{ position: 'static' }} htmlFor="cv-ref-q">
                  Convenio previo (busca por código o nombre)
                </label>
                {refSeleccionado ? (
                  <div className={styles.comboSelected}>
                    <div>
                      <strong>{refSeleccionado.codigoRegistro || '—'}</strong>
                      <span className="text-secondary" style={{ marginLeft: 8, fontSize: 12 }}>
                        {refSeleccionado.nombreCurso || 'Sin curso'}
                      </span>
                    </div>
                    <button type="button" className="btn btn--ghost btn--sm"
                      onClick={() => { setForm((f) => ({ ...f, convenioReferenciaId: null })); setRefQuery(''); }}>Cambiar</button>
                  </div>
                ) : (
                  <>
                    <div className={styles.comboInputWrap}>
                      <Search width={16} height={16} className={styles.comboIcon} />
                      <input id="cv-ref-q" className={`form-input${errors.referencia ? ' form-input--error' : ''}`}
                        style={{ paddingLeft: 34 }} autoComplete="off"
                        placeholder="Escribe código o nombre del curso…"
                        value={refQuery} onChange={(e) => setRefQuery(e.target.value)} />
                    </div>
                    {refQuery.trim().length >= 1 && (
                      <div className={styles.comboMenu}>
                        {refMatches.length === 0 ? (
                          <div className={styles.comboEmpty}>Sin coincidencias.</div>
                        ) : refMatches.map((r) => (
                          <button type="button" key={r.id} className={styles.comboItem}
                            onClick={() => { setForm((f) => ({ ...f, convenioReferenciaId: r.id })); setRefQuery(''); setErrors((e) => ({ ...e, referencia: undefined })); }}>
                            <span>{(r.codigoRegistro || '—')} - {r.nombreCurso || 'Sin curso'}</span>
                            <span className="text-secondary" style={{ fontSize: 12 }}>{r.nombreColaborador} · {r.cedulaColaborador}</span>
                          </button>
                        ))}
                      </div>
                    )}
                  </>
                )}
                {errors.referencia && <div className="form-helper form-helper--error">{errors.referencia}</div>}
              </div>
            )}
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
            <span className="badge badge--active">Devengable (ítems): {money(totalDevengable)}</span>
          </div>

          {/* Devengación */}
          <h4 style={{ marginTop: 'var(--spacing-4)', marginBottom: 'var(--spacing-2)' }}>Devengación</h4>
          <div className={styles.grid2}>
            <div className="form-group" style={{ position: 'static' }}>
              <label className="form-label" style={{ position: 'static' }} htmlFor="cv-valor">Valor asumido por la empresa (USD)</label>
              <input id="cv-valor" type="number" step="0.01" min="0" className="form-input" value={form.valorAsumidoEmpresa}
                onChange={(e) => setField('valorAsumidoEmpresa', e.target.value)} />
              <div className="form-helper">Base del cálculo de devengación.</div>
            </div>

            <div className="form-group" style={{ position: 'static' }}>
              <label className="form-label" style={{ position: 'static' }} htmlFor="cv-fecha">Fecha del convenio *</label>
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
          </div>

          {/* Cálculo automático (vista previa; el backend recalcula al guardar) */}
          <div className="alert alert--info" style={{ marginTop: 'var(--spacing-3)' }}>
            <div className="alert__content">
              <div className="alert__title">Cálculo automático</div>
              <div className="alert__message" style={{ display: 'flex', gap: 'var(--spacing-4)', flexWrap: 'wrap' }}>
                <span><strong>Clasificación:</strong> {calcPreview.clasif}</span>
                <span><strong>Modalidad:</strong> {calcPreview.modalidad}</span>
                <span><strong>Plazo sugerido:</strong> {calcPreview.plazo.texto}
                  {calcPreview.plazo.meses > 0 && Number(form.mesesADevengar) !== calcPreview.plazo.meses && (
                    <button type="button" className="btn btn--ghost btn--sm" style={{ marginLeft: 8 }}
                      onClick={() => setField('mesesADevengar', calcPreview.plazo.meses)}>Aplicar</button>
                  )}
                </span>
              </div>
            </div>
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
  const { colaboradores, loading: colabLoading } = useColaboradoresCache();

  const buscar = async (cedForce) => {
    const ced = String(cedForce ?? cedula).trim();
    if (!ced) { toast.error('Busca y selecciona un colaborador (nombre o cédula).'); return; }
    setLoading(true);
    try {
      const [data, colab] = await Promise.all([
        conveniosService.historial(ced, false), // false = todos los convenios (no solo con saldo)
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

  const [descargando, setDescargando] = useState(false);
  const descargarReporte = async () => {
    const ced = cedula.trim();
    if (!ced) { toast.error('Busca primero un colaborador.'); return; }
    setDescargando(true);
    try {
      await conveniosService.descargarReporteColaborador(ced);
    } catch (err) {
      if (err instanceof HttpError && err.status === 503) toast.error('El servicio de emisión no está disponible. Intenta en unos minutos.');
      else toast.error(err?.message || 'No se pudo descargar el reporte.');
    } finally {
      setDescargando(false);
    }
  };

  // Datos de colaborador para el encabezado: del lookup o del primer convenio (snapshot).
  const info = colaborador
    ? { name: colaborador.name, cargo: '', area: '' }
    : (rows && rows[0]
      ? { name: rows[0].nombreColaborador, cargo: rows[0].cargoColaborador, area: rows[0].areaColaborador }
      : null);

  const columns = useMemo(() => [
    { key: 'nombreCurso', header: 'Curso / Certificación / Exámen', accessor: (r) => r.nombreCurso || '—' },
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
        <div className={`toolbar__filters ${styles.filtersRow}`} style={{ alignItems: 'flex-start' }}>
          <ColaboradorCombo colaboradores={colaboradores} loading={colabLoading}
            onSelect={(c) => { setCedula(c.cedula); buscar(c.cedula); }} />
          {rows !== null && (
            <button type="button" className="btn btn--secondary" onClick={descargarReporte} disabled={descargando}>
              <FileDown width={16} height={16} /><span>{descargando ? 'Generando…' : 'Descargar Reporte'}</span>
            </button>
          )}
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
              <GruposConvenios rows={rows} columns={columns} loading={loading}
                montoKey="montoPendiente" subtotalLabel="Subtotal por devengar"
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
                      <strong>{cv.nombreCurso || cv.codigoRegistro || '—'}</strong>
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

/* ------------------------------- Pestaña Dashboard ------------------------------- */
function SectionTitle({ children }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10, margin: 'var(--spacing-5) 0 var(--spacing-3)', fontSize: 11, fontWeight: 700, letterSpacing: 2, textTransform: 'uppercase', color: 'var(--color-text-secondary, #6b7280)' }}>
      <span>{children}</span>
      <span style={{ flex: 1, height: 1, background: 'var(--color-border, #e5e7eb)' }} />
    </div>
  );
}

function DimBars({ titulo, grupos, top = 8 }) {
  const visibles = grupos.slice(0, top);
  const resto = grupos.length - visibles.length;
  const max = Math.max(1, ...visibles.map((g) => Number(g.inversion) || 0));
  return (
    <div className="card">
      <div className="card__header"><h3 className="card__title">{titulo}</h3></div>
      <div className="card__body" style={{ display: 'grid', gap: 'var(--spacing-4)' }}>
        {visibles.length === 0 && <div className="text-secondary">Sin datos.</div>}
        {visibles.map((g) => (
          <div key={g.etiqueta}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 5, gap: 12 }}>
              <span style={{ fontWeight: 600, fontSize: 13 }}>{g.etiqueta}</span>
              <span className="text-secondary" style={{ whiteSpace: 'nowrap', fontSize: 12 }}>{money(g.inversion)}</span>
            </div>
            <div style={{ height: 12, background: 'var(--color-surface-2, #eef0f3)', borderRadius: 6, overflow: 'hidden' }}>
              <div style={{ width: `${Math.round((Number(g.inversion) || 0) / max * 100)}%`, height: '100%', background: 'var(--color-primary, #2563eb)', borderRadius: 6 }} />
            </div>
            <div className="text-secondary" style={{ fontSize: 11, marginTop: 3 }}>{g.cantidad} convenios · {g.personas} colaboradores</div>
          </div>
        ))}
        {resto > 0 && <div className="text-secondary" style={{ fontSize: 12 }}>+{resto} más…</div>}
      </div>
    </div>
  );
}

function MesesBars({ porMes }) {
  const max = Math.max(1, ...porMes.map((m) => Number(m.inversion) || 0));
  return (
    <div className="card">
      <div className="card__body" style={{ display: 'flex', alignItems: 'flex-end', gap: 10, height: 200, overflowX: 'auto', paddingTop: 'var(--spacing-4)' }}>
        {porMes.length === 0 && <div className="text-secondary">Sin datos.</div>}
        {porMes.map((m) => (
          <div key={m.mes} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', minWidth: 52 }} title={`${m.mes}: ${money(m.inversion)} (${m.convenios} conv)`}>
            <div className="text-secondary" style={{ fontSize: 10, marginBottom: 4 }}>{money(m.inversion)}</div>
            <div style={{ flex: 1, display: 'flex', alignItems: 'flex-end', width: 28 }}>
              <div style={{ width: '100%', height: `${Math.round((Number(m.inversion) || 0) / max * 100)}%`, background: 'var(--color-primary, #2563eb)', borderRadius: '4px 4px 0 0', minHeight: 2 }} />
            </div>
            <div style={{ fontSize: 10, marginTop: 6, color: 'var(--color-text-secondary, #666)' }}>{m.mes}</div>
          </div>
        ))}
      </div>
    </div>
  );
}

function DashboardTab({ toast }) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [descargando, setDescargando] = useState(false);

  useEffect(() => {
    let active = true;
    (async () => {
      setLoading(true);
      try {
        const d = await conveniosService.dashboard();
        if (active) setData(d);
      } catch (err) {
        if (active) toast.error(err?.message || 'No se pudo cargar el dashboard.');
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => { active = false; };
  }, [toast]);

  const descargarPdf = async () => {
    setDescargando(true);
    try {
      await conveniosService.descargarDashboardPdf();
    } catch (err) {
      if (err instanceof HttpError && err.status === 503) toast.error('El servicio de emisión no está disponible. Intenta en unos minutos.');
      else toast.error(err?.message || 'No se pudo descargar el PDF.');
    } finally {
      setDescargando(false);
    }
  };

  if (loading) {
    return <div style={{ padding: 'var(--spacing-6)', display: 'flex', justifyContent: 'center' }}><Spinner size={32} label="Cargando…" /></div>;
  }
  if (!data) return <div className="card"><div className="card__body">Sin datos.</div></div>;

  const kpis = [
    { label: 'Convenios', value: data.totalConvenios },
    { label: 'Colaboradores', value: data.totalPersonas },
    { label: 'Valor asumido', value: money(data.totalAsumido) },
    { label: 'Devengado', value: money(data.totalDevengado) },
    { label: 'Por devengar', value: money(data.totalPorDevengar) },
    { label: 'Horas', value: data.totalHoras ?? 0 },
    { label: 'Costo prom./persona', value: money(data.costoPromedioPersona) },
    { label: 'Convenios firmados', value: `${data.conveniosFirmados}/${data.totalConvenios}` },
  ];

  return (
    <>
      <div className="toolbar">
        <div className="toolbar__filters">
          <span className="text-secondary" style={{ fontSize: 13 }}>Fecha de corte: {fechaCorta(data.fechaCorte)}</span>
        </div>
        <div className="toolbar__actions">
          <button type="button" className="btn btn--primary" onClick={descargarPdf} disabled={descargando}>
            <FileDown width={16} height={16} /><span>{descargando ? 'Generando…' : 'Descargar PDF'}</span>
          </button>
        </div>
      </div>

      <SectionTitle>Indicadores</SectionTitle>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 'var(--spacing-4)' }}>
        {kpis.map((k) => (
          <div key={k.label} className="card"><div className="card__body" style={{ padding: 'var(--spacing-4)' }}>
            <div className="text-xs text-secondary" style={{ textTransform: 'uppercase', letterSpacing: 0.5 }}>{k.label}</div>
            <div style={{ fontFamily: 'var(--font-family-display)', fontSize: 28, fontWeight: 800, lineHeight: 1.1, marginTop: 6, color: 'var(--color-primary)' }}>{k.value}</div>
          </div></div>
        ))}
      </div>

      <SectionTitle>Inversión en el tiempo</SectionTitle>
      <MesesBars porMes={data.porMes || []} />

      <SectionTitle>Distribución</SectionTitle>
      <div className={styles.dimGrid}>
        {(data.dimensiones || []).map((dim) => (
          <DimBars key={dim.clave} titulo={dim.titulo} grupos={dim.grupos} />
        ))}
      </div>
    </>
  );
}

/* ----------------------------- Pestaña Desvinculación ----------------------------- */
function DesvinculacionTab({ toast }) {
  const [cedula, setCedula] = useState('');
  const [nombreSel, setNombreSel] = useState('');
  const [fechaSalida, setFechaSalida] = useState('');
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(false);
  const { colaboradores, loading: colabLoading } = useColaboradoresCache();

  const calcular = async (cedForce) => {
    const ced = String(cedForce ?? cedula).trim();
    if (!ced) { toast.error('Busca y selecciona un colaborador (nombre o cédula).'); return; }
    setLoading(true);
    try {
      const d = await conveniosService.liquidacion(ced, fechaSalida || undefined);
      setData(d);
    } catch (err) {
      setData(null);
      toast.error(err?.message || 'No se pudo calcular la liquidación.');
    } finally {
      setLoading(false);
    }
  };

  const [descargando, setDescargando] = useState(false);
  // Descarga TODO en un ZIP: reporte de liquidación + PDF de cada convenio + todos sus anexos.
  const descargarTodo = async () => {
    if (!data) return;
    setDescargando(true);
    try {
      await conveniosService.descargarPaqueteDesvinculacion(data.cedula, fechaSalida || undefined);
    } catch (err) {
      if (err instanceof HttpError && err.status === 503) toast.error('El servicio de emisión no está disponible. Intenta en unos minutos.');
      else toast.error(err?.message || 'No se pudo descargar el paquete.');
    } finally {
      setDescargando(false);
    }
  };

  const [rowBusy, setRowBusy] = useState(null); // id de la fila con descarga en curso
  const imprimirConvenio = async (row) => {
    setRowBusy(`pdf-${row.id}`);
    try { await conveniosService.imprimir(row.id, `Convenio_${row.codigoRegistro || row.id}.pdf`); }
    catch (err) { toast.error(err?.message || 'No se pudo generar el PDF.'); }
    finally { setRowBusy(null); }
  };
  const descargarAnexosConvenio = async (row) => {
    setRowBusy(`anx-${row.id}`);
    try { await conveniosService.descargarAnexosZip(row.id, `Anexos_${row.codigoRegistro || row.id}.zip`); }
    catch (err) { toast.error(err?.message || 'No se pudieron descargar los anexos.'); }
    finally { setRowBusy(null); }
  };

  const columns = useMemo(() => [
    { key: 'codigoRegistro', header: 'Código', accessor: (r) => r.codigoRegistro || '—' },
    { key: 'nombreCurso', header: 'Curso / Certificación / Exámen', accessor: (r) => r.nombreCurso || '—' },
    { key: 'clasificacion', header: 'Clasificación', accessor: (r) => r.clasificacion },
    { key: 'modalidad', header: 'Modalidad', accessor: (r) => r.modalidadReintegro },
    { key: 'meses', header: 'Meses a la salida', align: 'center', accessor: (r) => r.mesesTranscurridosASalida },
    { key: 'valorAsumido', header: 'Valor asumido', align: 'right', accessor: (r) => money(r.valorAsumidoEmpresa) },
    { key: 'reintegro', header: 'Reintegro', align: 'right', accessor: (r) => money(r.montoReintegro) },
    {
      key: 'acciones', header: 'Acciones', align: 'center',
      accessor: (r) => (
        <div style={{ display: 'flex', gap: 4, justifyContent: 'center' }}>
          <button type="button" className="btn btn--ghost btn--sm btn--icon" title="Imprimir convenio (PDF)"
            onClick={() => imprimirConvenio(r)} disabled={rowBusy === `pdf-${r.id}`}>
            <Printer width={16} height={16} />
          </button>
          <button type="button" className="btn btn--ghost btn--sm btn--icon" title="Descargar anexos (ZIP)"
            onClick={() => descargarAnexosConvenio(r)} disabled={rowBusy === `anx-${r.id}`}>
            <Download width={16} height={16} />
          </button>
        </div>
      ),
    },
  ], [rowBusy]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <>
      <div className="toolbar">
        <div className={`toolbar__filters ${styles.filtersRow}`} style={{ alignItems: 'flex-start' }}>
          <div>
            <ColaboradorCombo colaboradores={colaboradores} loading={colabLoading}
              onSelect={(c) => { setCedula(c.cedula); setNombreSel(c.name); }} minWidth={280} />
            {cedula && <div className="text-secondary" style={{ fontSize: 12, marginTop: 4 }}>Seleccionado: {nombreSel || cedula} ({cedula})</div>}
          </div>
          <div className="form-group" style={{ position: 'static' }}>
            <label className="form-label" style={{ position: 'static' }} htmlFor="desv-fecha">Fecha de salida</label>
            <input id="desv-fecha" type="date" className="form-input" value={fechaSalida} onChange={(e) => setFechaSalida(e.target.value)} />
          </div>
          <button type="button" className="btn btn--primary" onClick={() => calcular()} disabled={loading}>
            <Search width={16} height={16} /><span>{loading ? 'Calculando…' : 'Calcular reintegro'}</span>
          </button>
        </div>
      </div>

      {data && (
        <div className="card">
          <div className="card__header">
            <div>
              <h2 className="card__title">{data.nombre} ({data.cedula})</h2>
              {(data.cargo || data.area) && (
                <p className="text-secondary" style={{ margin: 0, fontSize: 13 }}>{[data.cargo, data.area].filter(Boolean).join(' · ')}</p>
              )}
              <p className="text-secondary" style={{ margin: 0, fontSize: 13 }}>Fecha de desvinculación: {fechaCorta(data.fechaSalida)}</p>
            </div>
            <div style={{ display: 'flex', gap: 'var(--spacing-2)', alignItems: 'center' }}>
              <span className="badge badge--inactive">Total a reintegrar: {money(data.totalReintegro)}</span>
              <button type="button" className="btn btn--primary btn--sm" onClick={descargarTodo} disabled={descargando}
                title="Descarga el reporte + PDF de cada convenio + todos los anexos">
                <Download width={16} height={16} /><span>{descargando ? 'Generando ZIP…' : 'Descargar todo (ZIP)'}</span>
              </button>
            </div>
          </div>
          <div className="card__body" style={{ padding: 0 }}>
            <GruposConvenios rows={data.convenios} columns={columns} loading={loading}
              montoKey="montoReintegro" subtotalLabel="Subtotal a reintegrar"
              emptyMessage="Sin convenios con reintegro a la fecha indicada." />
          </div>
        </div>
      )}
    </>
  );
}
