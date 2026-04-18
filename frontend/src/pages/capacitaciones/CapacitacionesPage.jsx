import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Search } from 'lucide-react';
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
import { matchesSearch } from '../../utils/search.js';
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
  // El check controla qué se muestra por *default*. El backend siempre devuelve TODO
  // (incluyendo inactivas + finalizadas) para que el input de búsqueda pueda encontrar
  // también capacitaciones pasadas, aunque el check esté desmarcado.
  const [includeInactive, setIncludeInactive] = useState(false);
  const [search, setSearch] = useState('');

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
    async () => {
      setLoading(true);
      try {
        // Siempre traemos todo; el filtrado (check + búsqueda) es client-side.
        const data = await listCapacitaciones(true);
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
    fetchItems();
  }, [fetchItems]);

  // Vista filtrada.
  // Reglas:
  //  - Si hay `search` → busca sobre TODAS las capacitaciones (ignora el check). Permite
  //    encontrar por ejemplo una capacitación pasada aunque "Mostrar inactivas" esté off.
  //  - Si no hay search y `includeInactive` está ON → mostrar todas.
  //  - Si no hay search y `includeInactive` está OFF → ocultar finalizadas y las inactivas.
  const visibleItems = useMemo(() => {
    const q = search.trim();
    if (q) {
      return items.filter((x) => matchesSearch(x, q));
    }
    if (includeInactive) return items;
    return items.filter((x) => x.activo !== false && x.estado !== 'Finalizada');
  }, [items, search, includeInactive]);

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
    fetchItems();
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
      await fetchItems();
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
        <div className={`toolbar__filters ${styles.toolbarFilters}`}>
          <div className={styles.searchBox}>
            <Search width={16} height={16} className={styles.searchIcon} aria-hidden="true" />
            <input
              type="search"
              className={styles.searchInput}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Buscar en todas las capacitaciones..."
              aria-label="Buscar capacitaciones"
            />
          </div>
          <Toggle
            label="Mostrar finalizadas e inactivas"
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
      ) : visibleItems.length === 0 ? (
        <div className="card">
          <div className="card__body">
            <div className="empty-state">
              <div className="empty-state__title">
                {search.trim()
                  ? 'Sin resultados para tu búsqueda'
                  : items.length === 0
                    ? 'Aún no hay capacitaciones'
                    : 'No hay capacitaciones para mostrar'}
              </div>
              <p className="empty-state__description">
                {search.trim()
                  ? `Ningún campo coincide con "${search.trim()}". Prueba con otro término.`
                  : items.length === 0
                    ? 'Crea la primera capacitación para empezar a gestionar inscripciones y responsables.'
                    : 'Marca "Mostrar finalizadas e inactivas" para verlas aquí.'}
              </p>
            </div>
          </div>
        </div>
      ) : (
        <div className={styles.gridScroll}>
          <div className={styles.grid}>
            {visibleItems.map((cap) => (
              <CapacitacionCard
                key={cap.id}
                capacitacion={cap}
                onEdit={openEdit}
                onDelete={requestDelete}
              />
            ))}
          </div>
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
