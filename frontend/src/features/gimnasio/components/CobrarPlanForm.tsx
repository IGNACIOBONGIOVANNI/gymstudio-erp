import { useState, useEffect } from "react";
import { Loader2, X, Check } from "lucide-react";

interface PlanDto {
  id: string;
  nombre: string;
  precioMensual: number;
  activo: boolean;
}

interface CobrarPlanFormProps {
  socioId: string;
  socioNombre: string;
  onClose: () => void;
}

export function CobrarPlanForm({ socioId, socioNombre, onClose }: CobrarPlanFormProps) {
  const [planes, setPlanes] = useState<PlanDto[]>([]);
  const [selectedPlanId, setSelectedPlanId] = useState("");
  const [condicionPago, setCondicionPago] = useState("1"); // 1 = ContadoCaja, 2 = TransferenciaBanco
  const [isPending, setIsPending] = useState(false);
  const [isLoadingPlanes, setIsLoadingPlanes] = useState(true);

  useEffect(() => {
    const fetchPlanes = async () => {
      try {
        const response = await fetch("/api/planes");
        if (response.ok) {
          const data = await response.json();
          // Filter only active plans
          setPlanes(data);
        }
      } catch (error) {
        console.error("Error fetching planes:", error);
      } finally {
        setIsLoadingPlanes(false);
      }
    };

    fetchPlanes();
  }, []);

  const selectedPlan = planes.find(p => p.id.toString() === selectedPlanId);

  const currencyFormatter = new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "ARS",
  });

  const handleConfirmar = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPlanId) return;

    setIsPending(true);
    
    try {
      const response = await fetch("/api/membresias/asignar-y-cobrar", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          socioId: parseInt(socioId),
          planId: parseInt(selectedPlanId),
          condicionPago: parseInt(condicionPago)
        }),
      });

      if (!response.ok) {
        throw new Error("Error al procesar el cobro");
      }

      onClose();
    } catch (error) {
      console.error(error);
      alert("Hubo un error al procesar el cobro");
    } finally {
      setIsPending(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4 sm:p-0">
      <div className="w-full max-w-md rounded-xl bg-white shadow-xl ring-1 ring-slate-900/5">
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-slate-900">
            Cobrar Cuota - {socioNombre}
          </h2>
          <button
            onClick={onClose}
            className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-500 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleConfirmar} className="p-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">
              Seleccionar Plan
            </label>
            <select
              required
              value={selectedPlanId}
              onChange={(e) => setSelectedPlanId(e.target.value)}
              disabled={isLoadingPlanes}
              className="block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:bg-slate-50"
            >
              <option value="">
                {isLoadingPlanes ? "Cargando planes..." : "Seleccione un plan"}
              </option>
              {planes.map(plan => (
                <option key={plan.id} value={plan.id}>
                  {plan.nombre}
                </option>
              ))}
            </select>
          </div>

          {selectedPlan && (
            <div className="rounded-md bg-slate-50 p-4 border border-slate-100">
              <div className="flex justify-between items-center">
                <span className="text-sm font-medium text-slate-500">Monto a cobrar:</span>
                <span className="text-lg font-semibold text-slate-900">
                  {currencyFormatter.format(selectedPlan.precioMensual)}
                </span>
              </div>
            </div>
          )}

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">
              Condición de Pago
            </label>
            <select
              required
              value={condicionPago}
              onChange={(e) => setCondicionPago(e.target.value)}
              className="block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            >
              <option value="1">Contado Caja</option>
              <option value="2">Transferencia Banco</option>
            </select>
          </div>

          <div className="mt-6 flex justify-end space-x-3 pt-4 border-t border-slate-100">
            <button
              type="button"
              onClick={onClose}
              className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={isPending || !selectedPlanId}
              className="inline-flex items-center justify-center rounded-md bg-marca-600 px-4 py-2 text-sm font-medium text-white hover:bg-marca-700 disabled:opacity-50 transition-colors"
            >
              {isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Check className="mr-2 h-4 w-4" />
              )}
              Confirmar Pago
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
