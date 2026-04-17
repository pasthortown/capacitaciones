import { useEffect, useMemo, useRef, useState } from 'react';
import { ChevronUp, ChevronDown, Trash2, Plus, AlertTriangle } from 'lucide-react';
import Modal from '../Modal/Modal.jsx';
import TextField from '../Forms/TextField.jsx';
import Spinner from '../Spinner/Spinner.jsx';
import { useToast } from '../Toast/useToast.js';
import catalogosService from '../../services/catalogos.js';
import responsablesService from '../../services/responsables.js';
import {
  createCapacitacion,
  updateCapacitacion,
  getCapacitacion,
} from '../../services/capacitaciones.js';
import styles from './CapacitacionFormModal.module.css';

/**
 * Modal de creación / edición de capacitación.
 *
 * Responsables:
 *  - Son un catálogo global (ver services/responsables.js). Este modal sólo los
 *    **selecciona** y los **ordena** (↑, ↓, eliminar). El payload ahora lleva
 *    `responsableIds: Guid[]` — el orden del array = `orden` final en el
 *    certificado. El admin crea/edita/borra responsables en `/responsables`.
 *
 * Props:
 *   - isOpen
 *   - mode: 'create' | 'edit'
 *   - initialValue: null | { id, ... } (en modo edit, `id` es suficiente —
 *     el modal hará GET /capacitaciones/{id} para obtener el detalle completo).
 *   - onClose()
 *   - onSaved(updatedOrCreated)
 */
