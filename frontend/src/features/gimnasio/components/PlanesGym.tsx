import { useState, useEffect, useMemo } from "react";
import { Plus, Edit2, Trash2 } from "lucide-react";
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from "@tanstack/react-table";
import { PlanForm } from "./PlanForm";

interface PlanDto {
  id: string;
  nombre: string;
  precioMensual: number;
  duracionMeses: number;
  activo: boolean;
}

const columnHelper = createColumnHelper<PlanDto>();

const currencyFormatter = new Intl.NumberFormat("es-AR", {
  style: "currency",
  currency: "ARS",
});

export function PlanesGym() {
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [planes, setPlanes] = useState<PlanDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [planToEdit, setPlanToEdit] = useState<PlanDto | null>(null);
  
  // Modal de eliminación
  const [idToDelete, setIdToDelete] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const fetchPlanes = async () => {
    setIsLoading(true);
    try {
      const response = await fetch("/api/planes");
      if (response.ok) {
        const data = await response.json();
        setPlanes(data);
      }
    } catch (error) {
      console.error("Error fetching planes:", error);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchPlanes();
  }, []);

  const confirmarEliminacion = async () => {
    if (!idToDelete) return;
    setIsDeleting(true);
    try {
      const response = await fetch(`/api/planes/${idToDelete}`, { method: 'DELETE' });
      if (response.ok) {
        // Actualizamos el estado local eliminando el registro para evitar recargar
        setPlanes(prevPlanes => prevPlanes.filter(p => p.id !== idToDelete));
      } else {
        const err = await response.text();
        alert(`Error al eliminar el plan: ${err}`);
      }
    } catch (error) {
      console.error("Error eliminando plan:", error);
      alert("Error de red al intentar eliminar el plan.");
    } finally {
      setIsDeleting(false);
      setIdToDelete(null);
    }
  };

  const columns = useMemo(() => [
    columnHelper.accessor("nombre", {
      header: "Nombre del Plan",
      cell: (info) => (
        <span className="font-medium text-slate-900">{info.getValue()}</span>
      ),
    }),
    columnHelper.accessor("precioMensual", {
      header: () => <div className="text-right">Precio Mensual</div>,
      cell: (info) => (
        <div className="text-right text-slate-600 font-medium">
          {currencyFormatter.format(info.getValue())}
        </div>
      ),
    }),
    columnHelper.accessor("duracionMeses", {
      header: () => <div className="text-center">Duración (Meses)</div>,
      cell: (info) => (
        <div className="text-center text-slate-600">
          {info.getValue()}
        </div>
      ),
    }),
    columnHelper.accessor("activo", {
      header: "Estado",
      cell: (info) => {
        const activo = info.getValue();
        return (
          <span
            className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border ${
              activo
                ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                : "bg-slate-100 text-slate-600 border-slate-200"
            }`}
          >
            {activo ? "Activo" : "Inactivo"}
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
              setPlanToEdit(info.row.original);
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
    data: planes,
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-semibold text-slate-900">Gestin de Planes</h2>
        <button
          onClick={() => {
            setPlanToEdit(null);
            setIsFormOpen(true);
          }}
          className="inline-flex items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2"
        >
          <Plus className="mr-2 h-4 w-4" />
          Nuevo Plan
        </button>
      </div>

      <div className="rounded-md border border-slate-200 bg-white">
        <div className="overflow-x-auto">
          {isLoading ? (
            <div className="p-8 text-center text-slate-500">Cargando planes...</div>
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
                      No hay planes registrados.
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
        <PlanForm 
          planAEditar={planToEdit}
          onClose={() => {
            setIsFormOpen(false);
            fetchPlanes();
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
