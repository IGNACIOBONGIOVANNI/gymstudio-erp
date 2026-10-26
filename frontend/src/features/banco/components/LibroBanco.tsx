import { useState } from "react";
import { Loader2, Plus, Trash2, Pencil, Search } from "lucide-react";
import { useLibroBanco } from "../hooks/useLibroBanco";
import { MovimientoBancoForm } from "./MovimientoBancoForm";
import { useEliminarMovimientoBanco } from "../hooks/useEliminarMovimientoBanco";

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

      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-white p-4 sm:p-5 rounded-xl border border-slate-200/80 shadow-sm">
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
        <button
          onClick={() => setIsFormOpen(true)}
          className="inline-flex items-center justify-center gap-2 rounded-lg bg-slate-900 px-4 py-2 min-h-[40px] text-sm font-semibold text-white shadow-sm transition-all hover:bg-slate-800 active:scale-95 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2 shrink-0"
        >
          <Plus className="h-4 w-4" />
          Nuevo Movimiento
        </button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        
        {/* Columna Izquierda: Ingresos */}
        <div className="space-y-4">
          <div className="rounded-xl border border-slate-200/80 bg-white shadow-sm overflow-hidden">
            <div className="bg-slate-50 px-4 py-3 border-b border-slate-200">
              <h3 className="text-sm font-bold text-slate-800">Ingresos (Cobros y Transferencias)</h3>
            </div>
            <div className="overflow-x-auto w-full">
              <table className="w-full text-sm">
                <thead className="bg-slate-50/50">
                  <tr className="border-b border-slate-200">
                    <th className="h-10 px-4 text-left font-medium text-slate-500">Detalle</th>
                    <th className="h-10 px-4 text-right font-medium text-slate-500">Importe</th>
                    <th className="h-10 px-4 w-20"></th>
                  </tr>
                </thead>
                <tbody>
                  {ingresos.length === 0 ? (
                    <tr>
                      <td colSpan={3} className="p-4 text-center text-slate-500">Sin ingresos</td>
                    </tr>
                  ) : (
                    ingresos.map((mov: any, idx: number) => (
                      <tr key={`ing-${idx}`} className="border-b border-slate-100 hover:bg-slate-50/80 transition-colors duration-150">
                        <td className="p-3 px-4 text-slate-700">{mov.detalle}</td>
                        <td className="p-3 px-4 text-right font-semibold text-slate-800 tabular-nums">
                          {currencyFormatter.format(mov.ingreso)}
                        </td>
                        <td className="p-3 px-4 flex justify-end space-x-1">
                          <button
                            onClick={() => handleEditClick(mov)}
                            className="rounded p-1.5 text-slate-400 hover:bg-blue-50 hover:text-blue-600 transition-colors"
                            title="Editar movimiento"
                          >
                            <Pencil className="h-4 w-4" />
                          </button>
                          <button
                            onClick={() => setMovimientoToDelete(mov.id || mov.Id)}
                            className="rounded p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600 transition-colors"
                            title="Eliminar movimiento"
                          >
                            <Trash2 className="h-4 w-4" />
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <div className="rounded-xl border border-slate-200/80 bg-white p-6 shadow-sm">
            <h3 className="mb-4 text-lg font-bold text-slate-900">Resumen de Banco</h3>
            <div className="space-y-3 text-sm">
              <div className="flex justify-between items-center text-slate-600">
                <span>Saldo anterior:</span>
                <span className="font-semibold text-slate-800 tabular-nums">{currencyFormatter.format(saldoAnterior)}</span>
              </div>
              <div className="flex justify-between items-center text-slate-600">
                <span>Total Ingresos:</span>
                <span className="font-semibold text-green-600 tabular-nums">{currencyFormatter.format(totalIngresos)}</span>
              </div>
              <div className="flex justify-between items-center text-slate-600">
                <span>Total Egresos:</span>
                <span className="font-semibold text-red-600 tabular-nums">-{currencyFormatter.format(totalEgresos)}</span>
              </div>
              <div className="pt-3 border-t border-slate-100 flex justify-between items-center">
                <span className="font-bold text-slate-900 text-base">Saldo actual:</span>
                <span className="font-bold text-slate-900 text-lg tabular-nums">{currencyFormatter.format(saldoActual)}</span>
              </div>
            </div>
          </div>
        </div>

        {/* Columna Derecha: Egresos */}
        <div className="space-y-4">
          <div className="rounded-xl border border-slate-200/80 bg-white shadow-sm overflow-hidden">
            <div className="bg-slate-50 px-4 py-3 border-b border-slate-200">
              <h3 className="text-sm font-bold text-slate-800">Egresos (Pagos e Impuestos)</h3>
            </div>
            <div className="overflow-x-auto w-full">
              <table className="w-full text-sm">
                <thead className="bg-slate-50/50">
                  <tr className="border-b border-slate-200">
                    <th className="h-10 px-4 text-left font-medium text-slate-500">Detalle</th>
                    <th className="h-10 px-4 text-right font-medium text-slate-500">Importe</th>
                    <th className="h-10 px-4 w-20"></th>
                  </tr>
                </thead>
                <tbody>
                  {egresos.length === 0 ? (
                    <tr>
                      <td colSpan={3} className="p-4 text-center text-slate-500">Sin egresos</td>
                    </tr>
                  ) : (
                    egresos.map((mov: any, idx: number) => (
                      <tr key={`egr-${idx}`} className="border-b border-slate-100 hover:bg-slate-50/80 transition-colors duration-150">
                        <td className="p-3 px-4 text-slate-700">{mov.detalle}</td>
                        <td className="p-3 px-4 text-right font-semibold text-slate-800 tabular-nums">
                          {currencyFormatter.format(mov.egreso)}
                        </td>
                        <td className="p-3 px-4 flex justify-end space-x-1">
                          <button
                            onClick={() => handleEditClick(mov)}
                            className="rounded p-1.5 text-slate-400 hover:bg-blue-50 hover:text-blue-600 transition-colors"
                            title="Editar movimiento"
                          >
                            <Pencil className="h-4 w-4" />
                          </button>
                          <button
                            onClick={() => setMovimientoToDelete(mov.id || mov.Id)}
                            className="rounded p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600 transition-colors"
                            title="Eliminar movimiento"
                          >
                            <Trash2 className="h-4 w-4" />
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
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

      {isFormOpen && (
        <MovimientoBancoForm 
          clienteId={clienteId} 
          movimientoAEditar={movimientoAEditar} // 🔥 Le pasamos el movimiento a editar al formulario
          onClose={handleCloseForm} 
        />
      )}
    </div>
  );
}
