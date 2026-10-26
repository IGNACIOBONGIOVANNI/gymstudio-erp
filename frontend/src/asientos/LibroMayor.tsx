import { useState, useMemo } from "react";
import { Loader2, Search } from "lucide-react";
import { formatearFechaLocal } from "../utils/formatters";
import { useObtenerFacturas } from "../hooks/useObtenerFacturas";
import { useLibroCaja } from "../features/caja/hooks/useLibroCaja";
import { useLibroBanco } from "../features/banco/hooks/useLibroBanco";

const currencyFormatter = new Intl.NumberFormat("es-AR", {
  style: "currency",
  currency: "ARS",
});

function CuentaT({
  title,
  shortTitle,
  debe,
  haber,
}: {
  title: string;
  shortTitle: string;
  debe: { fecha: string; monto: number }[];
  haber: { fecha: string; monto: number }[];
}) {
  const maxRows = Math.max(debe.length, haber.length);
  const rows = Array.from({ length: maxRows });
  const totalDebe = debe.reduce((acc, val) => acc + val.monto, 0);
  const totalHaber = haber.reduce((acc, val) => acc + val.monto, 0);

  return (
    <div className="mx-auto max-w-3xl rounded-xl border border-slate-200/80 bg-white shadow-sm overflow-hidden transition-all">
      <div className="overflow-x-auto w-full">
        <table className="w-full text-sm">
          <thead>
            <tr>
              <th
                colSpan={3}
                className="border-b border-slate-200 bg-slate-50/80 py-4 text-center font-semibold text-slate-800 tracking-wide"
              >
                {title}
              </th>
            </tr>
            <tr className="border-b border-slate-200 bg-slate-50 text-slate-500">
              <th className="w-[45%] py-2.5 px-4 sm:px-6 text-left font-medium">Debe (D)</th>
              <th className="w-[10%] border-x border-slate-200 bg-slate-100 py-2.5 px-2 text-center text-xs font-semibold uppercase tracking-wider text-slate-600">
                {shortTitle}
              </th>
              <th className="w-[45%] py-2.5 px-4 sm:px-6 text-left font-medium">Haber (H)</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((_, i) => (
              <tr
                key={i}
                className="border-b border-slate-100 hover:bg-slate-50/80 transition-colors duration-150"
              >
                <td className="py-2.5 px-4 sm:px-6 text-left text-slate-700">
                  {debe[i] !== undefined ? (
                    <div className="flex justify-between items-center tabular-nums">
                      <span className="text-slate-400 text-xs pr-4">{formatearFechaLocal(debe[i].fecha)}</span>
                      <span className="font-semibold text-slate-800">{currencyFormatter.format(debe[i].monto)}</span>
                    </div>
                  ) : ""}
                </td>
                <td className="border-x border-slate-200 bg-slate-50/30"></td>
                <td className="py-2.5 px-4 sm:px-6 text-left text-slate-700">
                  {haber[i] !== undefined ? (
                    <div className="flex justify-between items-center tabular-nums">
                      <span className="text-slate-400 text-xs pr-4">{formatearFechaLocal(haber[i].fecha)}</span>
                      <span className="font-semibold text-slate-800">{currencyFormatter.format(haber[i].monto)}</span>
                    </div>
                  ) : ""}
                </td>
              </tr>
            ))}
            {maxRows < 2 && (
              <tr className="border-b border-slate-100">
                <td className="py-2.5 px-4 sm:px-6 h-10"></td>
                <td className="border-x border-slate-200 bg-slate-50/30 h-10"></td>
                <td className="py-2.5 px-4 sm:px-6 h-10"></td>
              </tr>
            )}
          </tbody>
          <tfoot>
            <tr className="border-t-2 border-slate-200 bg-slate-50">
              <td className="py-3 px-4 sm:px-6 text-right font-bold text-slate-900 tabular-nums">
                {currencyFormatter.format(totalDebe)}
              </td>
              <td className="border-x border-slate-200 bg-slate-100/50"></td>
              <td className="py-3 px-4 sm:px-6 text-right font-bold text-slate-900 tabular-nums">
                {currencyFormatter.format(totalHaber)}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
}

