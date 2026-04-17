import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { LogIn } from 'lucide-react';
import TextField from '../components/Forms/TextField.jsx';
import Spinner from '../components/Spinner/Spinner.jsx';
import { useAuth } from '../auth/useAuth.js';
import { HttpError } from '../services/http.js';
import styles from './LoginPage.module.css';

/**
 * Pantalla de login del admin.
 *
 * - No usa `AppLayout` (ni sidebar ni header admin) — es una vista plena.
 * - Inputs: correo completo (ej. `admin@dos.com.ec`) + contraseña.
 * - Al éxito: redirige a `state.from?.pathname || '/'`.
 * - 401: mensaje genérico "Credenciales inválidas".
 * - Otros errores: muestra `error.message` de la API.
 */
export default function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { login, isAuthenticated, isLoading } = useAuth();

  const redirectTo = location.state?.from?.pathname || '/';

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState('');

  // Si ya hay sesión (por ejemplo: navegó manualmente a /login con token
  // válido), mandarlo al destino.
  useEffect(() => {
    if (!isLoading && isAuthenticated) {
      navigate(redirectTo, { replace: true });
    }
  }, [isAuthenticated, isLoading, navigate, redirectTo]);

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (submitting) return;

    setFormError('');
    const trimmedEmail = email.trim();
    if (!trimmedEmail || !password) {
      setFormError('Ingresa correo y contraseña.');
      return;
    }

    setSubmitting(true);
    try {
      await login(trimmedEmail, password);
      navigate(redirectTo, { replace: true });
    } catch (error) {
      if (error instanceof HttpError && error.status === 401) {
        setFormError('Credenciales inválidas.');
      } else {
        setFormError(error?.message || 'No se pudo iniciar sesión.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  // Durante el bootstrap inicial no tiene sentido mostrar el form —
  // puede haber un token válido en proceso de validación.
  if (isLoading) {
    return <Spinner fullscreen size={40} label="Cargando sesión..." />;
  }

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <div className={styles.brand}>
          <h1 className={styles.brandTitle}>Capacitaciones</h1>
          <p className={styles.brandSubtitle}>Panel administrativo</p>
        </div>

        <form onSubmit={handleSubmit} noValidate>
          <TextField
            label="Correo"
            name="email"
            type="email"
            value={email}
            onChange={setEmail}
            placeholder="admin@dos.com.ec"
            autoComplete="username"
            required
            disabled={submitting}
          />
          <TextField
            label="Contraseña"
            name="password"
            type="password"
            value={password}
            onChange={setPassword}
            placeholder="••••••••"
            autoComplete="current-password"
            required
            disabled={submitting}
          />

          {formError && (
            <div className={`alert alert--error ${styles.error}`} role="alert">
              <div className="alert__content">
                <div className="alert__message">{formError}</div>
              </div>
            </div>
          )}

          <button
            type="submit"
            className={`btn btn--primary ${styles.submit}`}
            disabled={submitting}
          >
            {submitting ? (
              <Spinner size={16} label="Iniciando sesión..." />
            ) : (
              <LogIn width={16} height={16} />
            )}
            <span>{submitting ? 'Iniciando...' : 'Iniciar sesión'}</span>
          </button>
        </form>
      </div>
    </div>
  );
}
