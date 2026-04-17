import styles from './Spinner.module.css';

/**
 * Spinner mínimo de carga. Sin dependencias externas, solo CSS.
 *
 * @param {object} props
 * @param {number}  [props.size=24]   - Diámetro en px.
 * @param {string}  [props.label]     - Texto accesible opcional.
 * @param {boolean} [props.fullscreen] - Si true, centra a pantalla completa.
 */
export default function Spinner({ size = 24, label = 'Cargando...', fullscreen = false }) {
  const spinner = (
    <span
      className={styles.spinner}
      role="status"
      aria-label={label}
      style={{ width: size, height: size, borderWidth: Math.max(2, Math.round(size / 10)) }}
    />
  );

  if (fullscreen) {
    return <div className={styles.fullscreen}>{spinner}</div>;
  }
  return spinner;
}
