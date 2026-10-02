import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Eye, Pencil, Search } from 'lucide-react';
import DataTable from '../../components/Table/DataTable.jsx';
import Modal from '../../components/Modal/Modal.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import SignaturePad from '../../components/SignaturePad/SignaturePad.jsx';
import EmailConSufijo from '../../components/EmailConSufijo/EmailConSufijo.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { HttpError } from '../../services/http.js';
import { listCapacitaciones } from '../../services/capacitaciones.js';
import { list as listCatalogo, CATALOGO_SLUGS } from '../../services/catalogos.js';
import { listInscritos, getInscrito, updateInscrito } from '../../services/inscritos.js';
import { formatFechaHora } from '../../utils/formatters.js';
import styles from './InscritosPage.module.css';

/**
 * Pantalla admin "Inscritos": personas inscritas a las capacitaciones, con filtro por
 * capacitación y búsqueda (cédula, nombre o correo). Por fila:
 *  - Ver firma: la imagen sobre fondo blanco con sus dimensiones y peso, para detectar firmas
 *    cortadas, en blanco o equivocadas.
 *  - Editar: nombres, apellidos, cédula, área, correo y firma ("Reemplazar firma" muestra el
 *    SignaturePad). Al guardar el backend también actualiza el registro de personas.
 *
 * La lista no trae las firmas (pueden pesar cientos de KB); se piden por id al abrir un modal.
 */

const EMPTY_FORM = {
  nombres: '',
  apellidos: '',
  identificacion: '',
  areaId: '',
  emailUsuario: '',
};

/** Peso aproximado en KB de una data URL base64. */
function pesoKb(dataUrl) {
  const base64 = (dataUrl || '').split(',')[1] || '';
  return Math.round((base64.length * 3) / 4 / 1024);
}

function mensajeError(err, fallback) {
  if (err instanceof HttpError) {
    const codigo = err.body && typeof err.body === 'object' ? err.body.error : null;
    if (codigo === 'INSCRIPCION_DUPLICADA') {
      return 'Esa cédula ya está inscrita en esta capacitación.';
    }
    if (err.body && typeof err.body === 'object' && err.body.message) return err.body.message;
  }
  return err?.message || fallback;
}

