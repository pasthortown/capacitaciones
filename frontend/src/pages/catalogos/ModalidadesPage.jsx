import CatalogoPage from './CatalogoPage.jsx';

/**
 * Pantalla CRUD de Modalidades (Presencial, Virtual, Híbrida, ...).
 */
export default function ModalidadesPage() {
  return (
    <CatalogoPage
      tipo="modalidades"
      titulo="Modalidades"
      descripcion="Tipos de modalidad en que se dictan las capacitaciones."
    />
  );
}