export default function CapacitacionFormModal({
  isOpen,
  mode = 'create',
  initialValue = null,
  onClose,
  onSaved,
}) {
  const toast = useToast();

  // Form state --------------------------------------------------------------
  const [tema, setTema] = useState('');
  const [capacitador, setCapacitador] = useState('');
  const [cargoCapacitador, setCargoCapacitador] = useState('');
  const [empresaCapacitador, setEmpresaCapacitador] = useState('');
  const [modalidadId, setModalidadId] = useState('');
  const [tipoActividadId, setTipoActividadId] = useState('');
  const [tipoCertificacion, setTipoCertificacion] = useState('Participacion');
  const [fechaHoraInicio, setFechaHoraInicio] = useState(''); // "YYYY-MM-DDTHH:mm"
  const [duracionHoras, setDuracionHoras] = useState(1);
  const [duracionExtraMin, setDuracionExtraMin] = useState(0); // 0 | 30
  const [descripcion, setDescripcion] = useState('');
  const [responsableIds, setResponsableIds] = useState([]); // ordered array

  // Catálogos --------------------------------------------------------------
  const [modalidades, setModalidades] = useState([]);
  const [tiposActividad, setTiposActividad] = useState([]);
  const [catalogoResponsables, setCatalogoResponsables] = useState([]);
  const [loadingCatalogos, setLoadingCatalogos] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  // Sub-modal selector de responsable --------------------------------------
  const [pickerOpen, setPickerOpen] = useState(false);

  // Errores por campo ------------------------------------------------------
  const [errors, setErrors] = useState({});

  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  // Carga de catálogos cuando se abre el modal -----------------------------
  useEffect(() => {
    if (!isOpen) return;
    let cancelled = false;
    setLoadingCatalogos(true);
    Promise.all([
      catalogosService.list('modalidades', { includeInactive: false }),
      catalogosService.list('tipos-actividad', { includeInactive: false }),
      responsablesService.list(false),
    ])
      .then(([mods, tipos, resps]) => {
        if (cancelled) return;
        setModalidades(Array.isArray(mods) ? mods : []);
        setTiposActividad(Array.isArray(tipos) ? tipos : []);
        setCatalogoResponsables(Array.isArray(resps) ? resps : []);
      })
      .catch((err) => {
        if (cancelled) return;
        toast.error(err?.message || 'No se pudieron cargar los catálogos.');
      })
      .finally(() => {
        if (!cancelled && mountedRef.current) setLoadingCatalogos(false);
      });
    return () => {
      cancelled = true;
    };
  }, [isOpen, toast]);

  // Reset / carga del detalle al abrir -------------------------------------
  useEffect(() => {
    if (!isOpen) return;
    setErrors({});
    if (mode === 'create' || !initialValue?.id) {
      // Reset a valores por defecto
      setTema('');
      setCapacitador('');
      setCargoCapacitador('');
      setEmpresaCapacitador('');
      setModalidadId('');
      setTipoActividadId('');
      setTipoCertificacion('Participacion');
      setFechaHoraInicio('');
      setDuracionHoras(1);
      setDuracionExtraMin(0);
      setDescripcion('');
      setResponsableIds([]);
      return;
    }
    // Modo edit: carga detalle del backend
    let cancelled = false;
    setLoadingDetail(true);
    getCapacitacion(initialValue.id)
      .then((detail) => {
        if (cancelled) return;
        setTema(detail.tema || '');
        setCapacitador(detail.capacitador || '');
        setCargoCapacitador(detail.cargoCapacitador || '');
        setEmpresaCapacitador(detail.empresaCapacitador || '');
        setModalidadId(detail.modalidad?.id || '');
        setTipoActividadId(detail.tipoActividad?.id || '');
        setTipoCertificacion(detail.tipoCertificacion || 'Participacion');
        setFechaHoraInicio(isoToLocalInput(detail.fechaHoraInicio));
        const mins = Number(detail.duracionMinutos || 0);
        setDuracionHoras(Math.floor(mins / 60));
        setDuracionExtraMin(mins % 60 === 30 ? 30 : 0);
        setDescripcion(detail.descripcion || '');
        setResponsableIds(
          Array.isArray(detail.responsables)
            ? detail.responsables
                .slice()
                .sort((a, b) => (a.orden ?? 0) - (b.orden ?? 0))
                .map((r) => r.id)
                .filter(Boolean)
            : [],
        );
      })
      .catch((err) => {
        if (cancelled) return;
        toast.error(err?.message || 'No se pudo cargar la capacitación.');
      })
      .finally(() => {
        if (!cancelled && mountedRef.current) setLoadingDetail(false);
      });
    return () => {
      cancelled = true;
    };
  }, [isOpen, mode, initialValue, toast]);

  // Helpers ----------------------------------------------------------------
  const duracionMinutos = useMemo(() => {
    const horas = Math.max(0, Number(duracionHoras) || 0);
    const extra = duracionExtraMin === 30 ? 30 : 0;
    return horas * 60 + extra;
  }, [duracionHoras, duracionExtraMin]);

  const responsablesById = useMemo(() => {
    const m = new Map();
    (catalogoResponsables || []).forEach((r) => m.set(r.id, r));
    return m;
  }, [catalogoResponsables]);

  const availableToAdd = useMemo(() => {
    const selected = new Set(responsableIds);
    return (catalogoResponsables || []).filter((r) => !selected.has(r.id));
  }, [catalogoResponsables, responsableIds]);

  const addResponsable = (id) => {
    if (!id) return;
    setResponsableIds((prev) => (prev.includes(id) ? prev : [...prev, id]));
    setPickerOpen(false);
  };

  const removeResponsable = (id) => {
    setResponsableIds((prev) => prev.filter((x) => x !== id));
  };

  const moveResponsable = (id, direction) => {
    setResponsableIds((prev) => {
      const idx = prev.indexOf(id);
      if (idx < 0) return prev;
      const target = direction === 'up' ? idx - 1 : idx + 1;
      if (target < 0 || target >= prev.length) return prev;
      const next = prev.slice();
      const [moved] = next.splice(idx, 1);
      next.splice(target, 0, moved);
      return next;
    });
  };

  // Validación -------------------------------------------------------------
  const validate = () => {
    const next = {};
    if (!tema.trim()) next.tema = 'El tema es obligatorio.';
    else if (tema.length > 500) next.tema = 'Máximo 500 caracteres.';
    if (!capacitador.trim()) next.capacitador = 'El capacitador es obligatorio.';
    if (!modalidadId) next.modalidadId = 'Selecciona una modalidad.';
    if (!tipoActividadId) next.tipoActividadId = 'Selecciona un tipo de actividad.';
    if (!fechaHoraInicio) next.fechaHoraInicio = 'Ingresa fecha y hora.';
    if (duracionMinutos <= 0) {
      next.duracion = 'La duración debe ser mayor a 0.';
    } else if (duracionMinutos % 30 !== 0) {
      next.duracion = 'La duración debe ser múltiplo de 30 minutos.';
    }
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  // Submit -----------------------------------------------------------------
  const handleSubmit = async (event) => {
    event?.preventDefault?.();
    if (submitting) return;
    if (!validate()) return;
    const payload = {
      tema: tema.trim(),
      capacitador: capacitador.trim(),
      cargoCapacitador: cargoCapacitador.trim() || null,
      empresaCapacitador: empresaCapacitador.trim() || null,
      modalidadId,
      tipoActividadId,
      tipoCertificacion,
      fechaHoraInicio: localInputToIso(fechaHoraInicio),
      duracionMinutos,
      descripcion: descripcion.trim() || null,
      responsableIds: responsableIds.slice(),
    };
    setSubmitting(true);
    try {
      let result;
      if (mode === 'edit' && initialValue?.id) {
        result = await updateCapacitacion(initialValue.id, payload);
        toast.success('Capacitación actualizada.');
      } else {
        result = await createCapacitacion(payload);
        toast.success('Capacitación creada.');
      }
      onSaved?.(result);
      onClose?.();
    } catch (err) {
      toast.error(err?.message || 'No se pudo guardar la capacitación.');
    } finally {
      if (mountedRef.current) setSubmitting(false);
    }
  };

  const handleClose = () => {
    if (submitting) return;
    onClose?.();
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={handleClose}
      className={styles.wideModal}
      title={mode === 'edit' ? 'Editar capacitación' : 'Nueva capacitación'}
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
            disabled={submitting || loadingDetail}
          >
            {submitting ? 'Guardando...' : 'Guardar'}
          </button>
        </>
      }
    >
      {loadingDetail ? (
        <div style={{ padding: 24, textAlign: 'center' }}>
          <Spinner size={32} label="Cargando capacitación..." />
        </div>
      ) : (
        <form onSubmit={handleSubmit} noValidate>
          <div className={styles.grid2}>
            <div className={styles.fullSpan}>
              <TextField
                label="Tema"
                name="tema"
                value={tema}
                onChange={(v) => {
                  setTema(v);
                  if (errors.tema) setErrors((e) => ({ ...e, tema: undefined }));
                }}
                required
                maxLength={500}
                error={errors.tema}
              />
            </div>

            <TextField
              label="Capacitador"
              name="capacitador"
              value={capacitador}
              onChange={(v) => {
                setCapacitador(v);
                if (errors.capacitador) setErrors((e) => ({ ...e, capacitador: undefined }));
              }}
              required
              maxLength={255}
              error={errors.capacitador}
            />

            <TextField
              label="Cargo del capacitador"
              name="cargoCapacitador"
              value={cargoCapacitador}
              onChange={setCargoCapacitador}
              maxLength={255}
            />

            <TextField
              label="Empresa del capacitador"
              name="empresaCapacitador"
              value={empresaCapacitador}
              onChange={setEmpresaCapacitador}
              maxLength={255}
            />

            {/* Modalidad */}
            <div>
              <label className={styles.smallLabel} data-required="true" htmlFor="modalidad-select">
                Modalidad
              </label>
              <select
                id="modalidad-select"
                className={styles.select}
                value={modalidadId}
                onChange={(e) => {
                  setModalidadId(e.target.value);
                  if (errors.modalidadId) setErrors((er) => ({ ...er, modalidadId: undefined }));
                }}
                disabled={loadingCatalogos}
              >
                <option value="">Seleccionar...</option>
                {modalidades.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.nombre}
                  </option>
                ))}
              </select>
              {errors.modalidadId && <div className={styles.errorText}>{errors.modalidadId}</div>}
            </div>

            {/* Tipo de actividad */}
            <div>
              <label className={styles.smallLabel} data-required="true" htmlFor="tipo-actividad-select">
                Tipo de actividad
              </label>
              <select
                id="tipo-actividad-select"
                className={styles.select}
                value={tipoActividadId}
                onChange={(e) => {
                  setTipoActividadId(e.target.value);
                  if (errors.tipoActividadId)
                    setErrors((er) => ({ ...er, tipoActividadId: undefined }));
                }}
                disabled={loadingCatalogos}
              >
                <option value="">Seleccionar...</option>
                {tiposActividad.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.nombre}
                  </option>
                ))}
              </select>
              {errors.tipoActividadId && (
                <div className={styles.errorText}>{errors.tipoActividadId}</div>
              )}
            </div>

            {/* Tipo de certificación */}
            <div>
              <label className={styles.smallLabel} data-required="true">
                Tipo de certificación
              </label>
              <div className={styles.radioGroup}>
                <label className={styles.radioItem}>
                  <input
                    type="radio"
                    name="tipoCertificacion"
                    value="Participacion"
                    checked={tipoCertificacion === 'Participacion'}
                    onChange={() => setTipoCertificacion('Participacion')}
                  />
                  Participación
                </label>
                <label className={styles.radioItem}>
                  <input
                    type="radio"
                    name="tipoCertificacion"
                    value="Aprobacion"
                    checked={tipoCertificacion === 'Aprobacion'}
                    onChange={() => setTipoCertificacion('Aprobacion')}
                  />
                  Aprobación
                </label>
              </div>
            </div>

            {/* Fecha y hora de inicio */}
            <div>
              <label className={styles.smallLabel} data-required="true" htmlFor="fecha-hora-inicio">
                Fecha y hora de inicio
              </label>
              <input
                id="fecha-hora-inicio"
                type="datetime-local"
                className={styles.select}
                style={{ backgroundImage: 'none', paddingRight: 'var(--spacing-4)' }}
                value={fechaHoraInicio}
                onChange={(e) => {
                  setFechaHoraInicio(e.target.value);
                  if (errors.fechaHoraInicio)
                    setErrors((er) => ({ ...er, fechaHoraInicio: undefined }));
                }}
              />
              {errors.fechaHoraInicio && (
                <div className={styles.errorText}>{errors.fechaHoraInicio}</div>
              )}
            </div>

            {/* Duración */}
            <div>
              <label className={styles.smallLabel} data-required="true">
                Duración
              </label>
              <div className={styles.duracionGrid}>
                <div>
                  <input
                    type="number"
                    min="0"
                    step="1"
                    className={styles.select}
                    style={{ backgroundImage: 'none', paddingRight: 'var(--spacing-4)' }}
                    value={duracionHoras}
                    onChange={(e) => {
                      const raw = e.target.value;
                      const n = raw === '' ? 0 : Math.max(0, parseInt(raw, 10) || 0);
                      setDuracionHoras(n);
                      if (errors.duracion) setErrors((er) => ({ ...er, duracion: undefined }));
                    }}
                    aria-label="Horas"
                    placeholder="Horas"
                  />
                  <div className={styles.errorText} style={{ color: 'var(--color-text-secondary)' }}>
                    Horas
                  </div>
                </div>
                <div>
                  <select
                    className={styles.select}
                    value={String(duracionExtraMin)}
                    onChange={(e) => {
                      setDuracionExtraMin(Number(e.target.value));
                      if (errors.duracion) setErrors((er) => ({ ...er, duracion: undefined }));
                    }}
                    aria-label="Minutos"
                  >
                    <option value="0">00 min</option>
                    <option value="30">30 min</option>
                  </select>
                  <div className={styles.errorText} style={{ color: 'var(--color-text-secondary)' }}>
                    Minutos
                  </div>
                </div>
              </div>
              {errors.duracion && <div className={styles.errorText}>{errors.duracion}</div>}
            </div>

            {/* Descripción */}
            <div className={styles.fullSpan}>
              <label className={styles.smallLabel} htmlFor="descripcion">
                Descripción
              </label>
              <textarea
                id="descripcion"
                className={styles.textarea}
                value={descripcion}
                onChange={(e) => setDescripcion(e.target.value)}
                placeholder="Opcional. La puede llenar el capacitador desde el link firmado."
              />
            </div>
          </div>

          {/* Responsables ---------------------------------------------- */}
          <div className={styles.sectionTitle}>
            <span>Responsables</span>
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              onClick={() => setPickerOpen(true)}
              disabled={loadingCatalogos || availableToAdd.length === 0}
              title={
                availableToAdd.length === 0
                  ? 'No hay más responsables disponibles en el catálogo.'
                  : 'Agregar responsable'
              }
            >
              <Plus width={14} height={14} />
              <span>Agregar responsable</span>
            </button>
          </div>

          <div className={styles.responsablesHint}>
            El capacitador siempre firma el certificado. Los responsables
            seleccionados aquí firmarán en el orden listado. Adminístralos en{' '}
            <strong>Responsables</strong> (catálogo).
          </div>

          {responsableIds.length === 0 ? (
            <div className={styles.emptyResponsables}>
              Sin responsables adicionales.
            </div>
          ) : (
            <ul className={styles.responsableList}>
              {responsableIds.map((id, idx) => {
                const r = responsablesById.get(id);
                const missingSignature = r && r.tieneFirma === false;
                const isUnknown = !r; // seleccionado pero no en el catálogo cargado (inactivo?)
                return (
                  <li key={id} className={styles.responsableItem}>
                    <div className={styles.responsableOrder}>{idx + 1}</div>
                    <div className={styles.responsableInfo}>
                      <div className={styles.responsableName}>
                        {r?.nombres || 'Responsable desconocido'}
                        {missingSignature && (
                          <span
                            className={styles.warningBadge}
                            title="Este responsable no ha cargado su firma — se requerirá para emitir certificados"
                          >
                            <AlertTriangle width={12} height={12} />
                            <span>Sin firma</span>
                          </span>
                        )}
                        {isUnknown && (
                          <span
                            className={styles.warningBadge}
                            title="Este responsable ya no está activo en el catálogo. Considera quitarlo."
                          >
                            <AlertTriangle width={12} height={12} />
                            <span>Inactivo</span>
                          </span>
                        )}
                      </div>
                      <div className={styles.responsableMeta}>
                        {(r?.cargo || '—')}{' · '}{(r?.empresa || '—')}
                      </div>
                    </div>
                    <div className={styles.responsableActions}>
                      <button
                        type="button"
                        className={styles.iconBtn}
                        onClick={() => moveResponsable(id, 'up')}
                        disabled={idx === 0}
                        title="Mover arriba"
                        aria-label="Mover arriba"
                      >
                        <ChevronUp width={16} height={16} />
                      </button>
                      <button
                        type="button"
                        className={styles.iconBtn}
                        onClick={() => moveResponsable(id, 'down')}
                        disabled={idx === responsableIds.length - 1}
                        title="Mover abajo"
                        aria-label="Mover abajo"
                      >
                        <ChevronDown width={16} height={16} />
                      </button>
                      <button
                        type="button"
                        className={`${styles.iconBtn} ${styles.iconBtnDanger}`}
                        onClick={() => removeResponsable(id)}
                        title="Quitar"
                        aria-label="Quitar responsable"
                      >
                        <Trash2 width={16} height={16} />
                      </button>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}

          {/* Submit oculto para permitir envío con Enter */}
          <button type="submit" style={{ display: 'none' }} aria-hidden="true" />
        </form>
      )}

      {/* Sub-modal: selector de responsable del catálogo */}
      <Modal
        isOpen={pickerOpen}
        onClose={() => setPickerOpen(false)}
        title="Seleccionar responsable"
        footer={
          <button
            type="button"
            className="btn btn--secondary"
            onClick={() => setPickerOpen(false)}
          >
            Cerrar
          </button>
        }
      >
        {availableToAdd.length === 0 ? (
          <div className={styles.pickerEmpty}>
            No hay más responsables disponibles. Crea uno nuevo en la pantalla{' '}
            <strong>Responsables</strong>.
          </div>
        ) : (
          <ul className={styles.pickerList}>
            {availableToAdd.map((r) => (
              <li key={r.id}>
                <button
                  type="button"
                  className={styles.pickerItem}
                  onClick={() => addResponsable(r.id)}
                >
                  <div className={styles.pickerItemMain}>
                    <div className={styles.pickerItemName}>{r.nombres}</div>
                    <div className={styles.pickerItemMeta}>
                      {r.cargo || '—'}{' · '}{r.empresa || '—'}
                    </div>
                  </div>
                  {r.tieneFirma === false && (
                    <span
                      className={styles.warningBadge}
                      title="Este responsable no ha cargado su firma"
                    >
                      <AlertTriangle width={12} height={12} />
                      <span>Sin firma</span>
                    </span>
                  )}
                </button>
              </li>
            ))}
          </ul>
        )}
      </Modal>
    </Modal>
  );
}

/** Convierte ISO → valor aceptado por `<input type="datetime-local">` (sin segundos). */
function isoToLocalInput(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Convierte valor de `<input type="datetime-local">` a ISO con offset local. */
function localInputToIso(value) {
  if (!value) return null;
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return null;
  return d.toISOString();
}