export default function InscritosPage() {
  const toast = useToast();

  const [capacitaciones, setCapacitaciones] = useState([]);
  const [areas, setAreas] = useState([]);
  const [capacitacionId, setCapacitacionId] = useState('');
  const [buscar, setBuscar] = useState('');
  const [buscarAplicado, setBuscarAplicado] = useState('');
  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');

  // Modal "Ver firma"
  const [verFirma, setVerFirma] = useState(null); // { inscrito, loading, error, dims }

  // Modal "Editar"
  const [editando, setEditando] = useState(null); // detalle del inscrito
  const [editLoading, setEditLoading] = useState(false);
  const [editLoadError, setEditLoadError] = useState('');
  const [form, setForm] = useState(EMPTY_FORM);
  const [reemplazarFirma, setReemplazarFirma] = useState(false);
  const [firmaNueva, setFirmaNueva] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');

  // Identificadores de la última solicitud de cada modal: una respuesta que llega después de
  // cerrar el modal o de abrir otra fila se descarta (si no, se reabriría el modal o el
  // formulario quedaría con los datos de otra persona y el PUT iría al inscrito equivocado).
  const firmaReq = useRef(0);
  const editReq = useRef(0);
  const listReq = useRef(0);

  // Catálogos para filtros y edición.
  useEffect(() => {
    listCapacitaciones()
      .then((items) => {
        const lista = Array.isArray(items) ? items : [];
        lista.sort((a, b) => String(b.fechaHoraInicio || '').localeCompare(String(a.fechaHoraInicio || '')));
        setCapacitaciones(lista);
      })
      .catch(() => setCapacitaciones([]));
    listCatalogo(CATALOGO_SLUGS.AREAS)
      .then((items) => setAreas(Array.isArray(items) ? items : []))
      .catch(() => setAreas([]));
  }, []);

  const cargar = useCallback(() => {
    // Si el filtro cambia antes de que llegue la respuesta, la respuesta vieja se descarta.
    const req = ++listReq.current;
    setLoading(true);
    setLoadError('');
    listInscritos({ capacitacionId, buscar: buscarAplicado })
      .then((items) => {
        if (listReq.current === req) setRows(Array.isArray(items) ? items : []);
      })
      .catch((err) => {
        if (listReq.current === req) setLoadError(mensajeError(err, 'No se pudo cargar la lista de inscritos.'));
      })
      .finally(() => {
        if (listReq.current === req) setLoading(false);
      });
  }, [capacitacionId, buscarAplicado]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  const handleBuscar = (event) => {
    event.preventDefault();
    setBuscarAplicado(buscar.trim());
  };

  // ---- Ver firma ----
  const abrirFirma = async (row) => {
    const req = ++firmaReq.current;
    setVerFirma({ inscrito: row, loading: true, error: '', dims: null });
    try {
      const detalle = await getInscrito(row.id);
      if (firmaReq.current !== req) return;
      setVerFirma({ inscrito: detalle, loading: false, error: '', dims: null });
    } catch (err) {
      if (firmaReq.current !== req) return;
      setVerFirma({ inscrito: row, loading: false, error: mensajeError(err, 'No se pudo cargar la firma.'), dims: null });
    }
  };

  const cerrarFirma = () => {
    firmaReq.current += 1;
    setVerFirma(null);
  };

  // ---- Editar ----
  const abrirEditar = async (row) => {
    const req = ++editReq.current;
    setEditando({ ...row });
    setEditLoading(true);
    setEditLoadError('');
    setFormError('');
    setReemplazarFirma(false);
    setFirmaNueva(null);
    try {
      const detalle = await getInscrito(row.id);
      if (editReq.current !== req) return;
      setEditando(detalle);
      setForm({
        nombres: detalle.nombres || '',
        apellidos: detalle.apellidos || '',
        identificacion: detalle.identificacion || '',
        areaId: detalle.areaId || '',
        emailUsuario: detalle.emailUsuario || '',
      });
      // Sin firma guardada: se pide directamente una nueva.
      setReemplazarFirma(!detalle.firma);
    } catch (err) {
      if (editReq.current !== req) return;
      setEditLoadError(mensajeError(err, 'No se pudo cargar el inscrito.'));
    } finally {
      if (editReq.current === req) setEditLoading(false);
    }
  };

  const cerrarEditar = () => {
    if (saving) return;
    editReq.current += 1;
    setEditLoading(false);
    setEditLoadError('');
    setEditando(null);
    setForm(EMPTY_FORM);
    setFirmaNueva(null);
    setReemplazarFirma(false);
    setFormError('');
  };

  const validar = () => {
    if (!form.nombres.trim()) return 'Ingresa los nombres.';
    if (!form.apellidos.trim()) return 'Ingresa los apellidos.';
    if (!form.identificacion.trim()) return 'Ingresa la cédula.';
    if (!form.areaId) return 'Selecciona un área.';
    if (!form.emailUsuario.trim()) return 'Ingresa el correo.';
    if (form.emailUsuario.includes('@')) {
      return 'Ingresa solo la parte del correo antes de @dos.com.ec.';
    }
    return null;
  };

  const guardar = async () => {
    if (!editando || saving || editLoadError) return;
    const err = validar();
    if (err) {
      setFormError(err);
      return;
    }
    setSaving(true);
    setFormError('');
    try {
      await updateInscrito(editando.id, {
        nombres: form.nombres.trim(),
        apellidos: form.apellidos.trim(),
        identificacion: form.identificacion.trim(),
        areaId: form.areaId,
        emailUsuario: form.emailUsuario.trim(),
        firma: reemplazarFirma && firmaNueva ? firmaNueva : null,
      });
      toast.success('Inscrito actualizado.');
      setSaving(false);
      cerrarEditar();
      cargar();
    } catch (error) {
      setFormError(mensajeError(error, 'No se pudo guardar.'));
      setSaving(false);
    }
  };

  const areasEdicion = useMemo(() => {
    const activas = areas.filter((a) => a.activo !== false);
    // Si el inscrito tiene un área que ya no está activa, se muestra para que se vea cuál era.
    if (editando?.areaId && !activas.some((a) => a.id === editando.areaId)) {
      return [{ id: editando.areaId, nombre: `${editando.areaNombre || 'Área'} (inactiva)`, inactiva: true }, ...activas];
    }
    return activas;
  }, [areas, editando]);

  const columns = [
    { key: 'identificacion', header: 'Cédula', width: '120px' },
    { key: 'nombres', header: 'Nombres' },
    { key: 'apellidos', header: 'Apellidos' },
    { key: 'areaNombre', header: 'Área', accessor: (r) => r.areaNombre || '—' },
    { key: 'email', header: 'Correo', accessor: (r) => r.email || '—' },
    {
      key: 'capacitacion',
      header: 'Capacitación',
      accessor: (r) => (
        <span title={r.capacitacionTema}>
          <strong>{r.capacitacionCodigo}</strong>
          <br />
          <span className="text-secondary">{r.capacitacionTema}</span>
        </span>
      ),
    },
    {
      key: 'fechaInscripcion',
      header: 'Inscripción',
      width: '150px',
      accessor: (r) => formatFechaHora(r.fechaInscripcion) || '—',
    },
    {
      key: 'tieneFirma',
      header: 'Firma',
      width: '80px',
      align: 'center',
      accessor: (r) =>
        r.tieneFirma ? (
          <span className="badge badge--active">Sí</span>
        ) : (
          <span className="badge badge--blocked">Falta</span>
        ),
    },
  ];

  const firmaActual = verFirma?.inscrito?.firma;

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Inscritos</h1>
          <p className="page-header__subtitle">
            Personas inscritas a las capacitaciones. Revisa sus datos y su firma, y corrígelos si hace falta.
          </p>
        </div>
      </div>

      <form className="toolbar" onSubmit={handleBuscar}>
        <div className={`toolbar__filters ${styles.filters}`}>
          <select
            className={`form-input form-select ${styles.filtroCapacitacion}`}
            aria-label="Filtrar por capacitación"
            value={capacitacionId}
            onChange={(e) => setCapacitacionId(e.target.value)}
          >
            <option value="">Todas las capacitaciones</option>
            {capacitaciones.map((c) => (
              <option key={c.id} value={c.id}>
                {c.codigo} — {c.tema}
              </option>
            ))}
          </select>
          <input
            type="search"
            className={`form-input ${styles.filtroBuscar}`}
            aria-label="Buscar por cédula, nombre o correo"
            placeholder="Buscar por cédula, nombre o correo"
            value={buscar}
            onChange={(e) => setBuscar(e.target.value)}
          />
          <button type="submit" className="btn btn--secondary">
            <Search width={16} height={16} />
            <span>Buscar</span>
          </button>
        </div>
        <div className="toolbar__actions">
          <span className={`text-secondary ${styles.contador}`}>{loading ? '' : `${rows.length} inscrito(s)`}</span>
        </div>
      </form>

      {loadError && (
        <div className={`alert alert--error ${styles.loadError}`} role="alert">
          {loadError}
        </div>
      )}

      <div className="card">
        <div className={`card__body ${styles.tableBody}`}>
          <DataTable
            columns={columns}
            rows={rows}
            loading={loading}
            emptyMessage="No hay inscritos con esos filtros."
            actions={(row) => (
              <div className={styles.rowActions}>
                <button
                  type="button"
                  className="btn btn--ghost btn--sm btn--icon"
                  title="Ver firma"
                  aria-label={`Ver firma de ${row.nombres} ${row.apellidos}`}
                  onClick={() => abrirFirma(row)}
                  disabled={!row.tieneFirma}
                >
                  <Eye width={16} height={16} />
                </button>
                <button
                  type="button"
                  className="btn btn--ghost btn--sm btn--icon"
                  title="Editar"
                  aria-label={`Editar a ${row.nombres} ${row.apellidos}`}
                  onClick={() => abrirEditar(row)}
                >
                  <Pencil width={16} height={16} />
                </button>
              </div>
            )}
          />
        </div>
      </div>

      {/* Modal: ver firma */}
      <Modal
        isOpen={Boolean(verFirma)}
        onClose={cerrarFirma}
        title={verFirma ? `Firma de ${verFirma.inscrito.nombres} ${verFirma.inscrito.apellidos}` : ''}
        footer={
          <>
            {verFirma?.inscrito?.firma && (
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => {
                  const row = verFirma.inscrito;
                  cerrarFirma();
                  abrirEditar(row);
                }}
              >
                <Pencil width={16} height={16} />
                <span>Editar</span>
              </button>
            )}
            <button type="button" className="btn btn--primary" onClick={cerrarFirma}>
              Cerrar
            </button>
          </>
        }
      >
        {verFirma?.loading && <Spinner size={28} label="Cargando firma..." />}
        {verFirma?.error && <div className="alert alert--error">{verFirma.error}</div>}
        {!verFirma?.loading && !verFirma?.error && firmaActual && (
          <div>
            <div className={styles.firmaBox}>
              <img
                src={firmaActual}
                alt="Firma del inscrito"
                className={styles.firmaImg}
                onLoad={(e) => {
                  const { naturalWidth, naturalHeight } = e.currentTarget;
                  setVerFirma((prev) => (prev ? { ...prev, dims: { w: naturalWidth, h: naturalHeight } } : prev));
                }}
              />
            </div>
            <p className={`form-helper ${styles.spaceTop}`}>
              {verFirma.dims ? `${verFirma.dims.w} × ${verFirma.dims.h} px · ` : ''}
              {pesoKb(firmaActual)} KB · {verFirma.inscrito.capacitacionCodigo} — {verFirma.inscrito.capacitacionTema}
            </p>
          </div>
        )}
      </Modal>

      {/* Modal: editar */}
      <Modal
        isOpen={Boolean(editando)}
        onClose={cerrarEditar}
        title={editando ? `Editar inscrito — ${editando.capacitacionCodigo || ''}` : ''}
        footer={
          <>
            <button type="button" className="btn btn--secondary" onClick={cerrarEditar} disabled={saving}>
              Cancelar
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={guardar}
              disabled={saving || editLoading || Boolean(editLoadError)}
            >
              {saving ? 'Guardando...' : 'Guardar'}
            </button>
          </>
        }
      >
        {editLoading ? (
          <Spinner size={28} label="Cargando..." />
        ) : editLoadError ? (
          <div className="alert alert--error" role="alert">
            {editLoadError}
          </div>
        ) : (
          <div>
            <div className="form-row">
              <div className="form-group">
                <label className="form-label form-label--required" htmlFor="ins-nombres">Nombres</label>
                <input
                  id="ins-nombres"
                  className="form-input"
                  value={form.nombres}
                  maxLength={120}
                  onChange={(e) => setForm((p) => ({ ...p, nombres: e.target.value }))}
                  disabled={saving}
                />
              </div>
              <div className="form-group">
                <label className="form-label form-label--required" htmlFor="ins-apellidos">Apellidos</label>
                <input
                  id="ins-apellidos"
                  className="form-input"
                  value={form.apellidos}
                  maxLength={120}
                  onChange={(e) => setForm((p) => ({ ...p, apellidos: e.target.value }))}
                  disabled={saving}
                />
              </div>
            </div>

            <div className="form-row">
              <div className="form-group">
                <label className="form-label form-label--required" htmlFor="ins-cedula">Cédula</label>
                <input
                  id="ins-cedula"
                  className="form-input"
                  value={form.identificacion}
                  maxLength={30}
                  onChange={(e) => setForm((p) => ({ ...p, identificacion: e.target.value }))}
                  disabled={saving}
                />
              </div>
              <div className="form-group">
                <label className="form-label form-label--required" htmlFor="ins-area">Área</label>
                <select
                  id="ins-area"
                  className="form-input form-select"
                  value={form.areaId}
                  onChange={(e) => setForm((p) => ({ ...p, areaId: e.target.value }))}
                  disabled={saving}
                >
                  <option value="">Selecciona un área</option>
                  {areasEdicion.map((a) => (
                    <option key={a.id} value={a.id} disabled={a.inactiva}>
                      {a.nombre}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="form-group">
              <label className="form-label form-label--required" htmlFor="ins-email">Correo</label>
              <EmailConSufijo
                id="ins-email"
                name="ins-email"
                value={form.emailUsuario}
                onChange={(value) => setForm((p) => ({ ...p, emailUsuario: value }))}
                disabled={saving}
                required
              />
            </div>

            <div className="form-group">
              <label className="form-label">Firma</label>
              {editando?.firma && !reemplazarFirma && (
                <div>
                  <div className={`${styles.firmaBox} ${styles.firmaBoxCompacta}`}>
                    <img src={editando.firma} alt="Firma actual" className={styles.firmaImgCompacta} />
                  </div>
                  <button
                    type="button"
                    className={`btn btn--secondary btn--sm ${styles.spaceTop}`}
                    onClick={() => setReemplazarFirma(true)}
                    disabled={saving}
                  >
                    Reemplazar firma
                  </button>
                </div>
              )}
              {reemplazarFirma && (
                <div>
                  <SignaturePad
                    value={firmaNueva}
                    onChange={setFirmaNueva}
                    width={400}
                    height={150}
                    disabled={saving}
                  />
                  {editando?.firma && (
                    <button
                      type="button"
                      className={`btn btn--ghost btn--sm ${styles.spaceTop}`}
                      onClick={() => {
                        setReemplazarFirma(false);
                        setFirmaNueva(null);
                      }}
                      disabled={saving}
                    >
                      Mantener la firma actual
                    </button>
                  )}
                </div>
              )}
              {!editando?.firma && !firmaNueva && (
                <p className="form-helper">
                  Este inscrito no tiene firma. Puedes dibujarla o subirla, o guardar sin firma.
                </p>
              )}
              <p className="form-helper">
                Los cambios también se guardan en el registro de personas que autocompleta la inscripción.
              </p>
            </div>

            {formError && (
              <div className="alert alert--error" role="alert">
                {formError}
              </div>
            )}
          </div>
        )}
      </Modal>
    </div>
  );
}
