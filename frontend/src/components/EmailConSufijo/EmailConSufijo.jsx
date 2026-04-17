import styles from './EmailConSufijo.module.css';

/**
 * Input de correo con sufijo fijo `@dos.com.ec`.
 *
 * El usuario sólo edita la parte local (antes del `@`); el dominio está
 * "pegado" a la derecha como un addon no editable. Esto respeta la regla
 * de negocio (§7.2.5): las cuentas corporativas usan dominio fijo.
 *
 * Props:
 *  - id          (string) id para el `<input>` y target del `<label htmlFor>`.
 *  - name        (string) atributo name.
 *  - value       (string) parte local actual (sin `@`).
 *  - onChange    (value: string) => void   emite solo la parte local.
 *  - placeholder (string)
 *  - disabled    (boolean)
 *  - required    (boolean)
 *  - ariaLabel   (string) alternativa si no hay `<label>` externo.
 */
export default function EmailConSufijo({
  id,
  name,
  value = '',
  onChange,
  placeholder = 'nombre.apellido',
  disabled = false,
  required = false,
  ariaLabel,
}) {
  return (
    <div
      className={`${styles.wrapper} ${disabled ? styles.wrapperDisabled : ''}`}
    >
      <input
        id={id}
        name={name}
        type="text"
        className={styles.input}
        value={value}
        onChange={(event) => onChange?.(event.target.value)}
        placeholder={placeholder}
        disabled={disabled}
        required={required}
        aria-label={ariaLabel}
        autoComplete="off"
        spellCheck={false}
      />
      <span className={styles.suffix} aria-hidden="true">
        @dos.com.ec
      </span>
    </div>
  );
}
