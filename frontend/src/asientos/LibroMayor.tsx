import { useState, useMemo } from "react";
import { Loader2, Search } from "lucide-react";
import { formatearFechaLocal } from "../utils/formatters";
import { useAsientos } from "./hooks/useAsientos";
import { BotonPDF } from "../components/reportes/BotonPDF";

const currencyFormatter = new Intl.NumberFormat("es-AR", {
  style: "currency",
  currency: "ARS",
});

type Movimiento = { fecha: string; monto: number };

/** Una cuenta en T, como en la planilla: D a la izquierda, H a la derecha y la suma de cada lado. */
function CuentaT({ titulo, cuenta, debe, haber }: { titulo: string; cuenta: string; debe: Movimiento[]; haber: Movimiento[] }) {
  const filas = Math.max(debe.length, haber.length, 3);
  const totalDebe = debe.reduce((acc, m) => acc + m.monto, 0);
  const totalHaber = haber.reduce((acc, m) => acc + m.monto, 0);
  const saldo = totalDebe - totalHaber;

  const celda = (m?: Movimiento) => m ? (
    <div className="flex items-baseline justify-between gap-2 tabular-nums">
      <span className="text-[11px] text-slate-400">{formatearFechaLocal(m.fecha)}</span>
      <span className="font-semibold text-slate-800">{currencyFormatter.format(m.monto)}</span>
    </div>
  ) : null;

  return (
    <div className="rounded-xl border border-slate-200/80 bg-white shadow-sm overflow-hidden">
      <table className="w-full text-sm">
        <thead>
          <tr>
            <th colSpan={2} className="border-b border-slate-200 bg-slate-50/80 py-3 text-center text-xs font-bold tracking-wide text-slate-800">
              {titulo}
            </th>
          </tr>
          <tr className="border-b-2 border-slate-700 text-slate-600">
            <th colSpan={2} className="py-2 px-3">
              <div className="flex items-center justify-between">
                <span className="font-semibold">D</span>
                <span className="text-xs font-semibold">{cuenta}</span>
                <span className="font-semibold">H</span>
              </div>
            </th>
          </tr>
        </thead>
        <tbody>
          {Array.from({ length: filas }).map((_, i) => (
            <tr key={i} className="border-b border-slate-100">
              <td className="h-9 w-1/2 py-1.5 px-3 border-r-2 border-slate-700">{celda(debe[i])}</td>
              <td className="w-1/2 py-1.5 px-3">{celda(haber[i])}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr className="border-t-2 border-slate-300 bg-slate-50">
            <td className="py-2.5 px-3 text-right font-bold text-slate-900 tabular-nums border-r-2 border-slate-700">{currencyFormatter.format(totalDebe)}</td>
            <td className="py-2.5 px-3 text-right font-bold text-slate-900 tabular-nums">{currencyFormatter.format(totalHaber)}</td>
          </tr>
          <tr className="bg-slate-50 text-xs text-slate-600">
            <td colSpan={2} className="pb-2.5 px-3 text-center">
              {Math.abs(saldo) < 0.005 ? "Saldo: $ 0,00"
                : `Saldo ${saldo > 0 ? "deudor" : "acreedor"}: ${currencyFormatter.format(Math.abs(saldo))}`}
            </td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}

// Mismas cuentas y en el mismo orden que la hoja "Libro mayor" de la planilla.
// Ids del plan de cuentas (ver PlanDeCuentas.cs en el backend).
const CUENTAS = [
  { id: 105, titulo: "MAYOR CRÉDITO FISCAL", corto: "Crédito Fiscal" },
  { id: 201, titulo: "MAYOR DÉBITO FISCAL", corto: "Débito Fiscal" },
  { id: 501, titulo: "MAYOR DE COMPRAS", corto: "Compras" },
  { id: 104, titulo: "MAYOR DE VENTAS", corto: "Ventas" },
  { id: 101, titulo: "MAYOR CAJA", corto: "Caja" },
  { id: 102, titulo: "MAYOR BANCO", corto: "Banco" },
];
const ESTADO_CONFIRMADO = 1;

export function LibroMayor() {
  const { data: asientos = [], isLoading, isError } = useAsientos("1");
  const [fechaFiltro, setFechaFiltro] = useState<string>("");

  // El Mayor se arma con las líneas de los asientos confirmados del Libro Diario
  // (los anulados no suman), así coincide siempre con el Diario y el Balance.
  const mayores = useMemo(() => {
    const porCuenta: Record<number, { debe: Movimiento[]; haber: Movimiento[] }> =
      Object.fromEntries(CUENTAS.map((c) => [c.id, { debe: [], haber: [] }]));
    asientos
      .filter((a) => a.estado === ESTADO_CONFIRMADO)
      .filter((a) => !fechaFiltro || String(a.fecha).startsWith(fechaFiltro))
      .forEach((a) => {
        a.lineas.forEach((l) => {
          const cuenta = porCuenta[l.cuentaId];
          if (!cuenta) return;
          if (Number(l.debe) > 0) cuenta.debe.push({ fecha: a.fecha, monto: Number(l.debe) });
          if (Number(l.haber) > 0) cuenta.haber.push({ fecha: a.fecha, monto: Number(l.haber) });
        });
      });
    return porCuenta;
  }, [asientos, fechaFiltro]);

  if (isLoading) {
    return (
      <div className="flex min-h-[400px] flex-col items-center justify-center space-y-4 text-slate-500">
        <Loader2 className="h-8 w-8 animate-spin text-slate-400" />
        <p className="text-sm">Armando el Libro Mayor...</p>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="rounded-md bg-red-50 p-4">
        <p className="text-sm font-medium text-red-800">Error al cargar los datos para el Libro Mayor.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-white p-4 sm:p-5 rounded-xl border border-slate-200/80 shadow-sm">
        <div className="flex items-center gap-3 flex-wrap">
          <div className="p-2 bg-slate-100 rounded-lg text-slate-500 shrink-0">
            <Search className="h-4 w-4" />
          </div>
          <label htmlFor="fechaFiltro" className="text-sm font-medium text-slate-700 shrink-0">
            Filtrar por fecha:
          </label>
          <div className="flex items-center gap-2">
            <input
              id="fechaFiltro"
              type="date"
              value={fechaFiltro}
              onChange={(e) => setFechaFiltro(e.target.value)}
              className="border border-slate-300 rounded-lg px-3 py-1.5 text-sm text-slate-800 bg-white shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-colors"
            />
            {fechaFiltro && (
              <button
                onClick={() => setFechaFiltro("")}
                className="text-xs font-semibold px-2.5 py-1.5 rounded-md bg-slate-100 hover:bg-slate-200 text-slate-600 hover:text-slate-900 transition-colors"
              >
                Limpiar
              </button>
            )}
          </div>
        </div>
        <BotonPDF tipo="mayor" fecha={fechaFiltro} />
      </div>

      <div className="grid grid-cols-1 gap-5 md:grid-cols-2 xl:grid-cols-3">
        {CUENTAS.map((c) => (
          <CuentaT key={c.id} titulo={c.titulo} cuenta={c.corto} debe={mayores[c.id].debe} haber={mayores[c.id].haber} />
        ))}
      </div>
    </div>
  );
}
