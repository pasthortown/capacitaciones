import { useCallback, useEffect, useMemo, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import DataTable from '../components/Table/DataTable.jsx';
import { useToast } from '../components/Toast/useToast.js';
import { confirm as swalConfirm } from '../utils/swal.js';
import usuariosService from '../services/usuarios.js';

/**
 * Usuarios permitidos: lista simple de usuarios de red (samAccountName) que pueden ingresar
 * al sistema. El login valida contra el dominio y exige que el usuario esté en esta lista.
 * El nombre a mostrar lo entrega el dominio al ingresar (no se guarda aquí).
 */
const fechaCorta = (s) => (s ? String(s).slice(0, 10) : '—');

export default function UsuariosPage() {
  const toast = useToast();
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [nuevo, setNuevo] = useState('');
  const [guardando, setGuardando] = useState(false);

  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const data = await usuariosService.list();
      setItems(Array.isArray(data) ? data : []);
    } catch (err) {
      toast.error(err?.message || 'No se pudieron cargar los usuarios.');
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => { fetchList(); }, [fetchList]);

  const agregar = async () => {
    const u = nuevo.trim();
    if (!u) { toast.error('Ingresa el usuario de red.'); return; }
    setGuardando(true);
    try {
      await usuariosService.create(u);
      setNuevo('');
      toast.success('Usuario agregado.');
      await fetchList();
    } catch (err) {
      toast.error(err?.message || 'No se pudo agregar el usuario.');
    } finally {
      setGuardando(false);
    }
  };

  const eliminar = async (row) => {
    const ok = await swalConfirm({
      title: 'Quitar usuario',
      text: `El usuario de red "${row.usuarioRed}" dejará de poder ingresar al sistema.`,
      icon: 'warning', confirmText: 'Sí, quitar', cancelText: 'Cancelar', danger: true,
    });
    if (!ok) return;
    try {
      await usuariosService.remove(row.id);
      toast.success('Usuario quitado.');
      await fetchList();
    } catch (err) {
      toast.error(err?.message || 'No se pudo quitar el usuario.');
    }
  };

  const columns = useMemo(() => [
    { key: 'usuarioRed', header: 'Usuario de red', accessor: (r) => r.usuarioRed },
    { key: 'ultimoLogin', header: 'Último ingreso', accessor: (r) => fechaCorta(r.ultimoLogin) },
    {
      key: 'acciones', header: '', align: 'right',
      accessor: (r) => (
        <button type="button" className="btn btn--ghost btn--sm btn--icon" title="Quitar"
          onClick={() => eliminar(r)}>
          <Trash2 width={16} height={16} />
        </button>
      ),
    },
  ], []); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-header__title">Usuarios</h1>
          <p className="page-header__subtitle">
            Usuarios de red autorizados a ingresar. El ingreso se valida contra el dominio y solo se
            admite a quienes estén en esta lista.
          </p>
        </div>
      </div>

      <div className="toolbar">
        <div className="toolbar__filters" style={{ display: 'flex', gap: 'var(--spacing-2)', alignItems: 'center' }}>
          <input className="form-input" style={{ minWidth: 260 }} placeholder="Usuario de red (ej. jperez)"
            value={nuevo} onChange={(e) => setNuevo(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') agregar(); }} />
          <button type="button" className="btn btn--primary" onClick={agregar} disabled={guardando}>
            <Plus width={16} height={16} /><span>{guardando ? 'Agregando…' : 'Agregar'}</span>
          </button>
        </div>
      </div>

      <div className="card">
        <div className="card__body" style={{ padding: 0 }}>
          <DataTable columns={columns} rows={items} loading={loading}
            emptyMessage="No hay usuarios autorizados. Agrega al menos uno." />
        </div>
      </div>
    </div>
  );
}
