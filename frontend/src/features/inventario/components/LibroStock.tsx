import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Loader2, Search } from "lucide-react";
import { formatearFechaLocal } from "../../../utils/formatters";
import { BotonPDF } from "../../../components/reportes/BotonPDF";

interface MovimientoStock { fecha: string; detalle: string; ingreso: number; salida: number; saldo: number }
interface FichaStock {
  producto: string;
  precioUnitario: number;
  precioVenta: number;
  saldoAnterior: number;
  saldoFinal: number;
  movimientos: MovimientoStock[];
}

const moneda = new Intl.NumberFormat("es-AR", { style: "currency", currency: "ARS" });

/** Libro Stock como en la planilla: una ficha por producto con ingresos, salidas y saldo. */
export function LibroStock() {
  const [busqueda, setBusqueda] = useState("");
  // La clave empieza con "productos": se refresca sola cuando una factura mueve stock.
  const { data: fichas = [], isLoading, isError } = useQuery<FichaStock[]>({
    queryKey: ["productos", "libro-stock"],
    queryFn: async () => {
      const res = await fetch("/api/reportes/generar?tipo=stock");
      if (!res.ok) throw new Error("No se pudo cargar el Libro Stock");
      return res.json();
    },
  });

  const visibles = useMemo(() => {
    const texto = busqueda.trim().toLocaleLowerCase("es");
    return texto ? fichas.filter((f) => f.producto.toLocaleLowerCase("es").includes(texto)) : fichas;
  }, [fichas, busqueda]);

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-white p-4 sm:p-5 rounded-xl border border-slate-200/80 shadow-sm">
        <div className="flex items-center gap-3 flex-wrap">
          <div className="p-2 bg-slate-100 rounded-lg text-slate-500 shrink-0">
            <Search className="h-4 w-4" />
          </div>
          <label htmlFor="buscarProducto" className="text-sm font-medium text-slate-700 shrink-0">Buscar producto:</label>
          <input
            id="buscarProducto"
            value={busqueda}
            onChange={(e) => setBusqueda(e.target.value)}
            placeholder="Martillo, tornillo..."
            className="w-56 border border-slate-300 rounded-lg px-3 py-1.5 text-sm text-slate-800 bg-white shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
        <BotonPDF tipo="stock" />
      </div>

      {isLoading ? (
        <div className="flex h-40 items-center justify-center">
          <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
        </div>
      ) : isError ? (
        <p className="rounded-md bg-red-50 p-4 text-sm font-medium text-red-800">No se pudo cargar el Libro Stock.</p>
      ) : visibles.length === 0 ? (
        <p className="rounded-xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">
          {busqueda ? "Ningún producto coincide con la búsqueda." : "Todavía no hay productos con movimientos de stock."}
        </p>
      ) : (
        <div className="grid grid-cols-1 gap-5 2xl:grid-cols-2">
          {visibles.map((f) => (
            <div key={f.producto} className="rounded-xl border border-slate-200/80 bg-white shadow-sm overflow-hidden">
              <div className="flex items-baseline justify-between gap-3 border-b border-slate-200 bg-slate-50/80 px-4 py-3">
                <h3 className="text-sm font-bold text-slate-900">Producto: {f.producto}</h3>
                <span className="text-xs text-slate-500">Stock actual: <strong className="text-slate-900">{f.saldoFinal}</strong></span>
              </div>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[640px] text-sm">
                  <thead>
                    <tr className="border-b border-slate-200 text-xs text-slate-500">
                      <th className="px-3 py-2 text-left font-medium">Fecha</th>
                      <th className="px-3 py-2 text-left font-medium">Detalle</th>
                      <th className="px-3 py-2 text-right font-medium">Ingreso</th>
                      <th className="px-3 py-2 text-right font-medium">Salida</th>
                      <th className="px-3 py-2 text-right font-medium">Saldo</th>
                      <th className="px-3 py-2 text-right font-medium">P. Unitario</th>
                      <th className="px-3 py-2 text-right font-medium">Precio venta</th>
                    </tr>
                  </thead>
                  <tbody>
                    {f.saldoAnterior !== 0 && (
                      <tr className="border-b border-slate-100 text-slate-500">
                        <td className="px-3 py-2" />
                        <td className="px-3 py-2">Saldo anterior</td>
                        <td colSpan={2} />
                        <td className="px-3 py-2 text-right font-semibold tabular-nums text-slate-800">{f.saldoAnterior}</td>
                        <td colSpan={2} />
                      </tr>
                    )}
                    {f.movimientos.map((m, i) => (
                      <tr key={i} className="border-b border-slate-100 hover:bg-slate-50/80">
                        <td className="px-3 py-2 whitespace-nowrap tabular-nums text-slate-500">{formatearFechaLocal(m.fecha)}</td>
                        <td className="px-3 py-2 text-slate-700">{m.detalle}</td>
                        <td className="px-3 py-2 text-right tabular-nums">{m.ingreso || ""}</td>
                        <td className="px-3 py-2 text-right tabular-nums">{m.salida || ""}</td>
                        <td className="px-3 py-2 text-right font-semibold tabular-nums text-slate-900">{m.saldo}</td>
                        <td className="px-3 py-2 text-right tabular-nums text-slate-600">{moneda.format(f.precioUnitario)}</td>
                        <td className="px-3 py-2 text-right tabular-nums text-slate-600">{f.precioVenta ? moneda.format(f.precioVenta) : "-"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
