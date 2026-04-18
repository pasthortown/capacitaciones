import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Upload, Link2, Pencil, Trash2, Search } from 'lucide-react';
import DataTable from '../../components/Table/DataTable.jsx';
import Spinner from '../../components/Spinner/Spinner.jsx';
import RecursoUploadModal from '../../components/RecursoUploadModal/RecursoUploadModal.jsx';
import RecursoEditModal from '../../components/RecursoEditModal/RecursoEditModal.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { formatFechaHora } from '../../utils/formatters.js';
import { matchesSearch } from '../../utils/search.js';
import { confirm as swalConfirm, showCopyableValue } from '../../utils/swal.js';
import {
  listRecursos,
  deleteRecurso,
  getDownloadLink,
  formatBytes,
} from '../../services/recursos.js';
import styles from './RepositorioPage.module.css';

/**
 * Repositorio admin — lista los recursos subidos con enlaces públicos
 * de descarga. Permite subir nuevos, editar metadatos, copiar el link
 * y eliminar.
 */
const MAX_DESCRIPCION_PREVIEW = 80;

function truncate(text, max = MAX_DESCRIPCION_PREVIEW) {
  if (!text) return '';
  if (text.length <= max) return text;
  return `${text.slice(0, max - 1)}…`;
}

export default function RepositorioPage() {
  const toast = useToast();

  const [recursos, setRecursos] = useState([]);
  const [loading, setLoading] = useState(false);
  const [uploadOpen, setUploadOpen] = useState(false);
  const [editTarget, setEditTarget] = useState(null);
  const [copiandoId, setCopiandoId] = useState(null);
  const [search, setSearch] = useState('');

  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const data = await listRecursos(false);
      if (mountedRef.current) {
        setRecursos(Array.isArray(data) ? data : []);
      }
    } catch (err) {
      if (mountedRef.current) setRecursos([]);
      toast.error(err?.message || 'No se pudieron cargar los recursos.');
    } finally {
      if (mountedRef.current) setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    fetchList();
  }, [fetchList]);

  const sortedItems = useMemo(() => {
    if (!Array.isArray(recursos)) return [];
    const base = [...recursos].sort((a, b) => {
      const da = a?.fechaCreacion ? new Date(a.fechaCreacion).getTime() : 0;
      const db = b?.fechaCreacion ? new Date(b.fechaCreacion).getTime() : 0;
      return db - da;
    });
    const q = search.trim();
    if (!q) return base;
    return base.filter((r) => matchesSearch(r, q));
  }, [recursos, search]);

  // ---------- Copiar enlace público ----------
  // Intenta copiar vía Clipboard API con fallback a textarea+execCommand.
  // Si ambos fallan, retorna false y el caller muestra un diálogo copiable.
  const copyToClipboard = async (url) => {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      try {
        await navigator.clipboard.writeText(url);
        return true;
      } catch {
        // fallthrough al fallback
      }
    }
    try {
      const tmp = document.createElement('textarea');
      tmp.value = url;
      tmp.setAttribute('readonly', '');
      tmp.style.position = 'absolute';
      tmp.style.left = '-9999px';
      document.body.appendChild(tmp);
      tmp.select();
      const ok = document.execCommand('copy');
      document.body.removeChild(tmp);
      if (ok) return true;
    } catch {
      // ignore
    }
    return false;
  };

  const handleCopyLink = async (row) => {
    if (!row?.id || copiandoId === row.id) return;
    setCopiandoId(row.id);
    try {
      const response = await getDownloadLink(row.id);
      const fullUrl = `${window.location.origin}${response.url}`;
      const copied = await copyToClipboard(fullUrl);
      if (copied) {
        const shortUrl =
          fullUrl.length > 80 ? `${fullUrl.slice(0, 77)}…` : fullUrl;
        toast.success(`Enlace copiado: ${shortUrl}`);
      } else {
        // Sin clipboard API disponible: diálogo con el valor listo para seleccionar.
        await showCopyableValue({
          title: 'Enlace de descarga',
          text: 'Copia el enlace manualmente:',
          value: fullUrl,
        });
      }
    } catch (err) {
      toast.error(err?.message || 'No se pudo generar el enlace.');
    } finally {
      if (mountedRef.current) setCopiandoId(null);
    }
  };

  // ---------- Eliminar ----------
  const handleDelete = async (row) => {
    const nombre = row?.nombreOriginal || 'este recurso';
    const confirmed = await swalConfirm({
      title: 'Eliminar recurso',
      text: `"${nombre}" se eliminará y su enlace público dejará de funcionar.`,
      icon: 'warning',
      confirmText: 'Sí, eliminar',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmed) return;
    try {
      await deleteRecurso(row.id);
      toast.success('Recurso eliminado.');
      await fetchList();
    } catch (err) {
      toast.error(err?.message || 'No se pudo eliminar el recurso.');
    }
  };

  // ---------- Columnas ----------
  const columns = useMemo(
    () => [
      {
        key: 'nombreOriginal',
        header: 'Nombre',
        accessor: (row) => (
          <div className={styles.nombreCell} title={row?.nombreOriginal}>
            {row?.nombreOriginal || '—'}
          </div>
        ),
      },
      {
        key: 'descripcion',
        header: 'Descripción',
        accessor: (row) => (
          <div className={styles.descripcionCell} title={row?.descripcion || ''}>
            {truncate(row?.descripcion || '') || '—'}
          </div>
        ),
      },
      {
        key: 'tamanoBytes',
        header: 'Tamaño',
        width: '120px',
        align: 'right',
        accessor: (row) => formatBytes(row?.tamanoBytes ?? 0),
      },
      {
        key: 'fechaCreacion',
        header: 'Fecha',
        width: '170px',
        accessor: (row) => formatFechaHora(row?.fechaCreacion) || '—',
      },
    ],
    [],
  );

  const isEmpty = !loading && sortedItems.length === 0;

  return (
    <div>
      {/* Header */}
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Repositorio</h1>
          <p className="page-header__subtitle">
            Material compartido con enlaces públicos de descarga.
          </p>
        </div>
        <div>
          <button
            type="button"
            className="btn btn--primary"
            onClick={() => setUploadOpen(true)}
          >
            <Upload width={16} height={16} />
            <span>Subir recurso</span>
          </button>
        </div>
      </div>

      {/* Toolbar de búsqueda */}
      <div className="toolbar">
        <div className={`toolbar__filters ${styles.toolbarFilters}`}>
          <div className={styles.searchBox}>
            <Search width={16} height={16} className={styles.searchIcon} aria-hidden="true" />
            <input
              type="search"
              className={styles.searchInput}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Buscar en todo el material..."
              aria-label="Buscar recursos"
            />
          </div>
        </div>
      </div>

      {/* Tabla o empty state */}
      <div className="card">
        <div className="card__body" style={{ padding: 0 }}>
          {loading ? (
            <div className={styles.loadingWrapper}>
              <Spinner size={32} label="Cargando recursos..." />
            </div>
          ) : isEmpty ? (
            <div className="empty-state" style={{ padding: 32 }}>
              <div className="empty-state__title">Aún no hay recursos</div>
              <p className="empty-state__description">
                Sube el primer archivo para compartirlo mediante un enlace
                público.
              </p>
              <div className={styles.emptyStateActions}>
                <button
                  type="button"
                  className="btn btn--primary"
                  onClick={() => setUploadOpen(true)}
                >
                  <Upload width={16} height={16} />
                  <span>Subir primer recurso</span>
                </button>
              </div>
            </div>
          ) : (
            <DataTable
              columns={columns}
              rows={sortedItems}
              loading={false}
              emptyMessage="No hay recursos."
              actions={(row) => (
                <>
                  <button
                    type="button"
                    className="btn btn--ghost btn--sm btn--icon"
                    onClick={() => handleCopyLink(row)}
                    disabled={copiandoId === row.id}
                    aria-label={`Copiar enlace de ${row.nombreOriginal}`}
                    title="Copiar enlace público"
                  >
                    <Link2 width={16} height={16} />
                  </button>
                  <button
                    type="button"
                    className="btn btn--ghost btn--sm btn--icon"
                    onClick={() => setEditTarget(row)}
                    aria-label={`Editar ${row.nombreOriginal}`}
                    title="Editar"
                  >
                    <Pencil width={16} height={16} />
                  </button>
                  <button
                    type="button"
                    className="btn btn--ghost btn--sm btn--icon"
                    onClick={() => handleDelete(row)}
                    aria-label={`Eliminar ${row.nombreOriginal}`}
                    title="Eliminar"
                  >
                    <Trash2 width={16} height={16} />
                  </button>
                </>
              )}
            />
          )}
        </div>
      </div>

      {/* Modal subir */}
      <RecursoUploadModal
        isOpen={uploadOpen}
        onClose={() => setUploadOpen(false)}
        onSaved={fetchList}
      />

      {/* Modal editar */}
      <RecursoEditModal
        isOpen={Boolean(editTarget)}
        recurso={editTarget}
        onClose={() => setEditTarget(null)}
        onSaved={fetchList}
      />
    </div>
  );
}
