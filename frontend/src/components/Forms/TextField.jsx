import { useId } from 'react';

/**
 * Campo de texto reutilizable. Envuelve label + input + helper/error.
 * Usa las clases `.form-group`, `.form-label`, `.form-input` del design system.
 *
 * @param {object}   props
 * @param {string}   props.label
 * @param {string}   props.value
 * @param {Function} props.onChange    - recibe el nuevo valor (string), no el event.
 * @param {string}   [props.name]
 * @param {string}   [props.placeholder]
 * @param {boolean}  [props.required]
 * @param {boolean}  [props.disabled]
 * @param {string}   [props.error]     - mensaje de error inline.
 * @param {string}   [props.helper]    - texto auxiliar cuando no hay error.
 * @param {number}   [props.maxLength]
 * @param {string}   [props.type='text']
 * @param {string}   [props.autoComplete]
 */
export default function TextField({
  label,
  value,
  onChange,
  name,
  placeholder,
  required = false,
  disabled = false,
  error,
  helper,
  maxLength,
  type = 'text',
  autoComplete = 'off',
}) {
  const autoId = useId();
  const inputId = name ? `field-${name}` : `field-${autoId}`;
  const labelClass = `form-label${required ? ' form-label--required' : ''}`;
  const inputClass = `form-input${error ? ' form-input--error' : ''}`;

  return (
    <div className="form-group" style={{ position: 'static' }}>
      <label htmlFor={inputId} className={labelClass} style={{ position: 'static' }}>
        {label}
      </label>
      <input
        id={inputId}
        name={name}
        type={type}
        className={inputClass}
        value={value ?? ''}
        placeholder={placeholder}
        disabled={disabled}
        required={required}
        maxLength={maxLength}
        autoComplete={autoComplete}
        onChange={(event) => onChange?.(event.target.value)}
        aria-invalid={Boolean(error)}
        aria-describedby={error || helper ? `${inputId}-desc` : undefined}
      />
      {error ? (
        <div id={`${inputId}-desc`} className="form-helper form-helper--error">
          {error}
        </div>
      ) : helper ? (
        <div id={`${inputId}-desc`} className="form-helper">
          {helper}
        </div>
      ) : null}
    </div>
  );
}
