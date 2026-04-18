import styles from './AttendanceToggle.module.css';

/**
 * Split button tipo radio con dos opciones: "Presente" y "Ausente".
 *
 * Estados visuales:
 *   - value === null           → ambos botones en gris neutral.
 *   - value === 'Presente'     → botón Presente con fondo verde, Ausente en gris.
 *   - value === 'Ausente'      → botón Ausente con fondo rojo, Presente en gris.
 *
 * Reglas:
 *   - Click en el botón NO seleccionado: llama `onChange(nuevoValor)`.
 *   - Click en el botón YA seleccionado: no-op (no se puede desmarcar
 *     ambos una vez que alguno fue seleccionado — decisión 12 en instrucciones).
 *
 * Props:
 *   - value: 'Presente' | 'Ausente' | null
 *   - onChange: (newValue: 'Presente' | 'Ausente') => void
 *   - disabled?: boolean
 *   - size?: 'sm' | 'md'   (default: 'md')
 */
export default function AttendanceToggle({
  value = null,
  onChange,
  disabled = false,
  size = 'md',
}) {
  const isPresent = value === 'Presente';
  const isAbsent = value === 'Ausente';

  const handleClick = (newValue) => {
    if (disabled) return;
    if (value === newValue) return; // no-op: no se desmarca
    if (typeof onChange === 'function') {
      onChange(newValue);
    }
  };

  const sizeClass = size === 'sm' ? styles.sm : styles.md;

  // Clases de cada botón: base + variante de estado.
  const presentClass = [
    styles.btn,
    styles.btnLeft,
    isPresent ? styles.btnPresentActive : styles.btnNeutral,
  ].join(' ');

  const absentClass = [
    styles.btn,
    styles.btnRight,
    isAbsent ? styles.btnAbsentActive : styles.btnNeutral,
  ].join(' ');

  return (
    <div
      className={`${styles.group} ${sizeClass}`}
      role="group"
      aria-label="Asistencia"
    >
      <button
        type="button"
        className={presentClass}
        onClick={() => handleClick('Presente')}
        disabled={disabled}
        aria-pressed={isPresent}
        aria-label="Marcar como presente"
        title="Presente"
      >
        Presente
      </button>
      <button
        type="button"
        className={absentClass}
        onClick={() => handleClick('Ausente')}
        disabled={disabled}
        aria-pressed={isAbsent}
        aria-label="Marcar como ausente"
        title="Ausente"
      >
        Ausente
      </button>
    </div>
  );
}
