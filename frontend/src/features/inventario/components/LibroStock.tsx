import { useState } from "react";
import { formatearFechaLocal } from "../../../utils/formatters";
import { Loader2 } from "lucide-react";
import { useProductos } from "../../../hooks/useProductos";
import { useMovimientosStock } from "../../../hooks/useMovimientosStock";

export function LibroStock() {
  const [productoId, setProductoId] = useState<number | null>(null);

  const { data: productos = [], isLoading: cargandoProductos } = useProductos();
  const { data: movimientos = [], isLoading: cargandoMovimientos } = useMovimientosStock(productoId);

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6">
      <header className="flex flex-col gap-1">
        
        <p className="text-sm text-slate-500">
          Visualizá el historial de movimientos de inventario de un producto específico.
        </p>
      </header>

      <div className="rounded-lg border border-slate-200 bg-white p-6">
        <label className="mb-2 block text-sm font-medium text-slate-700">Seleccionar Producto</label>
        <select
          value={productoId || ""}
          onChange={(e) => setProductoId(Number(e.target.value))}
          disabled={cargandoProductos}
          className="w-full max-w-md rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
        >
          <option value="" disabled>Seleccione un producto...</option>
          {productos.map((p) => (
            <option key={p.id} value={p.id}>
              {p.nombre}
            </option>
          ))}
        </select>
      </div>

      <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
        {cargandoMovimientos ? (
          <div className="flex h-32 items-center justify-center">
            <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
          </div>
        ) : (
          <table className="w-full min-w-[640px] border-collapse text-left text-sm">
            <thead>
              <tr className="border-b border-slate-200 bg-slate-50 text-xs font-medium text-slate-500">
                <th className="px-4 py-3">Fecha</th>
                <th className="px-4 py-3">Detalle</th>
                <th className="px-4 py-3 text-right">Ingreso</th>
                <th className="px-4 py-3 text-right">Salida</th>
                <th className="px-4 py-3 text-right">Saldo</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200">
              {movimientos.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-8 text-center text-slate-500">
                    {productoId ? "No hay movimientos para este producto." : "Seleccione un producto para ver su historial."}
                  </td>
                </tr>
              ) : (
                movimientos.map((movimiento) => (
                  <tr key={movimiento.id} className="hover:bg-slate-50">
                    <td className="px-4 py-3 text-slate-600 whitespace-nowrap">
                      {formatearFechaLocal(movimiento.fecha)}
                    </td>
                    <td className="px-4 py-3 font-medium text-slate-900">{movimiento.detalle}</td>
                    <td className="px-4 py-3 text-right text-emerald-600 font-medium">
                      {movimiento.ingreso > 0 ? `+${movimiento.ingreso}` : "-"}
                    </td>
                    <td className="px-4 py-3 text-right text-rose-600 font-medium">
                      {movimiento.salida > 0 ? `-${movimiento.salida}` : "-"}
                    </td>
                    <td className="px-4 py-3 text-right font-bold text-slate-900 bg-slate-50/50">
                      {movimiento.saldo}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

