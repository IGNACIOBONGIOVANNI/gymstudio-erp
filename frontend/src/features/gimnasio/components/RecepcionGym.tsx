import { useState, useEffect, useMemo } from "react";
import { Search, CreditCard } from "lucide-react";
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from "@tanstack/react-table";
import { ModalCobrarCuota } from "./ModalCobrarCuota";

interface SocioDto {
  id: string;
  nombre: string;
  apellido: string;
  dni: string;
  telefono: string;
  planId?: string;
  planNombre?: string;
  fechaVencimiento?: string | null;
  activo: boolean;
}

const columnHelper = createColumnHelper<SocioDto>();

export function RecepcionGym() {
  const [socios, setSocios] = useState<SocioDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState("");
  const [socioSeleccionado, setSocioSeleccionado] = useState<SocioDto | null>(null);

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

  const filteredSocios = useMemo(() => {
    return socios.filter((s) => {
      const search = searchTerm.toLowerCase();
      const fullName = `${s.nombre} ${s.apellido}`.toLowerCase();
      return fullName.includes(search) || s.dni.includes(search);
    });
  }, [socios, searchTerm]);

  // Validar si la fecha es mayor o igual a hoy
  const isAlDia = (fechaStr: string | null | undefined) => {
    if (!fechaStr) return false;
    
    const [yyyy, mm, dd] = fechaStr.split('T')[0].split('-');
    const vencimiento = new Date(Number(yyyy), Number(mm) - 1, Number(dd));
    
    const hoy = new Date();
    hoy.setHours(0, 0, 0, 0); 
    
    return vencimiento >= hoy;
  };

  // 🔥 NUEVO: Función matemática para calcular los días restantes
  const calcularDiasRestantes = (fechaStr: string | null | undefined) => {
    if (!fechaStr) return 0;
    
    const [yyyy, mm, dd] = fechaStr.split('T')[0].split('-');
    const vencimiento = new Date(Number(yyyy), Number(mm) - 1, Number(dd));
    
    const hoy = new Date();
    hoy.setHours(0, 0, 0, 0);

    const diferenciaTiempo = vencimiento.getTime() - hoy.getTime();
    const diferenciaDias = Math.ceil(diferenciaTiempo / (1000 * 60 * 60 * 24));
    
    return diferenciaDias;
  };

  const formatearFechaLocal = (fechaStr: string | null | undefined) => {
    if (!fechaStr) return "-";
    const [yyyy, mm, dd] = fechaStr.split('T')[0].split('-');
    return `${dd}/${mm}/${yyyy}`;
  };

  const columns = useMemo(() => {
    return [
      columnHelper.accessor("nombre", {
        header: "Socio",
        cell: (info) => (
          <span className="font-medium text-slate-900">
            {info.row.original.apellido}, {info.row.original.nombre}
          </span>
        ),
      }),
      columnHelper.accessor("dni", {
        header: "DNI",
        cell: (info) => <span className="text-slate-600">{info.getValue()}</span>,
      }),
      columnHelper.accessor("planNombre", {
        header: "Plan Actual",
        cell: (info) => (
          <span className="text-slate-600">
            {info.getValue() || "-"}
          </span>
        ),
      }),
      columnHelper.accessor("fechaVencimiento", {
        header: "Vencimiento",
        cell: (info) => (
          <span className="text-slate-600 font-medium">
            {formatearFechaLocal(info.getValue())}
          </span>
        ),
      }),
      columnHelper.display({
        id: "estado",
        header: "Estado",
        cell: (info) => {
          const fecha = info.row.original.fechaVencimiento;
          const alDia = isAlDia(fecha);
          // 🔥 Ejecutamos el cálculo de días para este socio
          const diasRestantes = calcularDiasRestantes(fecha);

          return (
            <span
              className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border ${
                alDia
                  ? "bg-green-100 text-green-700 border-green-200"
                  : "bg-red-100 text-red-700 border-red-200"
              }`}
            >
              {alDia ? `Activo (${diasRestantes} días)` : "Vencido"}
            </span>
          );
        },
      }),
      columnHelper.display({
        id: "acciones",
        header: () => <div className="text-right">Acciones</div>,
        cell: (info) => (
          <div className="flex justify-end">
            <button
              type="button"
              onClick={() => {
                setSocioSeleccionado(info.row.original);
              }}
              className="inline-flex items-center justify-center rounded-md bg-marca-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-marca-700 transition-colors"
            >
              <CreditCard className="mr-1.5 h-3.5 w-3.5" />
              Cobrar Cuota
            </button>
          </div>
        ),
      }),
    ];
  }, []);

  const table = useReactTable({
    data: filteredSocios,
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div className="space-y-6">
      {/* Buscador Destacado */}
      <div className="bg-white p-4 rounded-lg shadow-sm border border-slate-200">
        <div className="relative max-w-2xl">
          <Search className="absolute left-3 top-3 h-5 w-5 text-slate-400" />
          <input
            type="text"
            placeholder="Buscar por Nombre, Apellido o DNI..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full pl-10 pr-4 py-3 rounded-lg border-2 border-slate-200 text-base focus:border-indigo-500 focus:outline-none focus:ring-0 transition-colors"
          />
        </div>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          {isLoading ? (
            <div className="p-12 text-center text-slate-500 font-medium">Cargando mostrador...</div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-slate-50">
                {table.getHeaderGroups().map((headerGroup) => (
                  <tr key={headerGroup.id} className="border-b border-slate-200">
                    {headerGroup.headers.map((header) => (
                      <th
                        key={header.id}
                        className="h-12 px-6 text-left align-middle font-semibold text-slate-700"
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
              <tbody className="divide-y divide-slate-100">
                {table.getRowModel().rows.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="p-12 text-center text-slate-500 font-medium">
                      No se encontraron coincidencias.
                    </td>
                  </tr>
                ) : (
                  table.getRowModel().rows.map((row) => (
                    <tr
                      key={row.id}
                      className="transition-colors hover:bg-slate-50"
                    >
                      {row.getVisibleCells().map((cell) => (
                        <td key={cell.id} className="p-6 align-middle">
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

      {socioSeleccionado && (
        <ModalCobrarCuota 
          socio={socioSeleccionado} 
          onClose={() => setSocioSeleccionado(null)} 
          onSuccess={() => {
            setSocioSeleccionado(null);
            fetchSocios();
          }} 
        />
      )}
    </div>
  );
}