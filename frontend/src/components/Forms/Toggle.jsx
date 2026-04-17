import { useId } from 'react';
import styles from './Toggle.module.css';

/**
 * Switch on/off accesible.
 * - Controlado: recibe `checked` y `onChange(nextBool)`.
 * - Puede usarse inline (sin label) pasando `label=""` y proveyendo un aria-label externo.
 *
 * @param {object}   props
 * @param {string}   [props.label]
 * @param {boolean}  props.checked
 * @param {Function} props.onChange
 * @param {string}   [props.name]
 * @param {boolean}  [props.disabled]
 * @param {string}   [props.ariaLabel]
 */
export default function Toggle({
  label,
  checked,
  onChange,
  name,
  disabled = false,
  ariaLabel,
}) {
  const autoId = useId();
  const inputId = name ? `toggle-${name}` : `toggle-${autoId}`;

  return (
    <label htmlFor={inputId} className={styles.wrapper}>
      <input
        id={inputId}
        name={name}
        type="checkbox"
        className={styles.input}
        checked={Boolean(checked)}
        disabled={disabled}
        aria-label={ariaLabel || label}
        onChange={(event) => onChange?.(event.target.checked)}
      />
      <span
        className={`${styles.track} ${checked ? styles.trackOn : ''} ${
          disabled ? styles.trackDisabled : ''
        }`}
        aria-hidden="true"
      >
        <span className={`${styles.thumb} ${checked ? styles.thumbOn : ''}`} />
      </span>
      {label && <span className={styles.label}>{label}</span>}
    </label>
  );
}
