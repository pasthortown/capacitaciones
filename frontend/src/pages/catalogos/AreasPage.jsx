import CatalogoPage from './CatalogoPage.jsx';

/**
 * Pantalla CRUD de Áreas (dominio del usuario, sin seed).
 */
export default function AreasPage() {
  return (
    <CatalogoPage
      tipo="areas"
      titulo="Áreas"
      descripcion="Áreas organizacionales a las que pertenecen los asistentes."
    />
  );
}
