import { useState } from "react";
import { ArrowRightLeft, Loader2, X } from "lucide-react";
import toast from "react-hot-toast";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { mensajeDeError } from "../../api/errores";

interface TransferenciaModalProps {
  origen: "Caja" | "Banco";
  onClose: () => void;
}

/**
 * Transferencia de fondos entre Caja y Banco: registra el egreso en un libro, el
 * ingreso en el otro y un único asiento en el Libro Diario.
 */
export function TransferenciaModal({ origen, onClose }: TransferenciaModalProps) {
  const destino = origen === "Caja" ? "Banco" : "Caja";
  const [fecha, setFecha] = useState(new Date().toISOString().slice(0, 10));
  const [importe, setImporte] = useState("");
  const [detalle, setDetalle] = useState("");
  const queryClient = useQueryClient();

  const transferir = useMutation({
    mutationFn: async () => {
      const res = await fetch("/api/tesoreria/transferencias", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ origen, fecha: new Date(fecha).toISOString(), importe: Number(importe), detalle }),
      });
      if (!res.ok) throw new Error(await mensajeDeError(res, "No se pudo registrar la transferencia"));
    },
    onSuccess: () => {
      ["libro-caja", "libro-banco", "asientos", "mayor"].forEach((k) =>
        queryClient.invalidateQueries({ queryKey: [k] })
      );
      toast.success(`Transferencia de ${origen} a ${destino} registrada`);
      onClose();
    },
    onError: (error: any) => toast.error(error?.message || "No se pudo registrar la transferencia"),
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const monto = Number(importe);
    if (!fecha) return void toast.error("Indicá la fecha de la transferencia");
    if (!Number.isFinite(monto) || monto <= 0) return void toast.error("El importe debe ser un número mayor a cero");
    transferir.mutate();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4 sm:p-0">
      <div className="w-full max-w-md rounded-xl bg-white shadow-xl ring-1 ring-slate-900/5">
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 className="flex items-center gap-2 text-lg font-semibold text-slate-900">
            <ArrowRightLeft className="h-5 w-5 text-slate-500" />
            Transferir de {origen} a {destino}
          </h2>
          <button onClick={onClose} className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-500 transition-colors">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <p className="text-sm text-slate-500">
            {origen === "Caja"
              ? "Por ejemplo, el depósito del efectivo de la caja en la cuenta bancaria."
              : "Por ejemplo, una extracción del banco para tener efectivo en la caja."}{" "}
            Se registra como egreso en el Libro {origen}, ingreso en el Libro {destino} y un asiento en el Libro Diario.
          </p>

          <div>
            <label className="block text-sm font-medium text-slate-700">Fecha</label>
            <input
              type="date"
              required
              value={fecha}
              onChange={(e) => setFecha(e.target.value)}
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700">Importe</label>
            <div className="relative mt-1">
              <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
                <span className="text-slate-500 sm:text-sm">$</span>
              </div>
              <input
                type="number"
                required
                min="0.01"
                step="0.01"
                value={importe}
                onChange={(e) => setImporte(e.target.value)}
                placeholder="0.00"
                className="block w-full rounded-md border border-slate-300 pl-7 pr-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
              />
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700">Detalle (opcional)</label>
            <input
              type="text"
              maxLength={200}
              value={detalle}
              onChange={(e) => setDetalle(e.target.value)}
              placeholder="Ej: Depósito de la recaudación del día"
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
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
              disabled={transferir.isPending}
              className="inline-flex items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              {transferir.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Registrar transferencia
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