export function LibroMayor() {
  const { data: facturas = [], isLoading: loadingF, isError: errF } = useObtenerFacturas();
  const { data: resumenCaja, isLoading: loadingC, isError: errC } = useLibroCaja("1");
  const { data: resumenBanco, isLoading: loadingB, isError: errB } = useLibroBanco("1");

  type CuentaKey = "caja" | "banco" | "ventas" | "compras" | "debitoFiscal" | "creditoFiscal";
  const [cuentaSeleccionada, setCuentaSeleccionada] = useState<CuentaKey>("caja");
  const [fechaFiltro, setFechaFiltro] = useState<string>("");

  const mayores = useMemo(() => {
    type MovimientoMayor = { fecha: string; monto: number };
    const cuentas: Record<CuentaKey, { debe: MovimientoMayor[]; haber: MovimientoMayor[] }> = {
      creditoFiscal: { debe: [], haber: [] },
      debitoFiscal: { debe: [], haber: [] },
      compras: { debe: [], haber: [] },
      ventas: { debe: [], haber: [] },
      caja: { debe: [], haber: [] },
      banco: { debe: [], haber: [] },
    };

    if (Array.isArray(facturas)) {
      const fFiltradas = fechaFiltro ? facturas.filter(f => f.fecha && f.fecha.startsWith(fechaFiltro)) : facturas;
      fFiltradas.forEach((f) => {
        const tipoOp = String(f.tipoOperacion || "").trim().toLowerCase();
        const subtotal = Number(f.subtotal) || 0;
        const iva = Number(f.iva) || 0;

        if (tipoOp === "compra") {
          if (subtotal > 0) cuentas.compras.debe.push({ fecha: f.fecha, monto: subtotal });
          if (iva > 0) cuentas.creditoFiscal.debe.push({ fecha: f.fecha, monto: iva });
        }

        if (tipoOp === "venta") {
          if (subtotal > 0) cuentas.ventas.haber.push({ fecha: f.fecha, monto: subtotal });
          if (iva > 0) cuentas.debitoFiscal.haber.push({ fecha: f.fecha, monto: iva });
        }
      });
    }

    const movsCaja = Array.isArray(resumenCaja) ? resumenCaja : (resumenCaja?.listaMovimientos || []);
    if (movsCaja.length > 0) {
      const cFiltrada = fechaFiltro ? movsCaja.filter((m: any) => m?.fecha && String(m.fecha).startsWith(fechaFiltro)) : movsCaja;
      cFiltrada.forEach((m: any) => {
        if (m && m.ingreso != null && Number(m.ingreso) > 0) {
          cuentas.caja.debe.push({ fecha: m.fecha, monto: Number(m.ingreso) });
        }
        if (m && m.egreso != null && Number(m.egreso) > 0) {
          cuentas.caja.haber.push({ fecha: m.fecha, monto: Number(m.egreso) });
        }
      });
    }

    const movsBanco = Array.isArray(resumenBanco) ? resumenBanco : (resumenBanco?.listaMovimientos || []);
    if (movsBanco.length > 0) {
      const bFiltrada = fechaFiltro ? movsBanco.filter((m: any) => m?.fecha && String(m.fecha).startsWith(fechaFiltro)) : movsBanco;
      bFiltrada.forEach((m: any) => {
        if (m && m.ingreso != null && Number(m.ingreso) > 0) {
          cuentas.banco.debe.push({ fecha: m.fecha, monto: Number(m.ingreso) });
        }
        if (m && m.egreso != null && Number(m.egreso) > 0) {
          cuentas.banco.haber.push({ fecha: m.fecha, monto: Number(m.egreso) });
        }
      });
    }

    return cuentas;
  }, [facturas, resumenCaja, resumenBanco, fechaFiltro]);

  const opcionesCuentas: { id: CuentaKey; title: string; short: string }[] = [
    { id: "caja", title: "Mayor de Caja", short: "Caja" },
    { id: "banco", title: "Mayor de Banco", short: "Banco" },
    { id: "ventas", title: "Mayor de Ventas", short: "Ventas" },
    { id: "compras", title: "Mayor de Compras", short: "Compras" },
    { id: "debitoFiscal", title: "Mayor Débito Fiscal", short: "Débito F." },
    { id: "creditoFiscal", title: "Mayor Crédito Fiscal", short: "Crédito F." },
  ];

  if (loadingF || loadingC || loadingB) {
    return (
      <div className="flex min-h-[400px] flex-col items-center justify-center space-y-4 text-slate-500">
        <Loader2 className="h-8 w-8 animate-spin text-slate-400" />
        <p className="text-sm">Calculando Libro Mayor Automático...</p>
      </div>
    );
  }

  if (errF || errC || errB) {
    return (
      <div className="rounded-md bg-red-50 p-4">
        <p className="text-sm font-medium text-red-800">
          Error al cargar los datos para el Libro Mayor.
        </p>
      </div>
    );
  }

  const cuentaActiva = opcionesCuentas.find(c => c.id === cuentaSeleccionada)!;

  return (
    <div className="space-y-6 py-4">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-white p-4 sm:p-5 rounded-xl border border-slate-200/80 shadow-sm mx-auto max-w-3xl">
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
      </div>

      <div className="flex flex-col items-center justify-center space-y-2 mb-8">
        <label htmlFor="selector-cuenta" className="text-xs font-bold text-slate-500 uppercase tracking-wider">
          Seleccione la cuenta a visualizar
        </label>
        <div className="relative w-72 sm:w-80">
          <select
            id="selector-cuenta"
            value={cuentaSeleccionada}
            onChange={(e) => setCuentaSeleccionada(e.target.value as CuentaKey)}
            className="block w-full appearance-none rounded-xl border border-slate-300 bg-white px-4 py-2.5 pr-10 text-sm font-semibold text-slate-800 shadow-sm hover:border-slate-400 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all cursor-pointer"
          >
            {opcionesCuentas.map((opcion) => (
              <option key={opcion.id} value={opcion.id}>
                {opcion.title}
              </option>
            ))}
          </select>
          <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-3 text-slate-400">
            <svg className="h-4 w-4 fill-current" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clipRule="evenodd" />
            </svg>
          </div>
        </div>
      </div>

      <div className="px-2 transition-all duration-300">
        <CuentaT
          title={cuentaActiva.title.toUpperCase()}
          shortTitle={cuentaActiva.short}
          debe={mayores[cuentaActiva.id].debe}
          haber={mayores[cuentaActiva.id].haber}
        />
      </div>
    </div>
  );
}
