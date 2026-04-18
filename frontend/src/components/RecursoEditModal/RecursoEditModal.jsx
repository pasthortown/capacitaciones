import { useEffect, useState } from 'react';
import Modal from '../Modal/Modal.jsx';
import TextField from '../Forms/TextField.jsx';
import FileDropzone from '../FileDropzone/FileDropzone.jsx';
import { useToast } from '../Toast/useToast.js';
import {
  updateRecurso,
  isBlockedExtension,
  MAX_SIZE_BYTES,
  formatBytes,
} from '../../services/recursos.js';
import styles from './RecursoEditModal.module.css';

/**
 * Modal para editar un recurso. Permite:
 *   - cambiar nombre y descripción (siempre);
 *   - opcionalmente reemplazar el archivo físico (dropzone) — si no se
 *     selecciona archivo nuevo, el binario no se toca.
 *
 * Props:
 *  - isOpen: boolean
 *  - recurso: { id, nombreOriginal, descripcion, extension?, tamanoBytes? }
 *    (puede ser null en el primer render).
 *  - onClose: () => void
 *  - onSaved: () => void
 */
const MAX_DESCRIPCION = 2000;

export default function RecursoEditModal({ isOpen, recurso, onClose, onSaved }) {
  const toast = useToast();

  const [nombre, setNombre] = useState('');
  const [descripcion, setDescripcion] = useState('');
  const [archivo, setArchivo] = useState(null);
  const [errors, setErrors] = useState({});
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (isOpen && recurso) {
      setNombre(recurso.nombreOriginal || '');
      setDescripcion(recurso.descripcion || '');
      setArchivo(null);
      setErrors({});
      setSubmitting(false);
    }
  }, [isOpen, recurso]);

  const handleFileChange = (file) => {
    setArchivo(file);
    if (file && errors.archivo) {
      setErrors((prev) => ({ ...prev, archivo: undefined }));
    }
  };

  const validate = () => {
    const next = {};
    if (!nombre.trim()) next.nombre = 'El nombre es obligatorio.';
    else if (nombre.length > 255) next.nombre = 'Máximo 255 caracteres.';
    if (!descripcion.trim()) next.descripcion = 'La descripción es obligatoria.';
    else if (descripcion.length > MAX_DESCRIPCION) {
      next.descripcion = `Máximo ${MAX_DESCRIPCION} caracteres.`;
    }
    if (archivo) {
      if (archivo.size === 0) next.archivo = 'El archivo está vacío.';
      else if (archivo.size > MAX_SIZE_BYTES) next.archivo = 'El archivo excede el máximo permitido (100 MB).';
      else if (isBlockedExtension(archivo.name)) next.archivo = 'Los ejecutables y scripts no están permitidos.';
    }
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const mapBackendError = (err) => {
    const status = err?.status;
    const code = err?.body?.error;
    if (code === 'EXTENSION_PROHIBIDA') return 'Ese tipo de archivo no está permitido.';
    if (code === 'ARCHIVO_DEMASIADO_GRANDE' || status === 413) return 'El archivo excede 100 MB.';
    if (code === 'ARCHIVO_VACIO') return 'El archivo está vacío.';
    return err?.message || 'No se pudo actualizar el recurso.';
  };

  const handleSubmit = async (event) => {
    event?.preventDefault?.();
    if (submitting) return;
    if (!recurso?.id) return;
    if (!validate()) return;

    setSubmitting(true);
    try {
      await updateRecurso(recurso.id, {
        nombreOriginal: nombre.trim(),
        descripcion: descripcion.trim(),
        archivo: archivo || undefined,
      });
      toast.success(archivo ? 'Recurso actualizado (archivo reemplazado).' : 'Recurso actualizado.');
      onSaved?.();
      onClose?.();
    } catch (err) {
      toast.error(mapBackendError(err));
    } finally {
      setSubmitting(false);
    }
  };

  const handleClose = () => {
    if (submitting) return;
    onClose?.();
  };

  const descLen = descripcion.length;
  const descOverLimit = descLen > MAX_DESCRIPCION;

  return (
    <Modal
      isOpen={isOpen}
      onClose={handleClose}
      title="Editar recurso"
      className={styles.modalWide}
      footer={
        <>
          <button
            type="button"
            className="btn btn--secondary"
            onClick={handleClose}
            disabled={submitting}
          >
            Cancelar
          </button>
          <button
            type="button"
            className="btn btn--primary"
            onClick={handleSubmit}
            disabled={submitting}
          >
            {submitting ? 'Guardando...' : 'Guardar'}
          </button>
        </>
      }
    >
      <form onSubmit={handleSubmit} noValidate>
        <div className={styles.field}>
          <TextField
            label="Nombre"
            name="nombre"
            value={nombre}
            required
            maxLength={255}
            onChange={(v) => {
              setNombre(v);
              if (errors.nombre) setErrors((e) => ({ ...e, nombre: undefined }));
            }}
            error={errors.nombre}
          />
        </div>

        <div className={styles.field}>
          <label className="form-label">Archivo (opcional)</label>
          {recurso && (
            <div className={styles.currentFileHint}>
              Archivo actual:{' '}
              <strong>{recurso.nombreOriginal}</strong>
              {recurso.tamanoBytes != null && (
                <> · {formatBytes(recurso.tamanoBytes)}</>
              )}
              . Sube uno nuevo para reemplazarlo, o deja vacío para conservarlo.
            </div>
          )}
          <FileDropzone
            value={archivo}
            onChange={handleFileChange}
            title="Reemplazar archivo"
            hint="Arrastra y suelta o haz clic para elegir uno nuevo. Máximo 100 MB. No se permiten ejecutables ni scripts."
            hasError={Boolean(errors.archivo)}
            disabled={submitting}
            ariaLabel="Reemplazo de archivo del recurso"
          />
          {errors.archivo && <div className={styles.errorMsg}>{errors.archivo}</div>}
        </div>

        <div className={styles.field}>
          <label
            htmlFor="recurso-edit-descripcion"
            className="form-label form-label--required"
          >
            Descripción
          </label>
          <textarea
            id="recurso-edit-descripcion"
            className={[
              styles.textarea,
              errors.descripcion ? styles.textareaError : '',
            ]
              .filter(Boolean)
              .join(' ')}
            value={descripcion}
            onChange={(e) => {
              setDescripcion(e.target.value);
              if (errors.descripcion) {
                setErrors((prev) => ({ ...prev, descripcion: undefined }));
              }
            }}
            maxLength={MAX_DESCRIPCION}
            aria-invalid={Boolean(errors.descripcion)}
          />
          <div
            className={[
              styles.counter,
              descOverLimit ? styles.counterError : '',
            ]
              .filter(Boolean)
              .join(' ')}
          >
            {descLen} / {MAX_DESCRIPCION}
          </div>
          {errors.descripcion && (
            <div className={styles.errorMsg}>{errors.descripcion}</div>
          )}
        </div>

        <button type="submit" style={{ display: 'none' }} aria-hidden="true" />
      </form>
    </Modal>
  );
}
