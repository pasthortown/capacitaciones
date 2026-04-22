import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Pencil, Trash2, RotateCcw, X, GripVertical } from 'lucide-react';
import DataTable from '../../components/Table/DataTable.jsx';
import Modal from '../../components/Modal/Modal.jsx';
import Toggle from '../../components/Forms/Toggle.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { confirm as swalConfirm } from '../../utils/swal.js';
import { list as listTipos } from '../../services/catalogos.js';
import {
  list as listPreguntas,
  create as createPregunta,
  update as updatePregunta,
  remove as removePregunta,
} from '../../services/preguntasEncuesta.js';

/**
 * CRUD de preguntas de encuesta de satisfacción. Cada pregunta está asociada
 * a un Tipo de Actividad y tiene un tipo propio (Selección múltiple, Texto largo
 * o Sí/No). Para Selección múltiple el admin define la lista de opciones.
 */

const TIPOS_PREGUNTA = [
  { value: 'SeleccionMultiple', label: 'Selección múltiple' },
  { value: 'TextoLargo', label: 'Texto largo (comentarios)' },
  { value: 'SiNo', label: 'Sí / No' },
];

const TIPO_LABEL = Object.fromEntries(TIPOS_PREGUNTA.map((t) => [t.value, t.label]));

export default function PreguntasEncuestaPage() {
  const toast = useToast();

  const [tipos, setTipos] = useState([]);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [filtroTipoId, setFiltroTipoId] = useState('');

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState(null);
  const [tipoActividadId, setTipoActividadId] = useState('');
  const [texto, setTexto] = useState('');
  const [tipoPregunta, setTipoPregunta] = useState('SeleccionMultiple');
  const [opciones, setOpciones] = useState(['', '']);
  const [activo, setActivo] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [errors, setErrors] = useState({});

  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      const data = await listPreguntas({
        tipoActividadId: filtroTipoId || undefined,
        includeInactive,
      });
      if (mounted.current) setItems(Array.isArray(data) ? data : []);
    } catch (err) {
      if (mounted.current) setItems([]);
      toast.error(err?.message || 'No se pudieron cargar las preguntas.');
    } finally {
      if (mounted.current) setLoading(false);
    }
  }, [filtroTipoId, includeInactive, toast]);

  useEffect(() => {
    listTipos('tipos-actividad', { includeInactive: false })
      .then((data) => {
        if (mounted.current) setTipos(Array.isArray(data) ? data : []);
      })
      .catch((err) => toast.error(err?.message || 'No se pudieron cargar los tipos de actividad.'));
  }, [toast]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const resetForm = () => {
    setTipoActividadId(filtroTipoId || '');
    setTexto('');
    setTipoPregunta('SeleccionMultiple');
    setOpciones(['', '']);
    setActivo(true);
    setErrors({});
  };

  const openCreate = () => {
    setEditing(null);
    resetForm();
    setFormOpen(true);
  };

  const openEdit = (row) => {
    setEditing(row);
    setTipoActividadId(row?.tipoActividadId || '');
    setTexto(row?.texto || '');
    setTipoPregunta(row?.tipoPregunta || 'SeleccionMultiple');
    const ops = Array.isArray(row?.opciones) ? row.opciones.slice() : [];
    setOpciones(ops.length > 0 ? ops : ['', '']);
    setActivo(Boolean(row?.activo));
    setErrors({});
    setFormOpen(true);
  };

  const closeForm = () => {
    if (submitting) return;
    setFormOpen(false);
  };

  const handleTipoPreguntaChange = (next) => {
    setTipoPregunta(next);
    if (errors.opciones) setErrors((prev) => ({ ...prev, opciones: '' }));
    // Al cambiar a Selección múltiple y no hay opciones, precargar 2 vacías.
    if (next === 'SeleccionMultiple' && (!opciones || opciones.length < 2)) {
      setOpciones(['', '']);
    }
  };

  const setOpcion = (idx, value) => {
    setOpciones((prev) => prev.map((op, i) => (i === idx ? value : op)));
    if (errors.opciones) setErrors((prev) => ({ ...prev, opciones: '' }));
  };

  const agregarOpcion = () => {
    setOpciones((prev) => [...prev, '']);
  };

  const quitarOpcion = (idx) => {
    setOpciones((prev) => prev.filter((_, i) => i !== idx));
  };

  const validate = () => {
    const next = {};
    if (!tipoActividadId) next.tipoActividadId = 'Selecciona un tipo de actividad.';
    const t = (texto || '').trim();
    if (!t) next.texto = 'El texto de la pregunta es obligatorio.';
    else if (t.length > 500) next.texto = 'Máximo 500 caracteres.';

    if (tipoPregunta === 'SeleccionMultiple') {
      const limpias = opciones.map((o) => (o || '').trim()).filter((o) => o.length > 0);
      const setOpts = new Set(limpias.map((o) => o.toLowerCase()));
      if (limpias.length < 2) next.opciones = 'Agrega al menos 2 opciones.';
      else if (setOpts.size !== limpias.length) next.opciones = 'Las opciones no pueden repetirse.';
      else if (limpias.some((o) => o.length > 200))
        next.opciones = 'Cada opción admite máximo 200 caracteres.';
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSubmit = async (event) => {
    event?.preventDefault?.();
    if (!validate()) return;
    setSubmitting(true);
    try {
      const payload = {
        tipoActividadId,
        texto: texto.trim(),
        tipoPregunta,
        opciones:
          tipoPregunta === 'SeleccionMultiple'
            ? opciones.map((o) => (o || '').trim()).filter((o) => o.length > 0)
            : [],
        activo,
      };
      if (editing) {
        await updatePregunta(editing.id, payload);
        toast.success('Pregunta actualizada.');
      } else {
        await createPregunta(payload);
        toast.success('Pregunta creada.');
      }
      setFormOpen(false);
      await refresh();
    } catch (err) {
      toast.error(err?.message || 'No se pudo guardar la pregunta.');
    } finally {
      if (mounted.current) setSubmitting(false);
    }
  };

  const handleDelete = async (row) => {
    const confirmed = await swalConfirm({
      title: 'Eliminar pregunta',
      text: 'La pregunta se marcará como inactiva y dejará de aparecer en nuevas encuestas.',
      icon: 'warning',
      confirmText: 'Sí, eliminar',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmed) return;
    try {
      await removePregunta(row.id);
      toast.success('Pregunta eliminada.');
      await refresh();
    } catch (err) {
      toast.error(err?.message || 'No se pudo eliminar la pregunta.');
    }
  };

  const handleReactivate = async (row) => {
    try {
      await updatePregunta(row.id, {
        tipoActividadId: row.tipoActividadId,
        texto: row.texto,
        tipoPregunta: row.tipoPregunta,
        opciones: Array.isArray(row.opciones) ? row.opciones : [],
        activo: true,
      });
      toast.success('Pregunta reactivada.');
      await refresh();
    } catch (err) {
      toast.error(err?.message || 'No se pudo reactivar.');
    }
  };

  const columns = useMemo(
    () => [
      {
        key: 'tipoActividad',
        header: 'Tipo de actividad',
        width: '180px',
        accessor: (row) => row?.tipoActividadNombre || '—',
      },
      {
        key: 'tipoPregunta',
        header: 'Formato',
        width: '170px',
        accessor: (row) => TIPO_LABEL[row?.tipoPregunta] || row?.tipoPregunta || '—',
      },
      {
        key: 'texto',
        header: 'Pregunta',
        accessor: (row) => (
          <div>
            <div>{row?.texto || '—'}</div>
            {row?.tipoPregunta === 'SeleccionMultiple' && Array.isArray(row?.opciones) && row.opciones.length > 0 && (
              <div className="text-xs text-secondary" style={{ marginTop: 4 }}>
                Opciones: {row.opciones.join(' · ')}
              </div>
            )}
          </div>
        ),
      },
      {
        key: 'activo',
        header: 'Estado',
        width: '120px',
        accessor: (row) => (
          <span className={`badge ${row?.activo ? 'badge--active' : 'badge--inactive'}`}>
            {row?.activo ? 'Activa' : 'Inactiva'}
          </span>
        ),
      },
    ],
    [],
  );

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Preguntas de encuesta</h1>
          <p className="page-header__subtitle">
            Banco de preguntas de satisfacción por tipo de actividad. Cada pregunta puede
            ser de selección múltiple (con opciones), texto largo (comentarios) o Sí/No.
          </p>
        </div>
      </div>

      <div className="toolbar">
        <div className="toolbar__filters" style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
          <div className="form-group" style={{ marginBottom: 0, minWidth: 220 }}>
            <label className="form-label" style={{ position: 'static' }}>
              Filtrar por tipo de actividad
            </label>
            <select
              className="form-input"
              value={filtroTipoId}
              onChange={(e) => setFiltroTipoId(e.target.value)}
            >
              <option value="">Todos</option>
              {tipos.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nombre}
                </option>
              ))}
            </select>
          </div>
          <Toggle
            label="Mostrar inactivas"
            checked={includeInactive}
            onChange={setIncludeInactive}
          />
        </div>
        <div className="toolbar__actions">
          <button type="button" className="btn btn--primary" onClick={openCreate}>
            <Plus width={16} height={16} />
            <span>Nueva pregunta</span>
          </button>
        </div>
      </div>

      <div className="card">
        <div className="card__body" style={{ padding: 0 }}>
          <DataTable
            columns={columns}
            rows={items}
            loading={loading}
            emptyMessage="Aún no hay preguntas para mostrar."
            actions={(row) => (
              <>
                <button
                  type="button"
                  className="btn btn--ghost btn--sm btn--icon"
                  onClick={() => openEdit(row)}
                  aria-label="Editar pregunta"
                  title="Editar"
                >
                  <Pencil width={16} height={16} />
                </button>
                {row?.activo ? (
                  <button
                    type="button"
                    className="btn btn--ghost btn--sm btn--icon"
                    onClick={() => handleDelete(row)}
                    aria-label="Eliminar pregunta"
                    title="Eliminar (marcar como inactiva)"
                  >
                    <Trash2 width={16} height={16} />
                  </button>
                ) : (
                  <button
                    type="button"
                    className="btn btn--ghost btn--sm btn--icon"
                    onClick={() => handleReactivate(row)}
                    aria-label="Reactivar pregunta"
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

      <Modal
        isOpen={formOpen}
        onClose={closeForm}
        title={editing ? 'Editar pregunta' : 'Nueva pregunta'}
        footer={
          <>
            <button type="button" className="btn btn--secondary" onClick={closeForm} disabled={submitting}>
              Cancelar
            </button>
            <button type="button" className="btn btn--primary" onClick={handleSubmit} disabled={submitting}>
              {submitting ? 'Guardando...' : 'Guardar'}
            </button>
          </>
        }
      >
        <form onSubmit={handleSubmit} noValidate>
          <div className="form-group" style={{ position: 'static' }}>
            <label className="form-label form-label--required" style={{ position: 'static' }}>
              Tipo de actividad
            </label>
            <select
              className={`form-input${errors.tipoActividadId ? ' form-input--error' : ''}`}
              value={tipoActividadId}
              onChange={(e) => {
                setTipoActividadId(e.target.value);
                if (errors.tipoActividadId) setErrors((prev) => ({ ...prev, tipoActividadId: '' }));
              }}
              required
            >
              <option value="">— Selecciona —</option>
              {tipos.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nombre}
                </option>
              ))}
            </select>
            {errors.tipoActividadId && (
              <div className="form-helper form-helper--error">{errors.tipoActividadId}</div>
            )}
          </div>

          <div className="form-group" style={{ position: 'static', marginTop: 12 }}>
            <label className="form-label form-label--required" style={{ position: 'static' }}>
              Formato de la pregunta
            </label>
            <select
              className="form-input"
              value={tipoPregunta}
              onChange={(e) => handleTipoPreguntaChange(e.target.value)}
            >
              {TIPOS_PREGUNTA.map((t) => (
                <option key={t.value} value={t.value}>
                  {t.label}
                </option>
              ))}
            </select>
            <div className="form-helper">
              {tipoPregunta === 'SeleccionMultiple'
                ? 'El asistente elige una de las opciones que definas.'
                : tipoPregunta === 'TextoLargo'
                ? 'El asistente escribe un comentario libre.'
                : 'El asistente responde "Sí" o "No".'}
            </div>
          </div>

          <div className="form-group" style={{ position: 'static', marginTop: 12 }}>
            <label className="form-label form-label--required" style={{ position: 'static' }}>
              Pregunta
            </label>
            <textarea
              className={`form-input${errors.texto ? ' form-input--error' : ''}`}
              rows={3}
              maxLength={500}
              value={texto}
              onChange={(e) => {
                setTexto(e.target.value);
                if (errors.texto) setErrors((prev) => ({ ...prev, texto: '' }));
              }}
              placeholder="Ej. ¿Qué tan satisfecho estás con la claridad del contenido?"
            />
            {errors.texto ? (
              <div className="form-helper form-helper--error">{errors.texto}</div>
            ) : (
              <div className="form-helper">Máximo 500 caracteres.</div>
            )}
          </div>

          {tipoPregunta === 'SeleccionMultiple' && (
            <div className="form-group" style={{ position: 'static', marginTop: 12 }}>
              <label className="form-label form-label--required" style={{ position: 'static' }}>
                Opciones
              </label>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {opciones.map((op, idx) => (
                  <div key={idx} style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                    <GripVertical width={14} height={14} style={{ color: '#999', flexShrink: 0 }} />
                    <input
                      className="form-input"
                      value={op}
                      onChange={(e) => setOpcion(idx, e.target.value)}
                      placeholder={`Opción ${idx + 1}`}
                      maxLength={200}
                    />
                    {opciones.length > 2 && (
                      <button
                        type="button"
                        className="btn btn--ghost btn--sm btn--icon"
                        onClick={() => quitarOpcion(idx)}
                        aria-label="Quitar opción"
                        title="Quitar"
                      >
                        <X width={14} height={14} />
                      </button>
                    )}
                  </div>
                ))}
              </div>
              <div style={{ marginTop: 8 }}>
                <button
                  type="button"
                  className="btn btn--ghost btn--sm"
                  onClick={agregarOpcion}
                  disabled={opciones.length >= 10}
                >
                  <Plus width={14} height={14} />
                  <span>Agregar opción</span>
                </button>
              </div>
              {errors.opciones ? (
                <div className="form-helper form-helper--error">{errors.opciones}</div>
              ) : (
                <div className="form-helper">Entre 2 y 10 opciones. Máximo 200 caracteres cada una.</div>
              )}
            </div>
          )}

          <div style={{ marginTop: 12 }}>
            <Toggle
              label={activo ? 'Activa' : 'Inactiva'}
              checked={activo}
              onChange={setActivo}
            />
          </div>
          <button type="submit" style={{ display: 'none' }} aria-hidden="true" />
        </form>
      </Modal>
    </div>
  );
}
