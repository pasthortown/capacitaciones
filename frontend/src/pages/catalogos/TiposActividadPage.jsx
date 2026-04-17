import CatalogoPage from './CatalogoPage.jsx';

/**
 * Pantalla CRUD de Tipos de Actividad (Charla, Workshop, Curso, Taller, ...).
 */
export default function TiposActividadPage() {
  return (
    <CatalogoPage
      tipo="tipos-actividad"
      titulo="Tipos de actividad"
      descripcion="Clasificación de la actividad formativa: charla, workshop, curso, etc."
    />
  );
}
