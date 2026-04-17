import { useMemo } from 'react';
import styles from './DateTimePicker.module.css';

/**
 * Selector de fecha + hora + minutos separado en tres campos.
 *
 * El valor es un string compatible con `<input type="datetime-local">`:
 *   `YYYY-MM-DDTHH:mm`
 * (sin zona horaria — el caller decide cómo convertir a ISO al persistir).
 *
 * Diseño: el calendario nativo del navegador sirve para elegir día; la hora
 * y los minutos van en selects amigables, sin el "stepper" típico de los
 * datetime-local nativos que el usuario percibía como incómodo.
 *
 * Props:
 *   - value: string | null | ''
 *   - onChange(nextValue: string): llamado cuando los 3 campos son válidos
 *     en conjunto. Si la fecha queda vacía, emite ''.
 *   - id, required, disabled
 *   - minuteStep: intervalo de minutos (default 30). Valores válidos: divisores de 60.
 */
export default function DateTimePicker({
  value,
  onChange,
  id,
  required = false,
  disabled = false,
  minuteStep = 30,
  hasError = false,
}) {
  const parts = useMemo(() => parseValue(value), [value]);

  const minuteOptions = useMemo(() => {
    const step = Math.max(1, Math.min(60, Math.floor(minuteStep) || 30));
    const list = [];
    for (let m = 0; m < 60; m += step) list.push(m);
    return list;
  }, [minuteStep]);

  const hourOptions = useMemo(() => Array.from({ length: 24 }, (_, i) => i), []);

  const emit = (next) => {
    if (!next.date) {
      onChange?.('');
      return;
    }
    const hh = pad2(next.hour ?? 0);
    const mm = pad2(next.minute ?? 0);
    onChange?.(`${next.date}T${hh}:${mm}`);
  };

  const handleDate = (e) => emit({ ...parts, date: e.target.value });
  const handleHour = (e) => emit({ ...parts, hour: Number(e.target.value) });
  const handleMinute = (e) => emit({ ...parts, minute: Number(e.target.value) });

  const wrapClass = hasError ? `${styles.wrap} ${styles.hasError}` : styles.wrap;

  return (
    <div className={wrapClass}>
      <div className={styles.field}>
        <input
          id={id}
          type="date"
          className={styles.date}
          value={parts.date}
          onChange={handleDate}
          required={required}
          disabled={disabled}
          aria-label="Fecha"
        />
        <span className={styles.caption}>Fecha</span>
      </div>

      <div className={styles.field}>
        <select
          className={styles.select}
          value={parts.date ? String(parts.hour) : ''}
          onChange={handleHour}
          disabled={disabled || !parts.date}
          aria-label="Hora"
        >
          {!parts.date && <option value="">--</option>}
          {hourOptions.map((h) => (
            <option key={h} value={h}>
              {pad2(h)}
            </option>
          ))}
        </select>
        <span className={styles.caption}>Hora</span>
      </div>

      <div className={styles.field}>
        <select
          className={styles.select}
          value={parts.date ? String(parts.minute) : ''}
          onChange={handleMinute}
          disabled={disabled || !parts.date}
          aria-label="Minutos"
        >
          {!parts.date && <option value="">--</option>}
          {minuteOptions.map((m) => (
            <option key={m} value={m}>
              {pad2(m)}
            </option>
          ))}
        </select>
        <span className={styles.caption}>Minutos</span>
      </div>
    </div>
  );
}

function pad2(n) {
  return String(n).padStart(2, '0');
}

function parseValue(value) {
  if (!value || typeof value !== 'string') {
    return { date: '', hour: 0, minute: 0 };
  }
  const [datePart, timePart = ''] = value.split('T');
  const [hStr = '0', mStr = '0'] = timePart.split(':');
  const hour = clamp(parseInt(hStr, 10) || 0, 0, 23);
  const minute = clamp(parseInt(mStr, 10) || 0, 0, 59);
  return { date: datePart || '', hour, minute };
}

function clamp(n, lo, hi) {
  return Math.max(lo, Math.min(hi, n));
}
