import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Pencil, Trash2, RotateCcw } from 'lucide-react';
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
 * a un Tipo de Actividad — al finalizar una capacitación, se muestran a los
 * asistentes las preguntas activas cuyo tipo coincida con el de la capacitación.
 */
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

  const openCreate = () => {
    setEditing(null);
    setTipoActividadId(filtroTipoId || '');
    setTexto('');
    setActivo(true);
    setErrors({});
    setFormOpen(true);
  };

  const openEdit = (row) => {
    setEditing(row);
    setTipoActividadId(row?.tipoActividadId || '');
    setTexto(row?.texto || '');
    setActivo(Boolean(row?.activo));
    setErrors({});
    setFormOpen(true);
  };

  const closeForm = () => {
    if (submitting) return;
    setFormOpen(false);
  };

  const validate = () => {
    const next = {};
    if (!tipoActividadId) next.tipoActividadId = 'Selecciona un tipo de actividad.';
    const t = (texto || '').trim();
    if (!t) next.texto = 'El texto de la pregunta es obligatorio.';
    else if (t.length > 500) next.texto = 'Máximo 500 caracteres.';
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSubmit = async (event) => {
    event?.preventDefault?.();
    if (!validate()) return;
    setSubmitting(true);
    try {
      const payload = { tipoActividadId, texto: texto.trim(), activo };
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
        width: '200px',
        accessor: (row) => row?.tipoActividadNombre || '—',
      },
      {
        key: 'texto',
        header: 'Pregunta',
        accessor: (row) => row?.texto || '—',
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
            Banco de preguntas de satisfacción por tipo de actividad. Al finalizar una
            capacitación, los asistentes verán las preguntas activas cuyo tipo coincida.
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
              Pregunta
            </label>
            <textarea
              className={`form-input${errors.texto ? ' form-input--error' : ''}`}
              rows={4}
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
