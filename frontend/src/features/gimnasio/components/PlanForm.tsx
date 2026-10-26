import { useState } from "react";
import { Loader2, Plus, X } from "lucide-react";

interface PlanFormProps {
  onClose: () => void;
}

export function PlanForm({ onClose }: PlanFormProps) {
  const [nombre, setNombre] = useState("");
  const [precioMensual, setPrecioMensual] = useState("");
  // 🔥 NUEVO ESTADO PARA LA DURACIÓN (Arranca en 1 mes por defecto)
  const [duracionMeses, setDuracionMeses] = useState("1");
  const [isPending, setIsPending] = useState(false);

  const handleLimpiar = () => {
    setNombre("");
    setPrecioMensual("");
    setDuracionMeses("1"); // 🔥 LIMPIAR TAMBIÉN RESETEA A 1
  };

  const handleGuardarPlan = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsPending(true);
    
    try {
      const response = await fetch("/api/planes", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          nombre,
          precioMensual: parseFloat(precioMensual),
          duracionMeses: parseInt(duracionMeses, 10), // 🔥 LO MANDAMOS AL BACKEND
        }),
      });

      if (!response.ok) {
        throw new Error("Error al guardar el plan");
      }

      onClose();
    } catch (error) {
      console.error(error);
      alert("Hubo un error al guardar el plan");
    } finally {
      setIsPending(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4 sm:p-0">
      <div className="w-full max-w-sm rounded-xl bg-white shadow-xl ring-1 ring-slate-900/5">
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-slate-900">
            Nuevo Plan
          </h2>
          <button
            onClick={onClose}
            className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-500 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleGuardarPlan} className="p-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700">Nombre del Plan</label>
            <input
              type="text"
              required
              placeholder="Ej: Pase Libre"
              value={nombre}
              onChange={(e) => setNombre(e.target.value)}
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
          </div>

          {/* 🔥 NUEVO CAMPO: DURACIÓN EN MESES */}
          <div>
            <label className="block text-sm font-medium text-slate-700">Duración (Meses)</label>
            <input
              type="number"
              required
              min="1"
              step="1"
              placeholder="Ej: 1"
              value={duracionMeses}
              onChange={(e) => setDuracionMeses(e.target.value)}
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700">Precio</label>
            <div className="relative mt-1 rounded-md shadow-sm">
              <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
                <span className="text-slate-500 sm:text-sm">$</span>
              </div>
              <input
                type="number"
                required
                min="0"
                step="0.01"
                placeholder="0.00"
                value={precioMensual}
                onChange={(e) => setPrecioMensual(e.target.value)}
                className="block w-full rounded-md border border-slate-300 pl-7 pr-12 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
              />
              <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-3">
                <span className="text-slate-500 sm:text-sm">ARS</span>
              </div>
            </div>
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
              Guardar Plan
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}