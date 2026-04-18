/**
 * Wrappers finos sobre SweetAlert2 alineados al design system del proyecto
 * (color primario, botones en español, orden `reverseButtons` consistente).
 *
 * Se centraliza aquí para evitar que cada llamada repita la misma config.
 */
import Swal from 'sweetalert2';
import 'sweetalert2/dist/sweetalert2.min.css';

// Color primario del design system (`--color-primary` en style/variables.css).
const PRIMARY = '#E31837';
const DANGER = '#dc2626';
const NEUTRAL = '#6b7280';

const base = Swal.mixin({
  confirmButtonColor: PRIMARY,
  cancelButtonColor: NEUTRAL,
  reverseButtons: true,
  heightAuto: false,
});

/**
 * Diálogo de confirmación con botón primario + cancelar.
 *
 * @param {object} opts
 * @param {string} opts.title
 * @param {string} [opts.text]
 * @param {string} [opts.html]                contenido HTML si se necesita mayor libertad.
 * @param {'warning'|'question'|'info'|'success'|'error'} [opts.icon='warning']
 * @param {string} [opts.confirmText='Confirmar']
 * @param {string} [opts.cancelText='Cancelar']
 * @param {boolean} [opts.danger=false]       pinta el confirm en rojo (eliminar/destructivo).
 * @returns {Promise<boolean>}                true si el usuario confirmó.
 */
export async function confirm({
  title,
  text,
  html,
  icon = 'warning',
  confirmText = 'Confirmar',
  cancelText = 'Cancelar',
  danger = false,
} = {}) {
  const result = await base.fire({
    title,
    text,
    html,
    icon,
    showCancelButton: true,
    confirmButtonText: confirmText,
    cancelButtonText: cancelText,
    confirmButtonColor: danger ? DANGER : PRIMARY,
    focusCancel: danger,
  });
  return Boolean(result.isConfirmed);
}

/**
 * Muestra un valor copiable (ej. enlace público) con input readonly y botón de copiar.
 * Útil cuando `navigator.clipboard` falla y no queremos usar `window.prompt`.
 *
 * @param {object} opts
 * @param {string} opts.title
 * @param {string} [opts.text]
 * @param {string} opts.value                 texto a mostrar/copiar.
 */
export async function showCopyableValue({ title, text, value }) {
  const safeValue = String(value ?? '');
  // Escapamos comillas dobles para el atributo value del input.
  const attr = safeValue.replace(/"/g, '&quot;');

  await base.fire({
    title,
    text,
    html: `
      <div style="display:flex;gap:8px;margin-top:8px;">
        <input
          id="swal-copy-input"
          type="text"
          readonly
          value="${attr}"
          style="flex:1;padding:8px 10px;border:1px solid #d1d5db;border-radius:6px;font-family:monospace;font-size:13px;"
        />
        <button
          type="button"
          id="swal-copy-btn"
          class="swal2-styled"
          style="background:${PRIMARY};color:#fff;"
        >Copiar</button>
      </div>
    `,
    showConfirmButton: true,
    confirmButtonText: 'Cerrar',
    showCancelButton: false,
    didOpen: () => {
      const input = document.getElementById('swal-copy-input');
      const btn = document.getElementById('swal-copy-btn');
      if (input) {
        input.focus();
        input.select();
      }
      if (btn && input) {
        btn.addEventListener('click', async () => {
          try {
            if (navigator.clipboard?.writeText) {
              await navigator.clipboard.writeText(input.value);
            } else {
              input.select();
              document.execCommand('copy');
            }
            btn.textContent = 'Copiado';
          } catch {
            input.select();
          }
        });
      }
    },
  });
}

export default { confirm, showCopyableValue };
