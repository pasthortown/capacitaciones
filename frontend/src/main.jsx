import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import App from './App.jsx';
import { AuthProvider } from './auth/AuthProvider.jsx';
import { ToastProvider } from './components/Toast/ToastContext.jsx';
import './styles/index.css';

/**
 * Orden de providers (outer → inner):
 *   BrowserRouter → AuthProvider → ToastProvider → App
 *
 * - BrowserRouter debe ser el más externo para que los hooks de router
 *   (useNavigate, Navigate) funcionen dentro de AuthProvider si se necesitara.
 * - AuthProvider envuelve a ToastProvider porque los toasts son UI
 *   y no dependen de auth, pero AuthProvider podría querer emitir toasts
 *   en futuros flujos (ver refactor futuro) — por ahora no los usa.
 * - ToastProvider queda cerca de la app para que cualquier página/feature
 *   pueda invocar `useToast()` sin importar si está autenticada.
 */
ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <ToastProvider>
          <App />
        </ToastProvider>
      </AuthProvider>
    </BrowserRouter>
  </React.StrictMode>,
);
