import { useCallback, useEffect, useRef, useState } from 'react';
import { Plus } from 'lucide-react';
import CapacitacionCard from '../../components/CapacitacionCard/CapacitacionCard.jsx';
import CapacitacionFormModal from '../../components/CapacitacionFormModal/CapacitacionFormModal.jsx';
import Modal from '../../components/Modal/Modal.jsx';
import Toggle from '../../components/Forms/Toggle.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import {
  listCapacitaciones,
  deleteCapacitacion,
} from '../../services/capacitaciones.js';
import styles from './CapacitacionesPage.module.css';

/**
 * Dashboard principal del módulo. Lista todas las capacitaciones en grid de cards.
 *
 * Acciones:
 *   - Nueva capacitación (modal).
 *   - Mostrar inactivas (toggle).
 *   - Por card: editar, eliminar, copiar links (capacitador/inscripción),
 *     navegar a asistentes.
 */
export default function CapacitacionesPage() {
  const toast = useToast();

  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [includeInactive, setIncludeInactive] = useState(false);

  // Modal crear/editar
  const [formOpen, setFormOpen] = useState(false);
  const [formMode, setFormMode] = useState('create');
  const [editing, setEditing] = useState(null);

  // Modal confirmación de eliminación
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const fetchItems = useCallback(
    async (flag) => {
      setLoading(true);
      try {
        const data = await listCapacitaciones(flag);
        if (mountedRef.current) {
          setItems(Array.isArray(data) ? data : []);
        }
      } catch (err) {
        if (mountedRef.current) setItems([]);
        toast.error(err?.message || 'No se pudieron cargar las capacitaciones.');
      } finally {
        if (mountedRef.current) setLoading(false);
      }
    },
    [toast],
  );

  useEffect(() => {
    fetchItems(includeInactive);
  }, [fetchItems, includeInactive]);

  const openCreate = () => {
    setEditing(null);
    setFormMode('create');
    setFormOpen(true);
  };

  const openEdit = (row) => {
    setEditing(row);
    setFormMode('edit');
    setFormOpen(true);
  };

  const handleSaved = () => {
    fetchItems(includeInactive);
  };

  const requestDelete = (row) => {
    setDeleteTarget(row);
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await deleteCapacitacion(deleteTarget.id);
      toast.success('Capacitación eliminada.');
      setDeleteTarget(null);
      await fetchItems(includeInactive);
    } catch (err) {
      toast.error(err?.message || 'No se pudo eliminar la capacitación.');
    } finally {
      if (mountedRef.current) setDeleting(false);
    }
  };

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Capacitaciones</h1>
          <p className="page-header__subtitle">
            Listado y gestión de capacitaciones registradas.
          </p>
        </div>
      </div>

      <div className="toolbar">
        <div className="toolbar__filters">
          <Toggle
            label="Mostrar inactivas"
            checked={includeInactive}
            onChange={setIncludeInactive}
          />
        </div>
        <div className="toolbar__actions">
          <button type="button" className="btn btn--primary" onClick={openCreate}>
            <Plus width={16} height={16} />
            <span>Nueva capacitación</span>
          </button>
        </div>
      </div>

      {loading ? (
        <div className={styles.loadingWrap}>
          <Spinner size={32} />
        </div>
      ) : items.length === 0 ? (
        <div className="card">
          <div className="card__body">
            <div className="empty-state">
              <div className="empty-state__title">Aún no hay capacitaciones</div>
              <p className="empty-state__description">
                Crea la primera capacitación para empezar a gestionar inscripciones
                y responsables.
              </p>
            </div>
          </div>
        </div>
      ) : (
        <div className={styles.grid}>
          {items.map((cap) => (
            <CapacitacionCard
              key={cap.id}
              capacitacion={cap}
              onEdit={openEdit}
              onDelete={requestDelete}
            />
          ))}
        </div>
      )}

      {/* Modal crear / editar */}
      <CapacitacionFormModal
        isOpen={formOpen}
        mode={formMode}
        initialValue={editing}
        onClose={() => setFormOpen(false)}
        onSaved={handleSaved}
      />

      {/* Modal confirmación eliminación */}
      <Modal
        isOpen={Boolean(deleteTarget)}
        onClose={() => !deleting && setDeleteTarget(null)}
        title="Eliminar capacitación"
        footer={
          <>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setDeleteTarget(null)}
              disabled={deleting}
            >
              Cancelar
            </button>
            <button
              type="button"
              className="btn btn--danger"
              onClick={confirmDelete}
              disabled={deleting}
            >
              {deleting ? 'Eliminando...' : 'Eliminar'}
            </button>
          </>
        }
      >
        <p>
          ¿Seguro que quieres eliminar la capacitación{' '}
          <strong>{deleteTarget?.codigo}</strong> — {deleteTarget?.tema}?
        </p>
        <p className="text-sm text-secondary">
          La eliminación es lógica: la capacitación se marcará como inactiva y
          no aparecerá por defecto en el listado.
        </p>
      </Modal>
    </div>
  );
}
