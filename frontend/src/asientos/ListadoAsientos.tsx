import { Fragment, useMemo, useState } from "react";
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  getExpandedRowModel,
  useReactTable,
} from "@tanstack/react-table";
import { Loader2, ChevronDown, ChevronRight, Trash2, Search } from "lucide-react";
import { useAsientos } from "./hooks/useAsientos";
import { useEliminarAsiento } from "./hooks/useEliminarAsiento";
import type { AsientoDto } from "./asientosApi";
import { formatearFechaLocal } from "../utils/formatters";
import toast from 'react-hot-toast';
import { generarFacturaPDF } from '../utils/generarFacturaPDF';

const columnHelper = createColumnHelper<AsientoDto>();

export function ListadoAsientos({ clienteId }: { clienteId: string }) {
  const { data: asientos, isLoading, isError, error } = useAsientos(clienteId);
  const eliminarMutation = useEliminarAsiento();

  const handleDobleClicFila = async (asientoId: number) => {
    try {
      const apiUrl = import.meta.env.VITE_API_URL || '';
      
      const response = await fetch(`${apiUrl}/api/asientos/${asientoId}/factura`);
      
      if (!response.ok) {
        if (response.status === 404) {
          toast.error("Este asiento no tiene un comprobante asociado (ej. Ajuste Manual).");
        } else {
          toast.error("Error al obtener la factura del servidor.");
        }
        return;
      }

      const factura = await response.json();
      generarFacturaPDF(factura);
      toast.success("Comprobante generado correctamente.");
    } catch (error) {
      toast.error("Ocurrió un problema de red.");
      console.error(error);
    }
  };

  const columns = useMemo(() => [
    columnHelper.display({
      id: "expander",
      header: () => null,
      cell: ({ row }) => {
        return (
          <button
            onClick={row.getToggleExpandedHandler()}
            className="cursor-pointer flex items-center justify-center p-1 rounded hover:bg-slate-200 transition-colors"
            title={row.getIsExpanded() ? "Ocultar detalle" : "Ver detalle"}
          >
            {row.getIsExpanded() ? (
              <ChevronDown className="h-4 w-4 text-slate-500" />
            ) : (
              <ChevronRight className="h-4 w-4 text-slate-500" />
            )}
          </button>
        );
      },
    }),
    columnHelper.accessor("numeroAsiento", {
      header: "Número",
      cell: (info) => (
        <span className="font-semibold text-slate-900 tabular-nums">
          #{info.getValue().toString().padStart(4, "0")}
        </span>
      ),
    }),
    columnHelper.accessor("fecha", {
      header: "Fecha",
      cell: (info) => (
        <span className="tabular-nums font-medium text-slate-700">
          {formatearFechaLocal(info.getValue())}
        </span>
      ),
    }),
    columnHelper.accessor("descripcion", {
      header: "Descripción",
      cell: (info) => (
        <span className="text-slate-600 truncate max-w-[300px] block">
          {info.getValue() || "Sin descripción"}
        </span>
      ),
    }),
    columnHelper.display({
      id: "estado",
      header: "Estado",
      cell: (info) => {
        const estaBalanceado = info.row.original.estaBalanceado;
        const estado = info.row.original.estado;
        
        let badgeClass = "bg-slate-100 text-slate-800";
        let text = "Borrador";

        if (estado === 1) {
          badgeClass = "bg-green-100 text-green-800";
          text = "Confirmado";
        } else if (!estaBalanceado) {
          badgeClass = "bg-red-100 text-red-800";
          text = "Desbalanceado";
        }

        return (
          <span
            className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${badgeClass}`}
          >
            {text}
          </span>
        );
      },
    }),
    columnHelper.accessor("totalDebe", {
      header: () => <div className="text-right">Total</div>,
      cell: (info) => {
        const formatted = new Intl.NumberFormat("es-AR", {
          style: "currency",
          currency: "ARS",
        }).format(info.getValue());
        return <div className="text-right font-semibold text-slate-900 tabular-nums">{formatted}</div>;
      },
    }),
    columnHelper.display({
      id: "acciones",
      header: "Acciones",
      cell: (info) => (
        <button
          onClick={() => {
            if (window.confirm("¿Anular este asiento?\n\nSi es un borrador, se elimina. Si está confirmado, se genera su contraasiento (y si pertenece a una factura, se anula la factura completa).")) {
              eliminarMutation.mutate(info.row.original.id.toString());
            }
          }}
          disabled={eliminarMutation.isPending}
          className="p-1.5 text-red-500 hover:bg-red-50 rounded-md transition-colors disabled:opacity-50"
          title="Anular asiento"
        >
          <Trash2 className="h-4 w-4" />
        </button>
      ),
    }),
  ], [eliminarMutation]);

  const [fechaFiltro, setFechaFiltro] = useState<string>("");

  const asientosFiltrados = useMemo(() => {
    if (!asientos) return [];
    // Orden cronológico (fecha y número) para que cada día quede agrupado una sola vez.
    const ordenados = [...asientos].sort((x, y) =>
      String(x.fecha).localeCompare(String(y.fecha)) || x.numeroAsiento - y.numeroAsiento);
    if (!fechaFiltro) return ordenados;

    return ordenados.filter(a => a.fecha.startsWith(fechaFiltro));
  }, [asientos, fechaFiltro]);

  const totalesPorFecha = useMemo(() => {
    const totales: Record<string, { debe: number; haber: number }> = {};
    asientosFiltrados.forEach(a => {
      const f = formatearFechaLocal(a.fecha);
      if (!totales[f]) totales[f] = { debe: 0, haber: 0 };
      totales[f].debe += (a.totalDebe || 0);
      totales[f].haber += (a.totalHaber || 0);
    });
    return totales;
  }, [asientosFiltrados]);

  const table = useReactTable({
    data: asientosFiltrados,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getExpandedRowModel: getExpandedRowModel(),
    getRowCanExpand: () => true,
  });



  const formatCurrency = (value: number) =>
    new Intl.NumberFormat("es-AR", { style: "currency", currency: "ARS" }).format(value);

  if (isLoading) {
    return (
      <div className="flex min-h-[400px] flex-col items-center justify-center space-y-4 text-slate-500">
        <Loader2 className="h-8 w-8 animate-spin text-slate-400" />
        <p>Cargando asientos...</p>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="flex min-h-[400px] flex-col items-center justify-center space-y-4">
        <div className="rounded-md bg-red-50 p-4">
          <p className="text-sm font-medium text-red-800">
            Error al cargar los asientos: {error.message}
          </p>
        </div>
      </div>
    );
  }

  if (!asientos?.length) {
    return (
      <div className="flex min-h-[400px] flex-col items-center justify-center space-y-4 rounded-lg border border-dashed border-slate-300">
        <div className="text-center">
          <h3 className="mt-2 text-sm font-semibold text-slate-900">
            No hay asientos
          </h3>
          <p className="mt-1 text-sm text-slate-500">
            Comenzá cargando tu primer asiento contable.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Date Filter */}
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
      </div>

      <div className="rounded-xl border border-slate-200/80 bg-white shadow-sm overflow-hidden">
        <div className="overflow-x-auto w-full">
          <table className="w-full text-sm">
            <thead className="bg-slate-50/50">
              {table.getHeaderGroups().map((headerGroup) => (
                <tr key={headerGroup.id} className="border-b border-slate-200">
                  {headerGroup.headers.map((header) => (
                    <th
                      key={header.id}
                      className="h-12 px-4 text-left align-middle font-medium text-slate-500"
                    >
                      {header.isPlaceholder
                        ? null
                        : flexRender(
                            header.column.columnDef.header,
                            header.getContext()
                          )}
                    </th>
                  ))}
                </tr>
              ))}
            </thead>
            <tbody>
              {(() => {
                let currentFecha = "";
                return table.getRowModel().rows.map((row, index, arr) => {
                  const rowFecha = formatearFechaLocal(row.original.fecha);
                  const showHeader = rowFecha !== currentFecha;
                  currentFecha = rowFecha;

                  const nextRowFecha = index < arr.length - 1 ? formatearFechaLocal(arr[index + 1].original.fecha) : "";
                  const showFooter = rowFecha !== nextRowFecha;

                  return (
                    <Fragment key={row.id}>
                      {showHeader && (
                        <tr className="bg-slate-100 border-b border-slate-200">
                          <td colSpan={columns.length} className="px-4 py-3 text-sm font-bold text-slate-700 ">
                            Asientos del {rowFecha}
                          </td>
                        </tr>
                      )}
                      <tr
                        onDoubleClick={() => handleDobleClicFila(row.original.id)}
                        className={`border-b border-slate-100 transition-colors duration-150 hover:bg-slate-50/80 cursor-pointer ${
                          row.getIsExpanded() ? "bg-slate-50/50" : ""
                        }`}
                      >
                        {row.getVisibleCells().map((cell) => (
                          <td key={cell.id} className="p-4 align-middle">
                            {flexRender(
                              cell.column.columnDef.cell,
                              cell.getContext()
                            )}
                          </td>
                        ))}
                      </tr>
                      {/* Expanded Row Content */}
                      {row.getIsExpanded() && (
                        <tr>
                          <td colSpan={row.getVisibleCells().length} className="p-0 border-b border-slate-200 bg-slate-50/80">
                            <div className="p-4 sm:p-6">
                              <h4 className="text-sm font-semibold text-slate-800 mb-3">
                                Detalle del asiento (Partida Doble)
                              </h4>
                              <div className="rounded-lg border border-slate-200 bg-white shadow-sm overflow-hidden">
                                <div className="overflow-x-auto w-full">
                                  <table className="w-full text-sm">
                                    <thead className="bg-slate-50/50 border-b border-slate-200">
                                      <tr>
                                        <th className="px-4 py-2 text-left font-medium text-slate-600">Cuenta</th>
                                        <th className="px-4 py-2 text-right font-medium text-slate-600">Debe</th>
                                        <th className="px-4 py-2 text-right font-medium text-slate-600">Haber</th>
                                      </tr>
                                    </thead>
                                    <tbody className="divide-y divide-slate-100">
                                      {row.original.lineas.map((linea) => (
                                        <tr key={linea.id} className="hover:bg-slate-50/80 transition-colors duration-150">
                                          <td className="px-4 py-2 text-slate-800 font-medium">
                                            {linea.nombreCuenta}
                                          </td>
                                          <td className="px-4 py-2 text-right text-slate-700 font-medium tabular-nums">
                                            {linea.debe > 0 ? formatCurrency(linea.debe) : ""}
                                          </td>
                                          <td className="px-4 py-2 text-right text-slate-700 font-medium tabular-nums">
                                            {linea.haber > 0 ? formatCurrency(linea.haber) : ""}
                                          </td>
                                        </tr>
                                      ))}
                                    </tbody>
                                    <tfoot className="bg-slate-50 border-t border-slate-200">
                                      <tr>
                                        <td colSpan={1} className="px-4 py-2.5 text-right font-semibold text-slate-700">
                                          Totales:
                                        </td>
                                        <td className="px-4 py-2.5 text-right font-bold text-slate-900 tabular-nums">
                                          {formatCurrency(row.original.totalDebe)}
                                        </td>
                                        <td className="px-4 py-2.5 text-right font-bold text-slate-900 tabular-nums">
                                          {formatCurrency(row.original.totalHaber)}
                                        </td>
                                      </tr>
                                    </tfoot>
                                  </table>
                                </div>
                              </div>
                            </div>
                          </td>
                        </tr>
                      )}
                      {showFooter && (
                        <tr className="bg-slate-200/60 border-b border-slate-300">
                          <td colSpan={columns.length - 2} className="px-4 py-3 text-sm font-bold text-slate-800 text-right ">
                            Total del día {rowFecha}:
                          </td>
                          <td className="px-4 py-3 text-sm font-bold text-slate-800 text-right">
                            <span className="text-slate-600 font-medium">Debe:</span> <span className="tabular-nums font-bold text-slate-900">{formatCurrency(totalesPorFecha[rowFecha]?.debe || 0)}</span> <br />
                            <span className="text-slate-600 font-medium">Haber:</span> <span className="tabular-nums font-bold text-slate-900">{formatCurrency(totalesPorFecha[rowFecha]?.haber || 0)}</span>
                          </td>
                          <td className="px-4 py-3"></td>
                        </tr>
                      )}
                    </Fragment>
                  );
                })})()}
            </tbody>
          </table>
        </div>
      </div>
      <div className="text-sm text-slate-500">
        Mostrando {asientosFiltrados.length} asiento{asientosFiltrados.length !== 1 && "s"}.
      </div>
    </div>
  );
}
