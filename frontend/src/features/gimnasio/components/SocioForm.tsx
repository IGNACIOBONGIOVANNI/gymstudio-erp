import { useState } from "react";
import { Loader2, Plus, X } from "lucide-react";

export interface SocioEditable {
  id: string | number;
  nombre: string;
  apellido: string;
  dni: string;
  telefono: string;
}

interface SocioFormProps {
  onClose: () => void;
  // Si viene un socio, el formulario edita (PUT) en lugar de crear (POST).
  socioAEditar?: SocioEditable | null;
}

export function SocioForm({ onClose, socioAEditar }: SocioFormProps) {
  const esEdicion = Boolean(socioAEditar);
  const [nombre, setNombre] = useState(socioAEditar?.nombre ?? "");
  const [apellido, setApellido] = useState(socioAEditar?.apellido ?? "");
  const [dni, setDni] = useState(socioAEditar?.dni ?? "");
  const [telefono, setTelefono] = useState(socioAEditar?.telefono ?? "");
  const [isPending, setIsPending] = useState(false);

  const handleLimpiar = () => {
    setNombre("");
    setApellido("");
    setDni("");
    setTelefono("");
  };

  const handleGuardarSocio = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsPending(true);
    
    try {
      const response = await fetch(esEdicion ? `/api/socios/${socioAEditar!.id}` : "/api/socios", {
        method: esEdicion ? "PUT" : "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          nombre,
          apellido,
          dni,
          telefono
        }),
      });

      if (!response.ok) {
        throw new Error("Error al guardar el socio");
      }

      onClose();
    } catch (error) {
      console.error(error);
      alert("Hubo un error al guardar el socio");
    } finally {
      setIsPending(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4 sm:p-0">
      <div className="w-full max-w-sm rounded-xl bg-white shadow-xl ring-1 ring-slate-900/5">
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-slate-900">
            {esEdicion ? "Editar Socio" : "Nuevo Socio"}
          </h2>
          <button
            onClick={onClose}
            className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-500 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleGuardarSocio} className="p-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700">Nombre</label>
            <input
              type="text"
              required
              placeholder="Ej: Juan"
              value={nombre}
              onChange={(e) => setNombre(e.target.value)}
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700">Apellido</label>
            <input
              type="text"
              required
              placeholder="Ej: Pérez"
              value={apellido}
              onChange={(e) => setApellido(e.target.value)}
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700">DNI</label>
            <input
              type="text"
              required
              placeholder="Ej: 12345678"
              value={dni}
              onChange={(e) => setDni(e.target.value)}
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700">Teléfono</label>
            <input
              type="text"
              placeholder="Ej: 1122334455"
              value={telefono}
              onChange={(e) => setTelefono(e.target.value)}
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
          </div>

          <div className="mt-6 flex justify-end space-x-3 pt-4 border-t border-slate-100">
            <button
              type="button"
              onClick={handleLimpiar}
              className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Limpiar
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="inline-flex items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              <Plus className="mr-2 h-4 w-4" />
              Guardar Socio
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
