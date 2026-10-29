import { useState } from "react";
import { Loader2, Plus, Trash2, Pencil, Search, ArrowRightLeft } from "lucide-react";
import { TransferenciaModal } from "../../tesoreria/TransferenciaModal";
import { useLibroBanco } from "../hooks/useLibroBanco";
import { formatearFechaLocal } from "../../../utils/formatters";
import { MovimientoBancoForm } from "./MovimientoBancoForm";
import { useEliminarMovimientoBanco } from "../hooks/useEliminarMovimientoBanco";
import { BotonPDF } from "../../../components/reportes/BotonPDF";

const currencyFormatter = new Intl.NumberFormat("es-AR", {
  style: "currency",
  currency: "ARS",
});

interface LibroBancoProps {
  clienteId?: string;
}

export function LibroBanco({ clienteId = "1" }: LibroBancoProps) {
  const { data: resumen, isLoading, isError, error } = useLibroBanco(clienteId);
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isTransferenciaOpen, setIsTransferenciaOpen] = useState(false);
  const [fechaFiltro, setFechaFiltro] = useState("");

  const { mutate: eliminarMovimiento } = useEliminarMovimientoBanco();
  const [movimientoToDelete, setMovimientoToDelete] = useState<number | null>(null);
  const [movimientoAEditar, setMovimientoAEditar] = useState<any | null>(null);

  if (isLoading) {
    return (
      <div className="flex min-h-[400px] flex-col items-center justify-center space-y-4 text-slate-500">
        <Loader2 className="h-8 w-8 animate-spin text-slate-400" />
        <p>Cargando Libro Banco...</p>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="flex min-h-[400px] flex-col items-center justify-center space-y-4">
        <div className="rounded-md bg-red-50 p-4">
          <p className="text-sm font-medium text-red-800">
            Error al cargar el banco: {error.message}
          </p>
        </div>
      </div>
    );
  }

  if (!resumen) {
    return null;
  }

  const listaBase = Array.isArray(resumen) ? resumen : (resumen?.listaMovimientos || []);
  const listaFiltrada = fechaFiltro
    ? listaBase.filter((m: any) => m?.fecha && String(m.fecha).startsWith(fechaFiltro))
    : listaBase;

  const ingresos = listaFiltrada.filter((m: any) => m && m.ingreso != null);
  const egresos = listaFiltrada.filter((m: any) => m && m.egreso != null);

  const totalIngresos = ingresos.reduce((sum: number, m: any) => sum + (Number(m.ingreso) || 0), 0);
  const totalEgresos = egresos.reduce((sum: number, m: any) => sum + (Number(m.egreso) || 0), 0);
  const saldoAnterior = Number(resumen?.saldoAnterior) || 0;
  const saldoActual = saldoAnterior + totalIngresos - totalEgresos;

  // Orden cronológico y saldo corrido desde el saldo anterior.
  const ordenados = [...listaFiltrada].sort((a: any, b: any) =>
    String(a.fecha).localeCompare(String(b.fecha)) || (Number(a.id) || 0) - (Number(b.id) || 0));
  let acumulado = saldoAnterior;
  const conSaldo = ordenados.map((mov: any) => {
    acumulado += (Number(mov.ingreso) || 0) - (Number(mov.egreso) || 0);
    return { mov, saldo: acumulado };
  });

  const handleEditClick = (movimiento: any) => {
    setMovimientoAEditar(movimiento);
    setIsFormOpen(true);
  };

  const handleCloseForm = () => {
    setIsFormOpen(false);
    setMovimientoAEditar(null);
  };

  return (
    <div className="space-y-6">

      <div className="flex flex-col sm:flex-row sm:flex-wrap sm:items-center justify-between gap-4 bg-white p-4 sm:p-5 rounded-xl border border-slate-200/80 shadow-sm">
        <div className="flex items-center gap-3 flex-wrap">
          <div className="p-2 bg-slate-100 rounded-lg text-slate-500 shrink-0">
            <Search className="h-4 w-4" />
          </div>
          <label htmlFor="fechaFiltroBanco" className="text-sm font-medium text-slate-700 shrink-0">
            Filtrar por fecha:
          </label>
          <div className="flex items-center gap-2">
            <input
              id="fechaFiltroBanco"
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
        <div className="flex flex-wrap gap-2 shrink-0">
          <BotonPDF tipo="banco" fecha={fechaFiltro} />
        <button
          onClick={() => setIsTransferenciaOpen(true)}
          className="inline-flex items-center justify-center gap-2 rounded-lg border border-slate-300 bg-white px-4 py-2 min-h-[40px] text-sm font-semibold text-slate-700 shadow-sm transition-all hover:bg-slate-50 active:scale-95 shrink-0"
        >
          <ArrowRightLeft className="h-4 w-4" />
          Transferir a Caja
        </button>
        <button
          onClick={() => setIsFormOpen(true)}
          className="inline-flex items-center justify-center gap-2 rounded-lg bg-marca-600 px-4 py-2 min-h-[40px] text-sm font-semibold text-white shadow-sm transition-all hover:bg-marca-700 active:scale-95 focus:outline-none focus:ring-2 focus:ring-marca-500 focus:ring-offset-2 shrink-0"
        >
          <Plus className="h-4 w-4" />
          Nuevo Movimiento
        </button>
        </div>
      </div>

      {/* Como la hoja "Libro Banco": una sola tabla en orden, con el saldo después de cada movimiento. */}
      <div className="rounded-xl border border-slate-200/80 bg-white shadow-sm overflow-hidden">
        <div className="overflow-x-auto w-full">
          <table className="w-full min-w-[720px] text-sm">
            <thead className="bg-slate-50/50">
              <tr className="border-b border-slate-200">
                <th className="h-10 px-4 text-left font-medium text-slate-500">Fecha</th>
                <th className="h-10 min-w-[260px] px-4 text-left font-medium text-slate-500">Detalle</th>
                <th className="h-10 px-4 text-right font-medium text-slate-500 whitespace-nowrap">Depósito</th>
                <th className="h-10 px-4 text-right font-medium text-slate-500 whitespace-nowrap">Cheque / Transf.</th>
                <th className="h-10 px-4 text-right font-medium text-slate-500 whitespace-nowrap">Saldo</th>
                <th className="h-10 px-4 w-28"></th>
              </tr>
            </thead>
            <tbody>
              <tr className="border-b border-slate-100 bg-slate-50/40">
                <td className="p-3 px-4" />
                <td className="p-3 px-4 font-medium text-slate-600">Saldo anterior</td>
                <td colSpan={2} />
                <td className="p-3 px-4 text-right font-semibold tabular-nums text-slate-800">{currencyFormatter.format(saldoAnterior)}</td>
                <td />
              </tr>
              {conSaldo.length === 0 ? (
                <tr>
                  <td colSpan={6} className="p-6 text-center text-slate-500">Sin movimientos</td>
                </tr>
              ) : conSaldo.map(({ mov, saldo }: any, idx: number) => (
                <tr key={`ban-${mov.id ?? idx}`} className="border-b border-slate-100 hover:bg-slate-50/80 transition-colors duration-150">
                  <td className="p-3 px-4 text-slate-500 whitespace-nowrap tabular-nums">{formatearFechaLocal(mov.fecha)}</td>
                  <td className="p-3 px-4 text-slate-700">{mov.detalle}</td>
                  <td className="p-3 px-4 text-right font-semibold text-slate-800 tabular-nums whitespace-nowrap">
                    {mov.ingreso != null ? currencyFormatter.format(mov.ingreso) : ""}
                  </td>
                  <td className="p-3 px-4 text-right font-semibold text-slate-800 tabular-nums whitespace-nowrap">
                    {mov.egreso != null ? currencyFormatter.format(mov.egreso) : ""}
                  </td>
                  <td className={`p-3 px-4 text-right font-semibold tabular-nums whitespace-nowrap ${saldo < 0 ? "text-red-600" : "text-slate-900"}`}>
                    {currencyFormatter.format(saldo)}
                  </td>
                  <td className="p-3 px-4">
                    <div className="flex justify-end gap-1">
                      {mov.facturaId ? (
                        <span
                          className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border bg-slate-50 text-slate-500 border-slate-200 whitespace-nowrap"
                          title="Generado por una factura: se corrige editando o anulando la factura"
                        >
                          Factura
                        </span>
                      ) : (
                        <>
                          {mov.esTransferencia ? (
                            <span
                              className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border bg-slate-50 text-slate-600 border-slate-300 whitespace-nowrap"
                              title="Transferencia entre Caja y Banco: no se edita; si hay un error, eliminala y cargala de nuevo"
                            >
                              Transferencia
                            </span>
                          ) : (
                            <button
                              onClick={() => handleEditClick(mov)}
                              className="rounded p-1.5 text-slate-400 hover:bg-blue-50 hover:text-blue-600 transition-colors"
                              title="Editar movimiento"
                            >
                              <Pencil className="h-4 w-4" />
                            </button>
                          )}
                          <button
                            onClick={() => setMovimientoToDelete(mov.id || mov.Id)}
                            className="rounded p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600 transition-colors"
                            title="Eliminar movimiento"
                          >
                            <Trash2 className="h-4 w-4" />
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t-2 border-slate-300 bg-slate-50 font-bold text-slate-900">
                <td className="p-3 px-4" />
                <td className="p-3 px-4">TOTALES / SALDO</td>
                <td className="p-3 px-4 text-right tabular-nums">{currencyFormatter.format(totalIngresos)}</td>
                <td className="p-3 px-4 text-right tabular-nums">{currencyFormatter.format(totalEgresos)}</td>
                <td className={`p-3 px-4 text-right tabular-nums ${saldoActual < 0 ? "text-red-600" : ""}`}>{currencyFormatter.format(saldoActual)}</td>
                <td />
              </tr>
            </tfoot>
          </table>
        </div>
      </div>

      {/* Modal Eliminar */}
      {movimientoToDelete !== null && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm transition-opacity">
          <div className="w-[95%] max-w-md mx-auto rounded-xl bg-white p-6 shadow-xl">
            <div className="flex items-center justify-center mb-4 h-12 w-12 rounded-full bg-red-100 mx-auto">
              <Trash2 className="h-6 w-6 text-red-600" />
            </div>
            <h3 className="text-center text-lg font-semibold text-slate-900">Eliminar movimiento</h3>
            <p className="mt-2 text-center text-sm text-slate-500">
              ¿Estás seguro de que querés eliminar este movimiento? El saldo se recalculará.
            </p>
            <div className="mt-6 flex justify-center space-x-3">
              <button onClick={() => setMovimientoToDelete(null)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 transition-colors">
                Cancelar
              </button>
              <button onClick={() => { eliminarMovimiento(movimientoToDelete); setMovimientoToDelete(null); }} className="rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 transition-colors shadow-sm">
                Sí, eliminar
              </button>
            </div>
          </div>
        </div>
      )}

      {isTransferenciaOpen && (
        <TransferenciaModal origen="Banco" onClose={() => setIsTransferenciaOpen(false)} />
      )}

      {isFormOpen && (
        <MovimientoBancoForm
          clienteId={clienteId}
          movimientoAEditar={movimientoAEditar}
          onClose={handleCloseForm}
        />
      )}
    </div>
  );
}
