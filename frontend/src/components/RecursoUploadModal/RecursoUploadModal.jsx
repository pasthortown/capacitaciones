import { useEffect, useState } from 'react';
import Modal from '../Modal/Modal.jsx';
import TextField from '../Forms/TextField.jsx';
import FileDropzone from '../FileDropzone/FileDropzone.jsx';
import { useToast } from '../Toast/useToast.js';
import {
  uploadRecurso,
  isBlockedExtension,
  MAX_SIZE_BYTES,
} from '../../services/recursos.js';
import styles from './RecursoUploadModal.module.css';

/**
 * Modal para subir un nuevo recurso al repositorio.
 *
 * Props:
 *  - isOpen: boolean
 *  - onClose: () => void
 *  - onSaved: () => void   (callback tras subida exitosa)
 *
 * Validación cliente antes de enviar:
 *  - Archivo requerido
 *  - Tamaño ≤ 100 MB
 *  - Extensión fuera de la lista de bloqueados
 *  - Descripción requerida (max 2000)
 *
 * Mapea los errores del backend (`EXTENSION_PROHIBIDA`, `ARCHIVO_DEMASIADO_GRANDE`,
 * `ARCHIVO_VACIO`) a mensajes legibles.
 */
const MAX_DESCRIPCION = 2000;

export default function RecursoUploadModal({ isOpen, onClose, onSaved }) {
  const toast = useToast();

  const [archivo, setArchivo] = useState(null);
  const [nombre, setNombre] = useState('');
  const [descripcion, setDescripcion] = useState('');
  const [errors, setErrors] = useState({});
  const [submitting, setSubmitting] = useState(false);

  // Reset al abrir/cerrar
  useEffect(() => {
    if (isOpen) {
      setArchivo(null);
      setNombre('');
      setDescripcion('');
      setErrors({});
      setSubmitting(false);
    }
  }, [isOpen]);

  const handleFileChange = (file) => {
    setArchivo(file);
    if (file && errors.archivo) {
      setErrors((prev) => ({ ...prev, archivo: undefined }));
    }
  };

  const validate = () => {
    const next = {};
    if (!archivo) {
      next.archivo = 'Selecciona un archivo.';
    } else {
      if (archivo.size > MAX_SIZE_BYTES) {
        next.archivo = 'El archivo excede el máximo permitido (100 MB).';
      } else if (isBlockedExtension(archivo.name)) {
        next.archivo =
          'Los ejecutables y scripts no están permitidos por política de seguridad.';
      } else if (archivo.size === 0) {
        next.archivo = 'El archivo está vacío.';
      }
    }
    if (!descripcion.trim()) {
      next.descripcion = 'La descripción es obligatoria.';
    } else if (descripcion.length > MAX_DESCRIPCION) {
      next.descripcion = `Máximo ${MAX_DESCRIPCION} caracteres.`;
    }
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const mapBackendError = (err) => {
    const status = err?.status;
    const code = err?.body?.error;
    if (code === 'EXTENSION_PROHIBIDA') {
      return 'Ese tipo de archivo no está permitido.';
    }
    if (code === 'ARCHIVO_DEMASIADO_GRANDE') {
      return 'El archivo excede 100 MB.';
    }
    if (code === 'ARCHIVO_VACIO') {
      return 'El archivo está vacío.';
    }
    if (status === 413) return 'El archivo excede 100 MB.';
    return err?.message || 'No se pudo subir el recurso.';
  };

  const handleSubmit = async (event) => {
    event?.preventDefault?.();
    if (submitting) return;
    if (!validate()) return;

    setSubmitting(true);
    try {
      const nombreTrim = nombre.trim();
      await uploadRecurso({
        archivo,
        nombre: nombreTrim || undefined,
        descripcion: descripcion.trim(),
      });
      toast.success('Recurso subido.');
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
      title="Subir recurso"
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
            {submitting ? 'Subiendo...' : 'Subir recurso'}
          </button>
        </>
      }
    >
      <form onSubmit={handleSubmit} noValidate>
        <FileDropzone
          value={archivo}
          onChange={handleFileChange}
          title="Sube tu archivo aquí"
          hint="Arrastra y suelta o haz clic para buscar en tu equipo. Máximo 100 MB. No se permiten ejecutables ni scripts."
          hasError={Boolean(errors.archivo)}
          disabled={submitting}
          ariaLabel="Zona de carga del recurso"
        />

        {errors.archivo && <div className={styles.errorMsg}>{errors.archivo}</div>}

        <div className={styles.field}>
          <TextField
            label="Nombre (opcional)"
            name="nombre"
            value={nombre}
            onChange={setNombre}
            maxLength={255}
            helper="Si lo dejas vacío, se usará el nombre original del archivo."
          />
        </div>

        <div className={styles.field}>
          <label
            htmlFor="recurso-descripcion"
            className="form-label form-label--required"
          >
            Descripción
          </label>
          <textarea
            id="recurso-descripcion"
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
