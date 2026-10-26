import { useState, useEffect, useMemo } from "react";
import { Plus, Edit2, Trash2 } from "lucide-react";
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from "@tanstack/react-table";
import { SocioForm } from "./SocioForm";

interface SocioDto {
  id: string;
  nombre: string;
  apellido: string;
  dni: string;
  telefono: string;
  planId?: string;
  planActualNombre?: string;
  fechaVencimiento?: string | null;
  activo: boolean;
}

const columnHelper = createColumnHelper<SocioDto>();

export function SociosGym() {
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [socios, setSocios] = useState<SocioDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [socioToEdit, setSocioToEdit] = useState<SocioDto | null>(null);

  // Modal de eliminación
  const [idToDelete, setIdToDelete] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const fetchSocios = async () => {
    setIsLoading(true);
    try {
      const response = await fetch("/api/socios");
      if (response.ok) {
        const data = await response.json();
        setSocios(data);
      }
    } catch (error) {
      console.error("Error fetching socios:", error);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchSocios();
  }, []);

  const confirmarEliminacion = async () => {
    if (!idToDelete) return;
    setIsDeleting(true);
    try {
      const response = await fetch(`/api/socios/${idToDelete}`, { method: 'DELETE' });
      if (response.ok) {
        // Actualizamos el estado local eliminando el registro para evitar recargar
        setSocios(prevSocios => prevSocios.filter(s => s.id !== idToDelete));
      } else {
        const err = await response.text();
        alert(`Error al eliminar el socio: ${err}`);
      }
    } catch (error) {
      console.error("Error eliminando socio:", error);
      alert("Error de red al intentar eliminar el socio.");
    } finally {
      setIsDeleting(false);
      setIdToDelete(null);
    }
    
  };
  const formatearFechaLocal = (fechaStr: string | null | undefined) => {
  if (!fechaStr) return "-";
  const [yyyy, mm, dd] = fechaStr.split('T')[0].split('-');
  return `${dd}/${mm}/${yyyy}`; 
    };

  const columns = useMemo(() => [
    columnHelper.accessor("nombre", {
      header: "Nombre Completo",
      cell: (info) => (
        <span className="font-medium text-slate-900">
          {info.row.original.apellido}, {info.getValue()}
        </span>
      ),
    }),
    columnHelper.accessor("dni", {
      header: "DNI / Telnono",
      cell: (info) => (
        <div className="text-slate-600">
          <div>{info.getValue()}</div>
          <div className="text-xs text-slate-400">{info.row.original.telefono || "-"}</div>
        </div>
      ),
    }),
    columnHelper.accessor("planActualNombre", {
      header: "Plan Actual",
      cell: (info) => (
        <span className="text-slate-600">
          {info.getValue() ? (
            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-50 text-blue-700 border border-blue-200">
              {info.getValue()}
            </span>
          ) : (
            "-"
          )}
        </span>
      ),
    }),
    columnHelper.accessor("fechaVencimiento", {
      header: "Vencimiento",
      cell: (info) => {
        const val = info.getValue();
        return (
          <span className="text-slate-600 font-medium">
            {val ? formatearFechaLocal(val) : "-"}
          </span>
        );
      },
    }),
    columnHelper.display({
      id: "acciones",
      header: () => <div className="text-right">Acciones</div>,
      cell: (info) => (
        <div className="flex justify-end gap-2">
          <button
            onClick={() => {
              setSocioToEdit(info.row.original);
              setIsFormOpen(true);
            }}
            className="p-1.5 text-slate-400 hover:text-blue-600 hover:bg-blue-50 rounded-md transition-colors"
            title="Editar"
          >
            <Edit2 className="h-4 w-4" />
          </button>
          <button
            onClick={() => setIdToDelete(info.row.original.id)}
            className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-md transition-colors"
            title="Eliminar"
          >
            <Trash2 className="h-4 w-4" />
          </button>
        </div>
      ),
    }),
  ], []);

  const table = useReactTable({
    data: socios,
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-semibold text-slate-900">Gestin de Socios</h2>
        <button
          onClick={() => {
            setSocioToEdit(null);
            setIsFormOpen(true);
          }}
          className="inline-flex items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2"
        >
          <Plus className="mr-2 h-4 w-4" />
          Nuevo Socio
        </button>
      </div>

      <div className="rounded-md border border-slate-200 bg-white">
        <div className="overflow-x-auto">
          {isLoading ? (
            <div className="p-8 text-center text-slate-500">Cargando socios...</div>
          ) : (
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
                {table.getRowModel().rows.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="p-8 text-center text-slate-500">
                      No hay socios registrados.
                    </td>
                  </tr>
                ) : (
                  table.getRowModel().rows.map((row) => (
                    <tr
                      key={row.id}
                      className="border-b border-slate-100 transition-colors hover:bg-slate-50/50"
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
                  ))
                )}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {isFormOpen && (
        <SocioForm 
          socioAEditar={socioToEdit}
          onClose={() => {
            setIsFormOpen(false);
            fetchSocios();
          }} 
        />
      )}

      {idToDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-lg shadow-xl p-6 w-full max-w-md">
            <h3 className="text-lg font-bold text-slate-900 mb-2">Confirmar Eliminación</h3>
            <p className="text-slate-600 mb-6">
              ¿Estás seguro de que deseas eliminar este registro? Esta acción no se puede deshacer.
            </p>
            <div className="flex justify-end gap-3">
              <button
                onClick={() => setIdToDelete(null)}
                disabled={isDeleting}
                className="px-4 py-2 text-sm font-medium text-slate-700 bg-slate-100 rounded-md hover:bg-slate-200 transition-colors"
              >
                Cancelar
              </button>
              <button
                onClick={confirmarEliminacion}
                disabled={isDeleting}
                className="px-4 py-2 text-sm font-medium text-white bg-red-600 rounded-md hover:bg-red-700 transition-colors disabled:opacity-50 inline-flex items-center"
              >
                {isDeleting ? "Eliminando..." : "Eliminar"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

