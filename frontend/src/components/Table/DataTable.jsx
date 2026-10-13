import { useMemo, useState } from 'react';
import { ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-react';
import styles from './DataTable.module.css';

/**
 * Tabla genérica mínima.
 *
 * Props:
 *   - columns: Array<{
 *       key:      string,
 *       header:   string | ReactNode,
 *       accessor?: (row) => ReactNode, // fallback: row[key]
 *       width?:   string,              // ej. "120px" o "15%"
 *       align?:   'left'|'right'|'center',
 *       sortable?: boolean,            // el encabezado ordena asc/desc al hacer clic
 *       sortValue?: (row) => string | number | boolean | null, // fallback: row[key]
 *     }>
 *   - rows:       Array<any>
 *   - rowKey:     (row) => string | number   // default: row.id
 *   - actions?:   (row) => ReactNode         // celda adicional a la derecha
 *   - emptyMessage?: string                  // vacío default: "Sin registros"
 *   - loading?:   boolean                    // muestra "Cargando..."
 *   - defaultSort?: { key: string, direction: 'asc'|'desc' } // orden inicial (opcional)
 *
 * El orden es en el cliente, sobre las filas recibidas. Los valores vacíos van siempre al final.
 * Se reordena al hacer clic en un encabezado o al cambiar las filas (altas, bajas, filtros); editar
 * un valor no mueve la fila.
 */

const collator = new Intl.Collator('es', { sensitivity: 'base', numeric: true });

function isEmpty(value) {
  return value === null || value === undefined || value === '';
}

function compareValues(a, b) {
  if (typeof a === 'number' && typeof b === 'number') return a - b;
  if (typeof a === 'boolean' && typeof b === 'boolean') return Number(a) - Number(b);
  return collator.compare(String(a), String(b));
}

export default function DataTable({
  columns,
  rows,
  rowKey = (row) => row?.id,
  actions,
  emptyMessage = 'Sin registros',
  loading = false,
  defaultSort = null,
}) {
  const hasActions = typeof actions === 'function';
  const totalCols = columns.length + (hasActions ? 1 : 0);
  const [sort, setSort] = useState(defaultSort);

  // El orden (lista de claves) se recalcula solo al cambiar el orden pedido o el conjunto de
  // filas, no cuando cambia un valor: así, al editar una celda (asistencia, calificación…) con
  // la tabla ordenada por esa columna, la fila no salta de lugar mientras se edita.
  const keySignature = Array.isArray(rows) ? rows.map(rowKey).join('\u0000') : '';
  const order = useMemo(() => {
    const col = sort && columns.find((c) => c.key === sort.key && c.sortable);
    if (!col || !Array.isArray(rows)) return null;
    const getValue = col.sortValue || ((row) => row?.[col.key]);
    const factor = sort.direction === 'desc' ? -1 : 1;
    return [...rows]
      .sort((ra, rb) => {
        const a = getValue(ra);
        const b = getValue(rb);
        if (isEmpty(a) && isEmpty(b)) return 0;
        if (isEmpty(a)) return 1;
        if (isEmpty(b)) return -1;
        return compareValues(a, b) * factor;
      })
      .map(rowKey);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sort, keySignature]);

  const sortedRows = useMemo(() => {
    if (!order || !Array.isArray(rows)) return rows;
    const byKey = new Map(rows.map((row) => [rowKey(row), row]));
    return order.map((k) => byKey.get(k)).filter(Boolean);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [order, rows]);

  function toggleSort(key) {
    setSort((prev) =>
      prev?.key === key
        ? { key, direction: prev.direction === 'asc' ? 'desc' : 'asc' }
        : { key, direction: 'asc' },
    );
  }

  function renderHeader(col) {
    if (!col.sortable) return col.header;
    const active = sort?.key === col.key;
    const Icon = !active ? ChevronsUpDown : sort.direction === 'asc' ? ArrowUp : ArrowDown;
    const siguiente = active && sort.direction === 'asc' ? 'descendente' : 'ascendente';
    return (
      <button
        type="button"
        className={styles.sortButton}
        onClick={() => toggleSort(col.key)}
        title={`Ordenar de forma ${siguiente}`}
      >
        <span>{col.header}</span>
        <Icon
          width={12}
          height={12}
          aria-hidden="true"
          className={active ? styles.sortIconActive : styles.sortIcon}
        />
      </button>
    );
  }

  function ariaSort(col) {
    if (!col.sortable) return undefined;
    if (sort?.key !== col.key) return 'none';
    return sort.direction === 'asc' ? 'ascending' : 'descending';
  }

  return (
    <div className="table-container">
      <table className="table">
        <thead>
          <tr>
            {columns.map((col) => (
              <th
                key={col.key}
                aria-sort={ariaSort(col)}
                style={{
                  width: col.width,
                  textAlign: col.align || 'left',
                }}
              >
                {renderHeader(col)}
              </th>
            ))}
            {hasActions && (
              <th style={{ textAlign: 'right', width: '160px' }}>Acciones</th>
            )}
          </tr>
        </thead>
        <tbody>
          {loading ? (
            <tr>
              <td colSpan={totalCols} style={{ textAlign: 'center', padding: 24 }}>
                Cargando...
              </td>
            </tr>
          ) : !sortedRows || sortedRows.length === 0 ? (
            <tr>
              <td colSpan={totalCols} style={{ textAlign: 'center', padding: 24 }}>
                {emptyMessage}
              </td>
            </tr>
          ) : (
            sortedRows.map((row) => (
              <tr key={rowKey(row)}>
                {columns.map((col) => (
                  <td
                    key={col.key}
                    style={{ textAlign: col.align || 'left' }}
                  >
                    {col.accessor ? col.accessor(row) : row?.[col.key]}
                  </td>
                ))}
                {hasActions && (
                  <td style={{ textAlign: 'right' }}>
                    <div
                      className="table__actions"
                      style={{ justifyContent: 'flex-end' }}
                    >
                      {actions(row)}
                    </div>
                  </td>
                )}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
