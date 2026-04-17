import { useMemo, useRef, useState } from 'react';
import { Plus, Download, Upload, Pencil, Trash2 } from 'lucide-react';
import useCatalogo from '../../hooks/useCatalogo.js';
import DataTable from '../../components/Table/DataTable.jsx';
import Modal from '../../components/Modal/Modal.jsx';
import TextField from '../../components/Forms/TextField.jsx';
import Toggle from '../../components/Forms/Toggle.jsx';

/**
 * Pantalla genérica para administrar un catálogo (CRUD + XLSX).
 *
 * Props:
 *   - tipo:       slug API (ej. "modalidades")
 *   - titulo:     título visible de la pantalla
 *   - descripcion: subtítulo opcional
 */
export default function CatalogoPage({ tipo, titulo, descripcion }) {
  const {
    items,
    loading,
    includeInactive,
    setIncludeInactive,
    refresh,
    create,
    update,
    remove,
    downloadTemplate,
    uploadTemplate,
  } = useCatalogo(tipo);

  // Modal CRUD --------------------------------------------------------------
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState(null); // null = crear
  const [nombre, setNombre] = useState('');
  const [activo, setActivo] = useState(true);
  const [nombreError, setNombreError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  // Modal resultado de importación -----------------------------------------
  const [importOpen, setImportOpen] = useState(false);
  const [importResult, setImportResult] = useState(null);
  const [importing, setImporting] = useState(false);

  const fileInputRef = useRef(null);

  const sortedItems = useMemo(() => {
    if (!Array.isArray(items)) return [];
    return [...items].sort((a, b) =>
      (a?.nombre || '').localeCompare(b?.nombre || '', 'es', { sensitivity: 'base' }),
    );
  }, [items]);

  // ---------- CRUD actions ----------
  const openCreate = () => {
    setEditing(null);
    setNombre('');
    setActivo(true);
    setNombreError('');
    setFormOpen(true);
  };

  const openEdit = (row) => {
    setEditing(row);
    setNombre(row?.nombre || '');
    setActivo(Boolean(row?.activo));
    setNombreError('');
    setFormOpen(true);
  };

  const closeForm = () => {
    if (submitting) return;
    setFormOpen(false);
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    const trimmed = (nombre || '').trim();
    if (!trimmed) {
      setNombreError('El nombre es obligatorio.');
      return;
    }
    if (trimmed.length > 255) {
      setNombreError('Máximo 255 caracteres.');
      return;
    }
    setSubmitting(true);
    try {
      if (editing) {
        await update(editing.id, { nombre: trimmed, activo });
      } else {
        await create({ nombre: trimmed, activo });
      }
      setFormOpen(false);
    } catch {
      // El hook ya mostró toast; mantener modal abierto para permitir corregir.
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async (row) => {
    const confirmed = window.confirm(
      `¿Eliminar "${row.nombre}"? Se marcará como inactivo.`,
    );
    if (!confirmed) return;
    try {
      await remove(row.id);
    } catch {
      // toast ya emitido
    }
  };

  // ---------- XLSX ----------
  const handleDownload = async () => {
    try {
      await downloadTemplate();
    } catch {
      // toast ya emitido
    }
  };

  const handleTriggerUpload = () => {
    if (!fileInputRef.current) return;
    fileInputRef.current.value = ''; // permite re-seleccionar el mismo archivo
    fileInputRef.current.click();
  };

  const handleFileSelected = async (event) => {
    const file = event.target.files?.[0];
    if (!file) return;
    setImporting(true);
    try {
      const resumen = await uploadTemplate(file);
      setImportResult(resumen);
      setImportOpen(true);
    } catch {
      // toast ya emitido
    } finally {
      setImporting(false);
    }
  };

  const closeImportModal = () => {
    const wasSuccess = (importResult?.filasValidas ?? 0) > 0;
    setImportOpen(false);
    setImportResult(null);
    if (wasSuccess) {
      refresh();
    }
  };

  // ---------- Columnas ----------
  const columns = useMemo(
    () => [
      {
        key: 'nombre',
        header: 'Nombre',
        accessor: (row) => row?.nombre || '—',
      },
      {
        key: 'activo',
        header: 'Activo',
        width: '120px',
        accessor: (row) => (
          <span className={`badge ${row?.activo ? 'badge--active' : 'badge--inactive'}`}>
            {row?.activo ? 'Activo' : 'Inactivo'}
          </span>
        ),
      },
      {
        key: 'fechaCreacion',
        header: 'Fecha creación',
        width: '200px',
        accessor: (row) => formatDate(row?.fechaCreacion),
      },
    ],
    [],
  );

  return (
    <div>
      {/* Header de página */}
      <div className="page-header">
        <div>
          <h1 className="page-header__title">{titulo}</h1>
          {descripcion && <p className="page-header__subtitle">{descripcion}</p>}
        </div>
      </div>

      {/* Toolbar de acciones */}
      <div className="toolbar">
        <div className="toolbar__filters">
          <Toggle
            label="Mostrar inactivos"
            checked={includeInactive}
            onChange={setIncludeInactive}
          />
        </div>
        <div className="toolbar__actions">
          <button type="button" className="btn btn--secondary" onClick={handleDownload}>
            <Download width={16} height={16} />
            <span>Descargar plantilla</span>
          </button>
          <button
            type="button"
            className="btn btn--secondary"
            onClick={handleTriggerUpload}
            disabled={importing}
          >
            <Upload width={16} height={16} />
            <span>{importing ? 'Subiendo...' : 'Subir plantilla'}</span>
          </button>
          <button type="button" className="btn btn--primary" onClick={openCreate}>
            <Plus width={16} height={16} />
            <span>Nuevo</span>
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            style={{ display: 'none' }}
            onChange={handleFileSelected}
          />
        </div>
      </div>

      {/* Tabla */}
      <div className="card">
        <div className="card__body" style={{ padding: 0 }}>
          <DataTable
            columns={columns}
            rows={sortedItems}
            loading={loading}
            emptyMessage="No hay registros para mostrar."
            actions={(row) => (
              <>
                <button
                  type="button"
                  className="btn btn--ghost btn--sm btn--icon"
                  onClick={() => openEdit(row)}
                  aria-label={`Editar ${row.nombre}`}
                  title="Editar"
                >
                  <Pencil width={16} height={16} />
                </button>
                <button
                  type="button"
                  className="btn btn--ghost btn--sm btn--icon"
                  onClick={() => handleDelete(row)}
                  aria-label={`Eliminar ${row.nombre}`}
                  title="Eliminar"
                >
                  <Trash2 width={16} height={16} />
                </button>
              </>
            )}
          />
        </div>
      </div>

      {/* Modal crear / editar */}
      <Modal
        isOpen={formOpen}
        onClose={closeForm}
        title={editing ? 'Editar registro' : 'Nuevo registro'}
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
              disabled={submitting}
            >
              {submitting ? 'Guardando...' : 'Guardar'}
            </button>
          </>
        }
      >
        <form onSubmit={handleSubmit} noValidate>
          <TextField
            label="Nombre"
            name="nombre"
            value={nombre}
            required
            maxLength={255}
            onChange={(value) => {
              setNombre(value);
              if (nombreError) setNombreError('');
            }}
            error={nombreError}
            helper="Máximo 255 caracteres."
          />
          <div style={{ marginTop: 12 }}>
            <Toggle
              label={activo ? 'Activo' : 'Inactivo'}
              checked={activo}
              onChange={setActivo}
            />
          </div>
          {/* Submit oculto para permitir envío con Enter */}
          <button type="submit" style={{ display: 'none' }} aria-hidden="true" />
        </form>
      </Modal>

      {/* Modal resultado de importación */}
      <Modal
        isOpen={importOpen}
        onClose={closeImportModal}
        title="Resultado de la importación"
        footer={
          <button type="button" className="btn btn--primary" onClick={closeImportModal}>
            Cerrar
          </button>
        }
      >
        <ImportSummary result={importResult} />
      </Modal>
    </div>
  );
}

/** Render del resumen de importación + tabla de errores. */
function ImportSummary({ result }) {
  if (!result) {
    return <p>Sin datos del servidor.</p>;
  }
  const { totalFilas = 0, filasValidas = 0, errores = [] } = result;
  const hasErrors = Array.isArray(errores) && errores.length > 0;

  return (
    <div>
      <div style={{ display: 'flex', gap: 16, marginBottom: 16, flexWrap: 'wrap' }}>
        <div style={{ minWidth: 160 }}>
          <div className="text-xs font-medium text-secondary">Total de filas</div>
          <div className="text-2xl font-bold">{totalFilas}</div>
        </div>
        <div style={{ minWidth: 160 }}>
          <div className="text-xs font-medium text-secondary">Filas válidas</div>
          <div className="text-2xl font-bold">{filasValidas}</div>
        </div>
        <div style={{ minWidth: 160 }}>
          <div className="text-xs font-medium text-secondary">Con errores</div>
          <div className="text-2xl font-bold">{hasErrors ? errores.length : 0}</div>
        </div>
      </div>

      {hasErrors ? (
        <>
          <p className="text-sm" style={{ marginBottom: 8 }}>
            Se detectaron las siguientes incidencias. Las filas válidas ya fueron
            importadas; corrige las filas con error y vuelve a subir el archivo.
          </p>
          <div className="table-container" style={{ maxHeight: 260, overflowY: 'auto' }}>
            <table className="table">
              <thead>
                <tr>
                  <th style={{ width: 80 }}>Fila</th>
                  <th style={{ width: 160 }}>Campo</th>
                  <th>Mensaje</th>
                </tr>
              </thead>
              <tbody>
                {errores.map((err, idx) => (
                  <tr key={`${err.fila}-${err.campo}-${idx}`}>
                    <td>{err.fila}</td>
                    <td>{err.campo}</td>
                    <td>{err.mensaje}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      ) : (
        <div className="alert alert--success">
          <div className="alert__content">
            <div className="alert__title">Importación completada</div>
            <div className="alert__message">
              Todas las filas procesadas fueron registradas correctamente.
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function formatDate(value) {
  if (!value) return '—';
  try {
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return value;
    return d.toLocaleString('es-EC', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return String(value);
  }
}
