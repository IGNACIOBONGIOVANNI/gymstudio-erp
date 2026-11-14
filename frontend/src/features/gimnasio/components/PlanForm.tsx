import { useState } from "react";
import { Loader2, Plus, X } from "lucide-react";
import toast from 'react-hot-toast';
import { mensajeDeError, ERROR_DE_CONEXION } from '../../../api/errores';
import { numeroDecimal, soloDigitos } from '../../../utils/entradaNumerica';

export interface PlanEditable {
  id: string | number;
  nombre: string;
  precioMensual: number;
  duracionMeses: number;
}

interface PlanFormProps {
  onClose: () => void;
  // Si viene un plan, el formulario edita (PUT) en lugar de crear (POST).
  planAEditar?: PlanEditable | null;
}

export function PlanForm({ onClose, planAEditar }: PlanFormProps) {
  const esEdicion = Boolean(planAEditar);
  const [nombre, setNombre] = useState(planAEditar?.nombre ?? "");
  const [precioMensual, setPrecioMensual] = useState(
    planAEditar ? String(planAEditar.precioMensual) : ""
  );
  const [duracionMeses, setDuracionMeses] = useState(
    String(planAEditar?.duracionMeses || 1)
  );
  const [isPending, setIsPending] = useState(false);

  const handleLimpiar = () => {
    setNombre("");
    setPrecioMensual("");
    setDuracionMeses("1"); // 🔥 LIMPIAR TAMBIÉN RESETEA A 1
  };

  const handleGuardarPlan = async (e: React.FormEvent) => {
    e.preventDefault();
    const precio = Number(precioMensual);
    const meses = Number(duracionMeses);
    if (!nombre.trim()) return void toast.error("Ingresá el nombre del plan");
    if (precioMensual.trim() === "" || !Number.isFinite(precio) || precio < 0)
      return void toast.error("Ingresá un precio válido (0 o más)");
    if (!Number.isInteger(meses) || meses < 1)
      return void toast.error("La duración debe ser un número entero de meses (1 o más)");
    setIsPending(true);

    try {
      const response = await fetch(esEdicion ? `/api/planes/${planAEditar!.id}` : "/api/planes", {
        method: esEdicion ? "PUT" : "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          nombre,
          precioMensual: parseFloat(precioMensual),
          duracionMeses: parseInt(duracionMeses, 10),
        }),
      });

      if (!response.ok) {
        throw new Error(await mensajeDeError(response, "Error al guardar el plan"));
      }

      onClose();
    } catch (error: any) {
      toast.error(error instanceof TypeError ? ERROR_DE_CONEXION : error?.message || "Error al guardar el plan");
    } finally {
      setIsPending(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4 sm:p-0">
      <div className="w-full max-w-sm rounded-xl bg-white shadow-xl ring-1 ring-slate-900/5">
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-slate-900">
            {esEdicion ? "Editar Plan" : "Nuevo Plan"}
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
              type="text"
              inputMode="numeric"
              autoComplete="off"
              required
              placeholder="Ej: 1"
              value={duracionMeses}
              onChange={(e) => setDuracionMeses(soloDigitos(e.target.value, 3))}
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
                type="text"
                inputMode="decimal"
                autoComplete="off"
                required
                placeholder="0.00"
                value={precioMensual}
                onChange={(e) => setPrecioMensual(numeroDecimal(e.target.value))}
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
              className="inline-flex items-center justify-center rounded-md bg-marca-600 px-4 py-2 text-sm font-medium text-white hover:bg-marca-700 disabled:opacity-50"
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