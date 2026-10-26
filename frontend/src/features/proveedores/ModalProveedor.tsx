import React, { useState, useEffect } from 'react';
import { X } from 'lucide-react';
import toast from 'react-hot-toast';

interface ModalProveedorProps {
  proveedorEditando: any | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function ModalProveedor({ proveedorEditando, onClose, onSuccess }: ModalProveedorProps) {
  const [formData, setFormData] = useState({
    razonSocial: '',
    cuit: '',
    direccion: '',
    localidad: '',
    condicionIva: 'Responsable Inscripto'
  });
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (proveedorEditando) {
      setFormData({
        razonSocial: proveedorEditando.razonSocial || '',
        cuit: proveedorEditando.cuit || '',
        direccion: proveedorEditando.direccion || '',
        localidad: proveedorEditando.localidad || '',
        condicionIva: proveedorEditando.condicionIva || 'Responsable Inscripto'
      });
    }
  }, [proveedorEditando]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    
    const isEdit = !!proveedorEditando;
    
    const url = proveedorEditando 
      ? `/api/proveedores/${proveedorEditando.id}`
      : '/api/proveedores';
      
    const method = isEdit ? 'PUT' : 'POST';

    try {
      const res = await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(formData)
      });
      
      if (res.ok) {
        toast.success(isEdit ? 'proveedor actualizado' : 'proveedor creado');
        onSuccess();
      } else {
        toast.error('Error al guardar el proveedor');
      }
    } catch (error) {
      console.error(error);
      toast.error('Error de red al guardar');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="w-full max-w-md rounded-xl bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-slate-200 px-6 py-4">
          <h3 className="text-lg font-semibold text-slate-900">
            {proveedorEditando ? 'Editar proveedor' : 'Nuevo proveedor'}
          </h3>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-500">
            <X className="h-5 w-5" />
          </button>
        </div>
        
        <form onSubmit={handleSubmit} className="p-6">
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-slate-700">Nombre / Razón Social</label>
              <input required type="text" className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" value={formData.razonSocial} onChange={e => setFormData({...formData, razonSocial: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">CUIT / DNI</label>
              <input required type="text" className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" value={formData.cuit} onChange={e => setFormData({...formData, cuit: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Dirección</label>
              <input required type="text" className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" value={formData.direccion} onChange={e => setFormData({...formData, direccion: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Localidad</label>
              <input required type="text" className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" value={formData.localidad} onChange={e => setFormData({...formData, localidad: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Condición frente al IVA</label>
              <select className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500" value={formData.condicionIva} onChange={e => setFormData({...formData, condicionIva: e.target.value})}>
                <option value="Responsable Inscripto">Responsable Inscripto</option>
                <option value="Monotributo">Monotributo</option>
                <option value="Exento">Exento</option>
                <option value="Consumidor Final">Consumidor Final</option>
              </select>
            </div>
          </div>
          
          <div className="mt-6 flex justify-end gap-3">
            <button type="button" onClick={onClose} className="rounded-md px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancelar</button>
            <button type="submit" disabled={loading} className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50">
              {loading ? 'Guardando...' : (proveedorEditando ? 'Actualizar proveedor' : 'Guardar proveedor')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

