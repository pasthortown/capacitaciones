import { useEffect, useLayoutEffect, useRef } from 'react';
import { X } from 'lucide-react';

/**
 * Modal reusable del sistema.
 *
 * Reglas de cierre (ver instrucciones.md §7.3):
 *  - NO cierra al hacer click fuera del contenido (backdrop no dispara onClose).
 *  - SÍ cierra con:
 *      · Tecla ESC (listener global registrado sólo cuando `isOpen` es true).
 *      · Botón X en la esquina superior derecha.
 *      · Botones Cerrar/Cancelar que el padre incluya en `footer`
 *        invocando `onClose`.
 *      · Cuando el padre lo cierre tras un submit exitoso.
 *
 * Usa las clases `.modal-backdrop`, `.modal`, `.modal__header`,
 * `.modal__title`, `.modal__close`, `.modal__body`, `.modal__footer`
 * definidas en `style/components.css`.
 *
 * @param {object}   props
 * @param {boolean}  props.isOpen   - Controla visibilidad.
 * @param {Function} props.onClose  - Callback para cierre solicitado.
 * @param {string}   props.title    - Título mostrado en el header.
 * @param {React.ReactNode} props.children - Contenido del body.
 * @param {React.ReactNode} [props.footer] - Botones del footer (opcional).
 * @param {string}   [props.className] - Clase adicional para el contenedor .modal
 *   (útil para anchos mayores, ej. formularios a dos columnas).
 */
export default function Modal({ isOpen, onClose, title, children, footer, className }) {
  const dialogRef = useRef(null);
  const bodyRef = useRef(null);

  // El `.modal__body` es un flex item con scroll (`flex: 1 1 auto; min-height: 0; overflow-y: auto`).
  // Cuando el contenido cambia dinámicamente (p.ej. mostrar/ocultar una sección del formulario),
  // Chromium a veces conserva el layout anterior y deja el body colapsado con un hueco blanco debajo.
  // Forzamos un reflow del body en cada render para que recalcule su alto (y el del diálogo).
  useLayoutEffect(() => {
    const el = bodyRef.current;
    if (!el) return;
    el.style.overflowY = 'hidden';
    void el.offsetHeight; // lectura forzada → reflow síncrono (antes del paint)
    el.style.overflowY = '';
  });

  // ESC -> onClose. Sólo activo cuando isOpen.
  useEffect(() => {
    if (!isOpen) return undefined;

    const handleKeyDown = (event) => {
      if (event.key === 'Escape') {
        event.stopPropagation();
        onClose?.();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  // Enfocar el dialog al abrir para accesibilidad básica.
  useEffect(() => {
    if (isOpen && dialogRef.current) {
      dialogRef.current.focus();
    }
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <>
      {/* Backdrop: click NO cierra (intencional) */}
      <div className="modal-backdrop modal-backdrop--open" aria-hidden="true" />

      <div
        ref={dialogRef}
        className={['modal', 'modal--open', className].filter(Boolean).join(' ')}
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-title"
        tabIndex={-1}
      >
        <header className="modal__header">
          <h3 id="modal-title" className="modal__title">
            {title}
          </h3>
          <button
            type="button"
            className="modal__close"
            onClick={onClose}
            aria-label="Cerrar"
          >
            <X width={20} height={20} />
          </button>
        </header>

        <div className="modal__body" ref={bodyRef}>{children}</div>

        {footer && <footer className="modal__footer">{footer}</footer>}
      </div>
    </>
  );
}
