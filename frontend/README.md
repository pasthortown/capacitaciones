# Capacitaciones - Frontend (React + Vite)

SPA administrativa del sistema **Capacitaciones**. Consume el design system
en `../style/` (no duplicar tokens aqui) y se integra con el backend .NET
por el prefijo `/api`.

## Requisitos

- Node.js >= 18.18 (recomendado: 20.x).
- npm 10+.

## Scripts

```bash
npm install    # instala dependencias (primera vez / tras cambios en package.json)
npm run dev    # servidor de desarrollo en http://localhost:5173
npm run build  # build de produccion en ./dist
npm run preview # sirve el build local para smoke-test
npm run lint   # ESLint
```

## Estructura

```
frontend/
├── src/
│   ├── components/
│   │   ├── Modal/        # Modal reusable (reglas UX §7.3)
│   │   ├── Sidebar/      # Navegacion lateral
│   │   └── Icon/         # Wrapper de lucide-react
│   ├── layouts/
│   │   └── AppLayout.jsx # Sidebar + Body (<Outlet/>)
│   ├── pages/
│   │   └── HomePage.jsx  # Bienvenida (placeholder)
│   ├── hooks/            # hooks reusables (vacio por ahora)
│   ├── services/
│   │   └── http.js       # Cliente HTTP centralizado
│   ├── styles/
│   │   └── index.css     # Importa ../../style/*.css (design system)
│   ├── App.jsx           # Router (rutas principales)
│   └── main.jsx          # Entry point
├── Dockerfile
├── index.html
├── package.json
└── vite.config.js
```

## Design system

El archivo `src/styles/index.css` importa los CSS oficiales desde `../../style/`:

```css
@import '../../../style/variables.css';
@import '../../../style/main.css';
@import '../../../style/utilities.css';
@import '../../../style/components.css';
```

No se debe introducir Bootstrap, MUI, Tailwind u otras librerias de estilo.

## Cliente HTTP

Un unico wrapper `src/services/http.js`. Los componentes no deben llamar a
`fetch` directamente. Base por defecto: `import.meta.env.VITE_API_BASE` o
`/api` (proxy del dev server / nginx).

El token JWT (cuando exista login) se lee de `localStorage` bajo la key
`capacitaciones.authToken` y se inyecta como `Authorization: Bearer ...`.

## Modal

`src/components/Modal/Modal.jsx` implementa las reglas UX §7.3:

- NO cierra al click fuera del contenido.
- SI cierra con ESC, boton X, boton Cerrar/Cancelar del footer, o tras submit
  exitoso controlado por el padre.

## Integracion con Nginx / Docker

Hay dos flujos soportados:

### (a) Build local + volumen (flujo principal del `docker-compose.yml`)

```bash
cd frontend
npm ci
npm run build
# ./frontend/dist queda listo. El contenedor nginx lo monta en /usr/share/nginx/html
docker compose up nginx
```

### (b) Build-as-image (CI/CD alternativo)

```bash
docker build -t capacitaciones-frontend:local ./frontend
# Exportar el artefacto:
docker create --name tmp capacitaciones-frontend:local
docker cp tmp:/app/dist ./frontend/dist
docker rm tmp
```

> El Dockerfile incluye etapa `build` y una etapa final `export` minima
> (alpine) con el artefacto en `/app/dist`. El proxy `/api` lo resuelve nginx
> en el compose (no el frontend).

## Variables de entorno

Prefijo requerido por Vite: `VITE_`.

| Variable        | Default | Descripcion                                  |
| --------------- | ------- | -------------------------------------------- |
| `VITE_API_BASE` | `/api`  | Prefijo del cliente HTTP.                    |

## Fases pendientes (roadmap del PM)

- Fase 1: paginas de catalogos (Modalidad, TipoActividad, Area) + modales CRUD.
- Fase 2: pantalla de configuracion de numeracion.
- Fase 3: dashboard de capacitaciones + grid de cards.
- Fase 4: pagina del capacitador (link firmado).
- Fase 5: pagina publica de inscripcion + componente SignaturePad.
