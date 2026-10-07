import { useEffect, useRef, useState } from 'react';
import Modal from '../../components/Modal/Modal.jsx';
import SignaturePad from '../../components/SignaturePad/SignaturePad.jsx';
import EmailConSufijo from '../../components/EmailConSufijo/EmailConSufijo.jsx';
import { useToast } from '../../components/Toast/useToast.js';
import { buscarPersonaInscrito, createInscrito } from '../../services/inscritos.js';

/**
 * Modal "Agregar inscrito" de la pantalla admin: registra a un colaborador que asistió pero no se
 * inscribió por el link, también en capacitaciones ya finalizadas.
 *  - Al salir del campo Cédula se autocompletan los datos desde el registro de personas; si la
 *    persona tiene firma guardada se ofrece "Usar la firma registrada".
 *  - La firma es opcional.
 *  - "Asistió" (marcada por defecto) deja al inscrito Presente, elegible a certificado.
 */

const EMPTY_FORM = {
  capacitacionId: '',
  identificacion: '',
  nombres: '',
  apellidos: '',
  areaId: '',
  emailUsuario: '',
};

export default function AgregarInscritoModal({ isOpen, onClose, onCreated, capacitaciones, areas, capacitacionIdInicial, mensajeError }) {
  const toast = useToast();

  const [form, setForm] = useState(EMPTY_FORM);
  const [firma, setFirma] = useState(null);
  const [asistio, setAsistio] = useState(true);
  const [personaEncontrada, setPersonaEncontrada] = useState(false);
  const [tieneFirmaRegistrada, setTieneFirmaRegistrada] = useState(false);
  const [usarFirmaRegistrada, setUsarFirmaRegistrada] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');

  // Cédula consultada y campos que se autocompletaron con ella: si cambia la cédula se quitan los
  // datos de la otra persona (salvo los editados a mano) y una respuesta tardía se descarta.
  const ultimaIdentificacionRef = useRef('');
  const autocompletadoRef = useRef(null);

  const areasActivas = areas.filter((a) => a.activo !== false);

  useEffect(() => {
    if (!isOpen) return;
    setForm({ ...EMPTY_FORM, capacitacionId: capacitacionIdInicial || '' });
    setFirma(null);
    setAsistio(true);
    setPersonaEncontrada(false);
    setTieneFirmaRegistrada(false);
    setUsarFirmaRegistrada(false);
    setFormError('');
    ultimaIdentificacionRef.current = '';
    autocompletadoRef.current = null;
  }, [isOpen, capacitacionIdInicial]);

  const cerrar = () => {
    if (saving) return;
    ultimaIdentificacionRef.current = '';
    onClose();
  };

  const handleIdentificacionChange = (value) => {
    const autocompletado = autocompletadoRef.current;
    const cambio = value.trim() !== ultimaIdentificacionRef.current;
    setForm((prev) => {
      const next = { ...prev, identificacion: value };
      if (cambio && autocompletado) {
        for (const campo of Object.keys(autocompletado)) {
          if (next[campo] === autocompletado[campo]) next[campo] = EMPTY_FORM[campo];
        }
      }
      return next;
    });
    if (cambio) {
      ultimaIdentificacionRef.current = '';
      autocompletadoRef.current = null;
      // Nunca usar la firma de otra persona.
      setPersonaEncontrada(false);
      setTieneFirmaRegistrada(false);
      setUsarFirmaRegistrada(false);
    }
  };

  const handleIdentificacionBlur = async () => {
    const identificacion = form.identificacion.trim();
    if (!identificacion || identificacion === ultimaIdentificacionRef.current) return;
    ultimaIdentificacionRef.current = identificacion;
    try {
      const persona = await buscarPersonaInscrito(identificacion);
      if (ultimaIdentificacionRef.current !== identificacion || !persona) return;
      const autocompletado = {};
      if (persona.nombres) autocompletado.nombres = persona.nombres;
      if (persona.apellidos) autocompletado.apellidos = persona.apellidos;
      if (persona.emailUsuario) autocompletado.emailUsuario = persona.emailUsuario;
      if (persona.areaId && areasActivas.some((a) => a.id === persona.areaId)) autocompletado.areaId = persona.areaId;
      autocompletadoRef.current = autocompletado;
      setForm((prev) => ({ ...prev, ...autocompletado }));
      setPersonaEncontrada(true);
      setTieneFirmaRegistrada(Boolean(persona.tieneFirma));
      setUsarFirmaRegistrada(Boolean(persona.tieneFirma));
    } catch {
      // 404 (persona nueva) o error: el formulario se llena a mano.
    }
  };

  const validar = () => {
    if (!form.capacitacionId) return 'Selecciona una capacitación.';
    if (!form.identificacion.trim()) return 'Ingresa la cédula.';
    if (!form.nombres.trim()) return 'Ingresa los nombres.';
    if (!form.apellidos.trim()) return 'Ingresa los apellidos.';
    if (!form.areaId) return 'Selecciona un área.';
    if (!form.emailUsuario.trim()) return 'Ingresa el correo.';
    if (form.emailUsuario.includes('@')) return 'Ingresa solo la parte del correo antes de @dos.com.ec.';
    return null;
  };

  const guardar = async () => {
    if (saving) return;
    const err = validar();
    if (err) {
      setFormError(err);
      return;
    }
    setSaving(true);
    setFormError('');
    try {
      await createInscrito({
        capacitacionId: form.capacitacionId,
        nombres: form.nombres.trim(),
        apellidos: form.apellidos.trim(),
        identificacion: form.identificacion.trim(),
        areaId: form.areaId,
        emailUsuario: form.emailUsuario.trim(),
        firma: usarFirmaRegistrada ? null : firma,
        usarFirmaRegistrada,
        asistio,
      });
      toast.success('Inscrito agregado.');
      setSaving(false);
      onCreated(form.capacitacionId);
    } catch (error) {
      setFormError(mensajeError(error, 'No se pudo agregar el inscrito.'));
      setSaving(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={cerrar}
      title="Agregar inscrito"
      footer={
        <>
          <button type="button" className="btn btn--secondary" onClick={cerrar} disabled={saving}>
            Cancelar
          </button>
          <button type="button" className="btn btn--primary" onClick={guardar} disabled={saving}>
            {saving ? 'Guardando...' : 'Agregar'}
          </button>
        </>
      }
    >
      <div>
        <div className="form-group">
          <label className="form-label form-label--required" htmlFor="add-capacitacion">Capacitación</label>
          <select
            id="add-capacitacion"
            className="form-input form-select"
            value={form.capacitacionId}
            onChange={(e) => setForm((p) => ({ ...p, capacitacionId: e.target.value }))}
            disabled={saving}
          >
            <option value="">Selecciona una capacitación</option>
            {capacitaciones.map((c) => (
              <option key={c.id} value={c.id}>
                {c.codigo} — {c.tema}
              </option>
            ))}
          </select>
          <p className="form-helper">Se puede agregar aunque la capacitación ya haya finalizado.</p>
        </div>

        <div className="form-row">
          <div className="form-group">
            <label className="form-label form-label--required" htmlFor="add-cedula">Cédula</label>
            <input
              id="add-cedula"
              className="form-input"
              value={form.identificacion}
              maxLength={30}
              onChange={(e) => handleIdentificacionChange(e.target.value)}
              onBlur={handleIdentificacionBlur}
              disabled={saving}
            />
          </div>
          <div className="form-group">
            <label className="form-label form-label--required" htmlFor="add-area">Área</label>
            <select
              id="add-area"
              className="form-input form-select"
              value={form.areaId}
              onChange={(e) => setForm((p) => ({ ...p, areaId: e.target.value }))}
              disabled={saving}
            >
              <option value="">Selecciona un área</option>
              {areasActivas.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.nombre}
                </option>
              ))}
            </select>
          </div>
        </div>

        {personaEncontrada && (
          <p className="form-helper" role="status">
            Datos tomados del registro de personas. Revísalos antes de guardar.
          </p>
        )}

        <div className="form-row">
          <div className="form-group">
            <label className="form-label form-label--required" htmlFor="add-nombres">Nombres</label>
            <input
              id="add-nombres"
              className="form-input"
              value={form.nombres}
              maxLength={120}
              onChange={(e) => setForm((p) => ({ ...p, nombres: e.target.value }))}
              disabled={saving}
            />
          </div>
          <div className="form-group">
            <label className="form-label form-label--required" htmlFor="add-apellidos">Apellidos</label>
            <input
              id="add-apellidos"
              className="form-input"
              value={form.apellidos}
              maxLength={120}
              onChange={(e) => setForm((p) => ({ ...p, apellidos: e.target.value }))}
              disabled={saving}
            />
          </div>
        </div>

        <div className="form-group">
          <label className="form-label form-label--required" htmlFor="add-email">Correo</label>
          <EmailConSufijo
            id="add-email"
            name="add-email"
            value={form.emailUsuario}
            onChange={(value) => setForm((p) => ({ ...p, emailUsuario: value }))}
            disabled={saving}
            required
          />
        </div>

        <div className="form-group">
          <label className="form-label">Firma</label>
          {tieneFirmaRegistrada && (
            <label className="form-checkbox">
              <input
                type="checkbox"
                className="form-checkbox__input"
                checked={usarFirmaRegistrada}
                onChange={(e) => setUsarFirmaRegistrada(e.target.checked)}
                disabled={saving}
              />
              <span className="form-checkbox__label">Usar la firma registrada</span>
            </label>
          )}
          {!usarFirmaRegistrada && (
            <>
              <SignaturePad value={firma} onChange={setFirma} width={400} height={150} disabled={saving} />
              <p className="form-helper">Opcional. Puedes dibujarla o subirla, o guardar sin firma.</p>
            </>
          )}
        </div>

        <div className="form-group">
          <label className="form-checkbox">
            <input
              type="checkbox"
              className="form-checkbox__input"
              checked={asistio}
              onChange={(e) => setAsistio(e.target.checked)}
              disabled={saving}
            />
            <span className="form-checkbox__label">Asistió</span>
          </label>
          <p className="form-helper">
            {asistio
              ? 'Queda como Presente y podrá recibir certificado.'
              : 'Queda sin marcar; podrás marcar la asistencia luego en el pase de lista.'}
          </p>
        </div>

        {formError && (
          <div className="alert alert--error" role="alert">
            {formError}
          </div>
        )}
      </div>
    </Modal>
  );
}
