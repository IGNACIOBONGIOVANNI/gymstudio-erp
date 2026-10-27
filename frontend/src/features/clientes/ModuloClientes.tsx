import { useState, useEffect } from 'react';
import { Plus, Edit2, Trash2 } from 'lucide-react';
import { ModalCliente } from './ModalCliente';
import toast from 'react-hot-toast';
import { mensajeDeError, ERROR_DE_CONEXION } from '../../api/errores';

export default function ModuloClientes() {
  const [clientes, setClientes] = useState<any[]>([]);
  const [showModal, setShowModal] = useState(false);
  const [clienteEditando, setClienteEditando] = useState<any | null>(null);

  const fetchClientes = () => {
    fetch('/api/clientes')
      .then(res => res.json())
      .then(data => setClientes(data));
  };

  useEffect(() => {
    fetchClientes();
  }, []);

  const handleEliminar = async (id: number) => {
    if (!window.confirm('¿Dar de baja este cliente?\n\nDeja de aparecer en las listas, pero sus facturas se conservan en los libros. Si lo volvés a cargar con el mismo CUIT, se reactiva.')) return;

    try {
      const res = await fetch(`/api/clientes/${id}`, {
        method: 'DELETE'
      });
      if (res.ok) {
        setClientes(clientes.filter(c => c.id !== id));
        toast.success('Cliente dado de baja: sus facturas se conservan');
      } else {
        toast.error(await mensajeDeError(res, 'Error al eliminar el cliente'));
      }
    } catch (error) {
      console.error(error);
      toast.error(ERROR_DE_CONEXION);
    }
  };

  const handleEditar = (cliente: any) => {
    setClienteEditando(cliente);
    setShowModal(true);
  };

  const handleNuevo = () => {
    setClienteEditando(null);
    setShowModal(true);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between border-b border-slate-200 pb-4">
        <p className="text-sm text-slate-500">Empresas y personas a las que les facturás.</p>
        <button
          onClick={handleNuevo}
          className="inline-flex items-center gap-2 rounded-md bg-marca-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-marca-700"
        >
          <Plus className="h-4 w-4" />
          Nuevo Cliente
        </button>
      </div>

      <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
        <table className="w-full divide-y divide-slate-200">
          <thead className="bg-slate-50">
            <tr>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500">ID</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500">Razón Social</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500">CUIT / DNI</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500">Condición IVA</th>
              <th className="px-6 py-3 text-right text-xs font-medium text-slate-500">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 bg-white">
            {clientes.map(c => (
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
                    <button onClick={() => handleEliminar(c.id)} className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-red-600" title="Dar de baja">
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {clientes.length === 0 && (
              <tr>
                <td colSpan={5} className="px-6 py-8 text-center text-sm text-slate-500">
                  No hay clientes registrados.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {showModal && (
        <ModalCliente
          clienteEditando={clienteEditando}
          onClose={() => setShowModal(false)}
          onSuccess={() => {
            setShowModal(false);
            fetchClientes();
          }}
        />
      )}
    </div>
  );
}