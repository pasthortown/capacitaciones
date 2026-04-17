import { useCallback, useEffect, useRef, useState } from 'react';
import catalogosService from '../services/catalogos.js';
import { useToast } from '../components/Toast/useToast.js';

/**
 * Hook para orquestar un catálogo (modalidades | tipos-actividad | areas).
 *
 * Gestiona estado local (items, loading, includeInactive) y expone acciones
 * que emiten toasts al finalizar:
 *   - refresh():                   recarga la lista.
 *   - setIncludeInactive(bool):    cambia el filtro (dispara refresh).
 *   - create({ nombre, activo }):  crea y recarga; retorna el creado.
 *   - update(id, { nombre, activo }): actualiza y recarga; retorna el actualizado.
 *   - remove(id):                  eliminación lógica y recarga.
 *   - downloadTemplate():          descarga XLSX vacío.
 *   - uploadTemplate(file):        sube XLSX; retorna el resumen tal cual del backend.
 */
export default function useCatalogo(slug) {
  const toast = useToast();

  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [includeInactive, setIncludeInactiveState] = useState(false);

  // Para evitar setState tras unmount.
  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const fetchList = useCallback(
    async (flagIncludeInactive) => {
      if (!slug) return;
      setLoading(true);
      try {
        const data = await catalogosService.list(slug, {
          includeInactive: flagIncludeInactive,
        });
        if (mountedRef.current) {
          setItems(Array.isArray(data) ? data : []);
        }
      } catch (error) {
        if (mountedRef.current) {
          setItems([]);
        }
        toast.error(error.message || 'No se pudo cargar el catálogo.');
      } finally {
        if (mountedRef.current) {
          setLoading(false);
        }
      }
    },
    [slug, toast],
  );

  // Carga inicial y cuando cambie el slug o el flag.
  useEffect(() => {
    fetchList(includeInactive);
  }, [fetchList, includeInactive]);

  const refresh = useCallback(() => fetchList(includeInactive), [fetchList, includeInactive]);

  const setIncludeInactive = useCallback((value) => {
    setIncludeInactiveState(Boolean(value));
  }, []);

  const create = useCallback(
    async (payload) => {
      try {
        const created = await catalogosService.create(slug, payload);
        toast.success('Registro creado correctamente.');
        await fetchList(includeInactive);
        return created;
      } catch (error) {
        toast.error(error.message || 'No se pudo crear el registro.');
        throw error;
      }
    },
    [slug, toast, fetchList, includeInactive],
  );

  const update = useCallback(
    async (id, payload) => {
      try {
        const updated = await catalogosService.update(slug, id, payload);
        toast.success('Registro actualizado.');
        await fetchList(includeInactive);
        return updated;
      } catch (error) {
        toast.error(error.message || 'No se pudo actualizar el registro.');
        throw error;
      }
    },
    [slug, toast, fetchList, includeInactive],
  );

  const remove = useCallback(
    async (id) => {
      try {
        await catalogosService.remove(slug, id);
        toast.success('Registro eliminado.');
        await fetchList(includeInactive);
      } catch (error) {
        toast.error(error.message || 'No se pudo eliminar el registro.');
        throw error;
      }
    },
    [slug, toast, fetchList, includeInactive],
  );

  const downloadTemplate = useCallback(async () => {
    try {
      await catalogosService.downloadTemplate(slug);
      toast.success('Plantilla descargada.');
    } catch (error) {
      toast.error(error.message || 'No se pudo descargar la plantilla.');
      throw error;
    }
  }, [slug, toast]);

  const uploadTemplate = useCallback(
    async (file) => {
      try {
        const resumen = await catalogosService.uploadTemplate(slug, file);
        return resumen;
      } catch (error) {
        toast.error(error.message || 'No se pudo procesar el archivo.');
        throw error;
      }
    },
    [slug, toast],
  );

  return {
    items,
    loading,
    includeInactive,
    setIncludeInactive,
    refresh,
    create,
    update,
    remove,
    downloadTemplate,
    uploadTemplate,
  };
}
