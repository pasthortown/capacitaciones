import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { sri } from 'vite-plugin-sri3';

// https://vitejs.dev/config/
export default defineConfig({
  // Prefijo opcional para servir el SPA bajo una ruta (ej. `/capacitados/`).
  // En dev/local queda `/`. Se setea `VITE_BASE_PATH` en el build de prod.
  base: process.env.VITE_BASE_PATH || '/',
  // `vite-plugin-sri3` añade `integrity="sha384-..."` en los <script>/<link>
  // emitidos por Vite — cierra el hallazgo OWASP 90003 (Sub-Resource Integrity).
  plugins: [react(), sri()],
  server: {
    host: true,
    port: 5173,
    proxy: {
      // En desarrollo local fuera de Docker, el backend .NET corre en :8080.
      // En produccion (Docker + nginx), nginx hace el proxy de /api -> backend:8080.
      '/api': {
        target: 'http://localhost:8080',
        changeOrigin: true,
        secure: false,
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: false,
    emptyOutDir: true,
  },
});
