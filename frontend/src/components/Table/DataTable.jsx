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
 *     }>
 *   - rows:       Array<any>
 *   - rowKey:     (row) => string | number   // default: row.id
 *   - actions?:   (row) => ReactNode         // celda adicional a la derecha
 *   - emptyMessage?: string                  // vacío default: "Sin registros"
 *   - loading?:   boolean                    // muestra "Cargando..."
 */
export default function DataTable({
  columns,
  rows,
  rowKey = (row) => row?.id,
  actions,
  emptyMessage = 'Sin registros',
  loading = false,
}) {
  const hasActions = typeof actions === 'function';
  const totalCols = columns.length + (hasActions ? 1 : 0);

  return (
    <div className="table-container">
      <table className="table">
        <thead>
          <tr>
            {columns.map((col) => (
              <th
                key={col.key}
                style={{
                  width: col.width,
                  textAlign: col.align || 'left',
                }}
              >
                {col.header}
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
          ) : !rows || rows.length === 0 ? (
            <tr>
              <td colSpan={totalCols} style={{ textAlign: 'center', padding: 24 }}>
                {emptyMessage}
              </td>
            </tr>
          ) : (
            rows.map((row) => (
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
