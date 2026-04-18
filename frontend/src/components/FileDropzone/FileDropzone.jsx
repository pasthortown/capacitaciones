import { useRef, useState } from 'react';
import { UploadCloud, FileText, X } from 'lucide-react';
import { formatBytes } from '../../services/recursos.js';

/**
 * Dropzone reutilizable alineado al design system (clases `.dropzone`,
 * `.dropzone--active`, `.dropzone__icon`, `.dropzone__title`, `.dropzone__text`
 * definidas en `style/components.css` — ver `style/example.html`).
 *
 * Soporta click-para-seleccionar y drag-and-drop nativo. Cuando hay un archivo
 * seleccionado muestra una fila compacta con nombre/tamaño y acción para quitarlo.
 *
 * Props:
 *  - value: File | null
 *  - onChange: (File|null) => void
 *  - title?: string    texto principal dentro del dropzone vacío (override).
 *  - hint?:  string    texto secundario dentro del dropzone vacío (override).
 *  - accept?: string   atributo accept del input.
 *  - disabled?: boolean
 *  - hasError?: boolean  aplica un borde rojo vía color del design system.
 *  - ariaLabel?: string
 */
export default function FileDropzone({
  value = null,
  onChange,
  title = 'Sube tu archivo aquí',
  hint = 'Arrastra y suelta o haz clic para buscar en tu equipo',
  accept,
  disabled = false,
  hasError = false,
  ariaLabel = 'Zona de carga de archivo',
}) {
  const inputRef = useRef(null);
  const [dragActive, setDragActive] = useState(false);

  const openPicker = () => {
    if (disabled) return;
    inputRef.current?.click();
  };

  const handleInputChange = (event) => {
    const file = event.target.files?.[0] || null;
    onChange?.(file);
    // Permite re-seleccionar el mismo archivo.
    if (event.target) event.target.value = '';
  };

  const handleDragOver = (event) => {
    if (disabled) return;
    event.preventDefault();
    event.stopPropagation();
    if (!dragActive) setDragActive(true);
  };

  const handleDragLeave = (event) => {
    event.preventDefault();
    event.stopPropagation();
    setDragActive(false);
  };

  const handleDrop = (event) => {
    if (disabled) return;
    event.preventDefault();
    event.stopPropagation();
    setDragActive(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) onChange?.(file);
  };

  const handleKeyDown = (event) => {
    if (disabled) return;
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      openPicker();
    }
  };

  const clearFile = (event) => {
    event?.stopPropagation?.();
    onChange?.(null);
  };

  const baseClasses = ['dropzone'];
  if (dragActive) baseClasses.push('dropzone--active');
  if (hasError) baseClasses.push('dropzone--error');

  return (
    <>
      <input
        ref={inputRef}
        type="file"
        accept={accept}
        style={{ display: 'none' }}
        onChange={handleInputChange}
        disabled={disabled}
      />

      {value ? (
        <div
          className={baseClasses.join(' ')}
          onClick={openPicker}
          onKeyDown={handleKeyDown}
          onDragOver={handleDragOver}
          onDragEnter={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          role="button"
          tabIndex={disabled ? -1 : 0}
          aria-label={ariaLabel}
          aria-disabled={disabled || undefined}
          style={{
            opacity: disabled ? 0.6 : 1,
            cursor: disabled ? 'not-allowed' : 'pointer',
            gap: 'var(--spacing-2)',
            padding: 'var(--spacing-4)',
          }}
        >
          <FileText className="dropzone__icon" />
          <div className="dropzone__title" style={{ wordBreak: 'break-all' }}>
            {value.name}
          </div>
          <div className="dropzone__text">
            {formatBytes(value.size)} · Haz clic o arrastra otro archivo para reemplazarlo
          </div>
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={clearFile}
            disabled={disabled}
            style={{ marginTop: 'var(--spacing-2)' }}
          >
            <X width={14} height={14} />
            <span>Quitar archivo</span>
          </button>
        </div>
      ) : (
        <div
          className={baseClasses.join(' ')}
          onClick={openPicker}
          onKeyDown={handleKeyDown}
          onDragOver={handleDragOver}
          onDragEnter={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          role="button"
          tabIndex={disabled ? -1 : 0}
          aria-label={ariaLabel}
          aria-disabled={disabled || undefined}
          style={{
            opacity: disabled ? 0.6 : 1,
            cursor: disabled ? 'not-allowed' : 'pointer',
          }}
        >
          <UploadCloud className="dropzone__icon" />
          <div className="dropzone__title">{title}</div>
          <div className="dropzone__text">{hint}</div>
        </div>
      )}
    </>
  );
}
