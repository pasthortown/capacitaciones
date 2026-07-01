import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/useAuth.js';
import { HttpError } from '../services/http.js';
import Spinner from '../components/Spinner/Spinner.jsx';
import loginBg from '../assets/login-bg.webp';

/**
 * Pantalla de login. Ingreso por dominio: usuario de red + contraseña corporativa.
 * Estilo tomado de ControlTareas (fondo full-screen + panel translúcido).
 */
const wrapper = {
  minHeight: '100vh',
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  padding: '24px',
  backgroundColor: '#08080f',
  backgroundImage: `url(${loginBg})`,
  backgroundSize: 'cover',
  backgroundPosition: 'center',
  backgroundRepeat: 'no-repeat',
  overflowY: 'auto',
};
const panel = {
  width: '100%',
  maxWidth: '400px',
  background: 'rgba(8, 8, 18, 0.55)',
  backdropFilter: 'blur(3px)',
  WebkitBackdropFilter: 'blur(3px)',
  border: '1px solid rgba(255,255,255,0.10)',
  borderRadius: '16px',
  padding: '32px 28px',
  boxShadow: '0 18px 50px rgba(0,0,0,0.55)',
};
const labelStyle = { display: 'block', color: '#e5e7eb', fontSize: '13px', marginBottom: '6px', fontWeight: 600 };
const inputStyle = {
  width: '100%', boxSizing: 'border-box', padding: '10px 12px', fontSize: '15px',
  color: '#fff', background: 'rgba(255,255,255,0.12)',
  border: '1px solid rgba(255,255,255,0.25)', borderRadius: '8px', outline: 'none',
};
const errorStyle = { color: '#fca5a5', fontSize: '13px', marginBottom: '12px', lineHeight: 1.4 };

export default function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { login, isAuthenticated, isLoading } = useAuth();

  const redirectTo = location.state?.from?.pathname || '/';

  const [usuario, setUsuario] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState('');

  useEffect(() => {
    if (!isLoading && isAuthenticated) navigate(redirectTo, { replace: true });
  }, [isAuthenticated, isLoading, navigate, redirectTo]);

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (submitting) return;
    setFormError('');
    const u = usuario.trim();
    if (!u || !password) { setFormError('Ingresa tu usuario de red y contraseña.'); return; }
    setSubmitting(true);
    try {
      await login(u, password);
      navigate(redirectTo, { replace: true });
    } catch (error) {
      if (error instanceof HttpError && error.status === 401) {
        setFormError('Usuario no autorizado o credenciales incorrectas.');
      } else {
        setFormError(error?.message || 'No se pudo iniciar sesión.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (isLoading) return <Spinner fullscreen size={40} label="Cargando sesión..." />;

  return (
    <div style={wrapper}>
      <div style={panel}>
        <div style={{ textAlign: 'center', marginBottom: '22px' }}>
          <div style={{ background: '#fff', borderRadius: '12px', padding: '14px 18px', display: 'inline-block' }}>
            <img src="/logo.png" alt="Capacitados — DOS" style={{ width: '210px', maxWidth: '100%', display: 'block' }} />
          </div>
          <h1 style={{ color: '#fff', fontSize: '15px', fontWeight: 700, letterSpacing: '0.5px', marginTop: '18px', marginBottom: 0 }}>
            SISTEMA DE REGISTRO DE CAPACITACIONES
          </h1>
        </div>

        <form onSubmit={handleSubmit} noValidate>
          <div style={{ marginBottom: '16px' }}>
            <label style={labelStyle} htmlFor="loginUsuario">Usuario de red</label>
            <input id="loginUsuario" type="text" style={inputStyle} value={usuario}
              onChange={(e) => setUsuario(e.target.value)} autoFocus autoComplete="username"
              placeholder="ej. jperez" disabled={submitting} />
          </div>
          <div style={{ marginBottom: '20px' }}>
            <label style={labelStyle} htmlFor="loginPass">Contraseña</label>
            <input id="loginPass" type="password" style={inputStyle} value={password}
              onChange={(e) => setPassword(e.target.value)} autoComplete="current-password"
              placeholder="••••••••" disabled={submitting} />
          </div>

          {formError && <p style={errorStyle} role="alert">{formError}</p>}

          <button type="submit" className="btn btn--primary" style={{ width: '100%' }} disabled={submitting}>
            {submitting ? 'Ingresando…' : 'Iniciar sesión'}
          </button>
        </form>
      </div>
    </div>
  );
}
