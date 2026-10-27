import { useState, useMemo } from "react";
import { Loader2, Pencil, Ban, Search, FileText } from "lucide-react";
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from "@tanstack/react-table";
import { useObtenerFacturas } from "../../../hooks/useObtenerFacturas";
import type { FacturaDto } from "../../../api/facturacionApi";
import { useAnularFactura } from "../../ventas/hooks/useAnularFactura";
import { ModalEditarFactura } from "../../ventas/components/ModalEditarFactura";
import { formatearFechaLocal } from "../../../utils/formatters";
import { generarFacturaPDF } from "../../../utils/generarFacturaPDF";
import { formatearCuit } from "../../../utils/pdfComun";
import { BotonPDF } from "../../../components/reportes/BotonPDF";

const columnHelper = createColumnHelper<FacturaDto>();

const currencyFormatter = new Intl.NumberFormat("es-AR", {
  style: "currency",
  currency: "ARS",
});

export function LibroCompra() {
  const { mutate: anularFactura } = useAnularFactura();
  const [facturaAAnular, setFacturaAAnular] = useState<number | null>(null);
  const [facturaAEditar, setFacturaAEditar] = useState<any | null>(null);
  const [fechaFiltro, setFechaFiltro] = useState<string>("");

  const columns = useMemo(() => [
    columnHelper.accessor("fecha", {
      header: "Fecha",
      cell: (info) => (
        <span className="tabular-nums font-medium text-slate-700">
          {formatearFechaLocal(info.getValue())}
        </span>
      ),
      footer: () => <span className="font-bold">TOTALES</span>,
    }),
    columnHelper.accessor("tipoComprobante", {
      header: "Tipo de Fact.",
      cell: (info) => (
        <span className="font-medium text-slate-900">{info.getValue()}</span>
      ),
    }),
    columnHelper.accessor("numeroFactura", {
      header: "N° de Fact.",
      cell: (info) => <span className="tabular-nums whitespace-nowrap">{info.getValue() || "s/n"}</span>,
    }),
    columnHelper.accessor("cliente", {
      header: "Nombre",
      cell: (info) => info.getValue() || "-",
    }),
    columnHelper.accessor("cuit", {
      header: "CUIT",
      cell: (info) => <span className="tabular-nums whitespace-nowrap">{formatearCuit(info.getValue()) || "-"}</span>,
    }),
    columnHelper.accessor("subtotal", {
      header: () => <div className="text-right">Neto</div>,
      cell: (info) => (
        <div className="text-right text-slate-700 font-medium tabular-nums">
          {currencyFormatter.format(info.getValue())}
        </div>
      ),
      footer: (info) => {
        const total = info.table.getFilteredRowModel().rows.filter((row) => !row.original.anulada).reduce((sum, row) => sum + row.original.subtotal, 0);
        return <div className="text-right font-bold text-slate-900 tabular-nums">{currencyFormatter.format(total)}</div>;
      },
    }),
    columnHelper.accessor("iva", {
      header: () => <div className="text-right">IVA</div>,
      cell: (info) => (
        <div className="text-right text-slate-700 font-medium tabular-nums">
          {currencyFormatter.format(info.getValue())}
        </div>
      ),
      footer: (info) => {
        const total = info.table.getFilteredRowModel().rows.filter((row) => !row.original.anulada).reduce((sum, row) => sum + row.original.iva, 0);
        return <div className="text-right font-bold text-slate-900 tabular-nums">{currencyFormatter.format(total)}</div>;
      },
    }),
    columnHelper.accessor("total", {
      header: () => <div className="text-right">Total</div>,
      cell: (info) => (
        <div className="text-right font-semibold text-slate-900 tabular-nums">
          {currencyFormatter.format(info.getValue())}
        </div>
      ),
      footer: (info) => {
        const total = info.table.getFilteredRowModel().rows.filter((row) => !row.original.anulada).reduce((sum, row) => sum + row.original.total, 0);
        return <div className="text-right font-bold text-slate-900 tabular-nums">{currencyFormatter.format(total)}</div>;
      },
    }),
    columnHelper.accessor("condicionPago", {
      header: "Condición de Pago",
      cell: (info) => {
        const val = info.getValue();
        let colorClass = "bg-slate-100 text-slate-700 border-slate-200";
        let text = val ? "Cuenta Corriente" : "-";

        if (val === "ContadoCaja") {
          colorClass = "bg-emerald-50 text-emerald-700 border-emerald-200";
          text = "Contado (Caja)";
        } else if (val === "TransferenciaBanco") {
          colorClass = "bg-blue-50 text-blue-700 border-blue-200";
          text = "Transferencia (Banco)";
        }

        return (
          <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border ${colorClass}`}>
            {text}
          </span>
        );
      },
    }),
    columnHelper.display({
      id: "acciones",
      header: () => <div className="text-center">Acciones</div>,
      cell: (info) => info.row.original.anulada ? (
        <div className="flex justify-center items-center gap-2">
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border bg-red-50 text-red-700 border-red-200">
            Anulada
          </span>
          <button
            onClick={(e) => {
              e.preventDefault();
              e.stopPropagation();
              generarFacturaPDF(info.row.original as any);
            }}
            className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-900 transition-colors"
            title="Ver o imprimir la factura en PDF"
          >
            <FileText className="h-4 w-4" />
          </button>
        </div>
      ) : (
        <div className="flex justify-center items-center gap-2">
          <button
            onClick={(e) => {
              e.preventDefault();
              e.stopPropagation();
              generarFacturaPDF(info.row.original as any);
            }}
            className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-900 transition-colors"
            title="Ver o imprimir la factura en PDF"
          >
            <FileText className="h-4 w-4" />
          </button>
          <button
            onClick={(e) => {
              e.preventDefault();
              e.stopPropagation();
              setFacturaAEditar(info.row.original);
            }}
            className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-blue-600 transition-colors"
            title="Editar factura"
          >
            <Pencil className="h-4 w-4" />
          </button>
          <button
            onClick={(e) => {
              e.preventDefault();
              e.stopPropagation();
              setFacturaAAnular(info.row.original.id);
            }}
            className="rounded p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600 transition-colors"
            title="Anular factura"
          >
            <Ban className="h-4 w-4" />
          </button>
        </div>
      ),
    }),
  ], [anularFactura]);

  const { data: facturas, isLoading, isError, error } = useObtenerFacturas();

  const facturasFiltradas = useMemo(() => {
    let list = facturas?.filter(f => f.tipoOperacion?.toLowerCase() === "compra") || [];
    if (fechaFiltro) {
      list = list.filter(f => f.fecha && f.fecha.startsWith(fechaFiltro));
    }
    return list;
  }, [facturas, fechaFiltro]);

  const totales = useMemo(() => {
    // Las facturas anuladas se muestran pero no suman.
    return facturasFiltradas.filter(f => !f.anulada).reduce((acc, factura) => ({
      neto: acc.neto + factura.subtotal,
      iva: acc.iva + factura.iva,
      total: acc.total + factura.total
    }), { neto: 0, iva: 0, total: 0 });
  }, [facturasFiltradas]);

  const table = useReactTable({
    data: facturasFiltradas,
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  if (isLoading) {
    return (
      <div className="flex min-h-[400px] flex-col items-center justify-center space-y-4 text-slate-500">
        <Loader2 className="h-8 w-8 animate-spin text-slate-400" />
        <p>Cargando Libro de Compras...</p>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="flex min-h-[400px] flex-col items-center justify-center space-y-4">
        <div className="rounded-md bg-red-50 p-4">
          <p className="text-sm font-medium text-red-800">
            Error al cargar las compras: {error.message}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-white p-4 sm:p-5 rounded-xl border border-slate-200/80 shadow-sm">
        <div className="flex items-center gap-3 flex-wrap">
          <div className="p-2 bg-slate-100 rounded-lg text-slate-500 shrink-0">
            <Search className="h-4 w-4" />
          </div>
          <label htmlFor="fechaFiltroCompra" className="text-sm font-medium text-slate-700 shrink-0">
            Filtrar por fecha:
          </label>
          <div className="flex items-center gap-2">
            <input
              id="fechaFiltroCompra"
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
        <BotonPDF tipo="compra" fecha={fechaFiltro} />
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
                      className="h-11 px-3 text-left align-middle font-medium text-slate-500 whitespace-nowrap"
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
              {table.getRowModel().rows?.length ? (
                table.getRowModel().rows.map((row) => (
                  <tr
                    key={row.id}
                    className={`border-b border-slate-100 hover:bg-slate-50/80 transition-colors duration-150 ${
                      row.original.anulada ? "*:text-slate-400 *:line-through *:decoration-slate-400 bg-slate-50/50" : ""
                    }`}
                  >
                    {row.getVisibleCells().map((cell) => (
                      <td key={cell.id} className="px-3 py-2.5 align-middle text-slate-700">
                        {flexRender(
                          cell.column.columnDef.cell,
                          cell.getContext()
                        )}
                      </td>
                    ))}
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={columns.length}
                    className="h-24 text-center text-slate-500"
                  >
                    No hay facturas cargadas.
                  </td>
                </tr>
              )}
            </tbody>
            <tfoot className="bg-slate-50 font-bold border-t-2 border-slate-200">
              <tr>
                <td colSpan={5} className="px-3 py-3 text-right text-slate-700">
                  TOTALES
                </td>
                <td className="px-3 py-3 text-right text-slate-900 font-bold tabular-nums whitespace-nowrap">
                  {currencyFormatter.format(totales.neto)}
                </td>
                <td className="px-3 py-3 text-right text-slate-900 font-bold tabular-nums whitespace-nowrap">
                  {currencyFormatter.format(totales.iva)}
                </td>
                <td className="px-3 py-3 text-right text-slate-900 font-bold tabular-nums whitespace-nowrap">
                  {currencyFormatter.format(totales.total)}
                </td>
                <td colSpan={2}></td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>

      {facturaAAnular !== null && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm transition-opacity">
          <div className="w-[95%] max-w-md mx-auto rounded-xl bg-white p-6 shadow-xl">
            <div className="flex items-center justify-center mb-4 h-12 w-12 rounded-full bg-red-100 mx-auto">
              <Ban className="h-6 w-6 text-red-600" />
            </div>
            <h3 className="text-center text-lg font-semibold text-slate-900">
              Anular comprobante
            </h3>
            <p className="mt-2 text-center text-sm text-slate-500">
              ¿Querés anular esta factura? Queda registrada como anulada, su asiento se anula y se registran los movimientos inversos de caja/banco y stock. Esta acción no se puede deshacer.
            </p>
            <div className="mt-6 flex justify-center space-x-3">
              <button
                onClick={() => setFacturaAAnular(null)}
                className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 transition-colors"
              >
                Cancelar
              </button>
              <button
                onClick={() => {
                  anularFactura(facturaAAnular);
                  setFacturaAAnular(null);
                }}
                className="rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 transition-colors shadow-sm"
              >
                Sí, anular
              </button>
            </div>
          </div>
        </div>
      )}

      {facturaAEditar && (
        <ModalEditarFactura
          factura={facturaAEditar}
          onClose={() => setFacturaAEditar(null)}
        />
      )}
    </div>
  );
}

