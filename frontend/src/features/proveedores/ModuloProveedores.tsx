import { useState, useEffect } from 'react';
import { Plus, Edit2, Trash2 } from 'lucide-react';
import { ModalProveedor } from './ModalProveedor';
import toast from 'react-hot-toast';
import { mensajeDeError, ERROR_DE_CONEXION } from '../../api/errores';

export default function ModuloProveedores() {
  const [proveedores, setproveedores] = useState<any[]>([]);
  const [showModal, setShowModal] = useState(false);
  const [proveedorEditando, setproveedorEditando] = useState<any | null>(null);

  const fetchproveedores = () => {
    fetch('/api/proveedores')
      .then(res => res.json())
      .then(data => setproveedores(data));
  };

  useEffect(() => {
    fetchproveedores();
  }, []);

  const handleEliminar = async (id: number) => {
    if (!window.confirm('¿Estás seguro de que deseas eliminar este proveedor?')) return;

    try {
      const res = await fetch(`/api/proveedores/${id}`, {
        method: 'DELETE'
      });
      if (res.ok) {
        setproveedores(proveedores.filter(c => c.id !== id));
        toast.success('proveedor eliminado exitosamente');
      } else {
        toast.error(await mensajeDeError(res, 'Error al eliminar el proveedor'));
      }
    } catch (error) {
      console.error(error);
      toast.error(ERROR_DE_CONEXION);
    }
  };

  const handleEditar = (proveedor: any) => {
    setproveedorEditando(proveedor);
    setShowModal(true);
  };

  const handleNuevo = () => {
    setproveedorEditando(null);
    setShowModal(true);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between border-b border-slate-200 pb-4">
        <h2 className="text-xl font-semibold text-slate-900">Directorio de proveedores</h2>
        <button
          onClick={handleNuevo}
          className="inline-flex items-center gap-2 rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-indigo-700"
        >
          <Plus className="h-4 w-4" />
          Nuevo proveedor
        </button>
      </div>

      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="min-w-full divide-y divide-slate-200">
          <thead className="bg-slate-50">
            <tr>
              <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-slate-500">ID</th>
              <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-slate-500">Razón Social</th>
              <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-slate-500">CUIT / DNI</th>
              <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-slate-500">Condición IVA</th>
              <th className="px-6 py-3 text-right text-xs font-medium uppercase tracking-wider text-slate-500">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 bg-white">
            {proveedores.map(c => (
              <tr key={c.id} className="hover:bg-slate-50">
                <td className="whitespace-nowrap px-6 py-4 text-sm text-slate-500">#{c.id}</td>
                <td className="whitespace-nowrap px-6 py-4 text-sm font-medium text-slate-900">{c.razonSocial}</td>
                <td className="whitespace-nowrap px-6 py-4 text-sm text-slate-500">{c.cuit}</td>
                <td className="whitespace-nowrap px-6 py-4 text-sm text-slate-500">
                  <span className="inline-flex items-center rounded-full bg-blue-50 px-2 py-1 text-xs font-medium text-blue-700 ring-1 ring-inset ring-blue-700/10">
                    {c.condicionIva}
                  </span>
                </td>
                <td className="whitespace-nowrap px-6 py-4 text-right text-sm font-medium">
                  <div className="flex justify-end gap-2">
                    <button onClick={() => handleEditar(c)} className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-indigo-600" title="Editar">
                      <Edit2 className="h-4 w-4" />
                    </button>
                    <button onClick={() => handleEliminar(c.id)} className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-red-600" title="Eliminar">
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {proveedores.length === 0 && (
              <tr>
                <td colSpan={5} className="px-6 py-8 text-center text-sm text-slate-500">
                  No hay proveedores registrados.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {showModal && (
        <ModalProveedor
          proveedorEditando={proveedorEditando}
          onClose={() => setShowModal(false)}
          onSuccess={() => {
            setShowModal(false);
            fetchproveedores();
          }}
        />
      )}
    </div>
  );
}
