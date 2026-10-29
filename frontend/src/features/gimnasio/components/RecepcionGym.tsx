import { useState, useEffect, useMemo } from "react";
import { Search, CreditCard, MessageCircle } from "lucide-react";
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from "@tanstack/react-table";
import { ModalCobrarCuota } from "./ModalCobrarCuota";
import { IngresoSocios } from "./IngresoSocios";
import { SociosEnRiesgo } from "./SociosEnRiesgo";
import { datosDelNegocio } from "../../../utils/pdfComun";
import { hoyLocal } from "../../../utils/formatters";
import { enlaceWhatsApp, estadoDeCuota, mensajeDeCuota } from "../../../utils/whatsapp";

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

type FiltroCuota = "todos" | "porVencer" | "vencidos";

export function RecepcionGym() {
  const [socios, setSocios] = useState<SocioDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState("");
  const [socioSeleccionado, setSocioSeleccionado] = useState<SocioDto | null>(null);
  const [filtro, setFiltro] = useState<FiltroCuota>("todos");
  // Contadores que avisan a las partes de la pantalla que tienen que volver a mirar: se cobró una cuota o cambió una visita.
  const [cobros, setCobros] = useState(0);
  const [visitas, setVisitas] = useState(0);

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

  const hoy = hoyLocal();
  const cantidades = useMemo(() => ({
    porVencer: socios.filter((s) => estadoDeCuota(s.fechaVencimiento, hoy) === "PorVencer").length,
    vencidos: socios.filter((s) => estadoDeCuota(s.fechaVencimiento, hoy) === "Vencida").length,
  }), [socios, hoy]);

  const filteredSocios = useMemo(() => {
    return socios.filter((s) => {
      const search = searchTerm.toLowerCase();
      const fullName = `${s.nombre} ${s.apellido}`.toLowerCase();
      if (!(fullName.includes(search) || s.dni.includes(search))) return false;
      const estado = estadoDeCuota(s.fechaVencimiento, hoy);
      return filtro === "todos" || (filtro === "porVencer" ? estado === "PorVencer" : estado === "Vencida");
    });
  }, [socios, searchTerm, filtro, hoy]);

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
      columnHelper.accessor("planActualNombre", {
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
          // Un socio que todavía no pagó ninguna cuota no está vencido: no tiene plan.
          if (!fecha) {
            return (
              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border bg-slate-100 text-slate-600 border-slate-200">
                Sin plan
              </span>
            );
          }
          const alDia = isAlDia(fecha);
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
        cell: (info) => {
          const socio = info.row.original;
          const mensaje = mensajeDeCuota({
            nombre: socio.nombre,
            gimnasio: datosDelNegocio().nombreFantasia || datosDelNegocio().razonSocial,
            vencimiento: socio.fechaVencimiento,
            hoy: hoyLocal(),
          });
          const enlace = mensaje ? enlaceWhatsApp(socio.telefono, mensaje) : null;
          return (
          <div className="flex justify-end gap-2">
            {mensaje && (
              enlace ? (
                <a
                  href={enlace}
                  target="_blank"
                  rel="noopener noreferrer"
                  title="Abrir WhatsApp con el aviso de cuota ya escrito"
                  className="inline-flex items-center justify-center rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 transition-colors"
                >
                  <MessageCircle className="mr-1.5 h-3.5 w-3.5" />
                  Avisar
                </a>
              ) : (
                <span
                  title={socio.telefono ? "El teléfono cargado no es válido: corregilo en Socios" : "Este socio no tiene teléfono cargado"}
                  className="inline-flex cursor-not-allowed items-center justify-center rounded-md border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs font-medium text-slate-400"
                >
                  <MessageCircle className="mr-1.5 h-3.5 w-3.5" />
                  Sin teléfono
                </span>
              )
            )}
            <button
              type="button"
              onClick={() => {
                setSocioSeleccionado(socio);
              }}
              className="inline-flex items-center justify-center rounded-md bg-marca-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-marca-700 transition-colors"
            >
              <CreditCard className="mr-1.5 h-3.5 w-3.5" />
              Cobrar Cuota
            </button>
          </div>
          );
        },
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
      <IngresoSocios
        refrescar={cobros}
        onIngreso={() => setVisitas((v) => v + 1)}
        onCobrar={(s) => setSocioSeleccionado({ ...s, telefono: "", activo: true })}
      />

      <SociosEnRiesgo refrescar={visitas + cobros} />

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
        <div className="mt-3 flex flex-wrap gap-2" role="group" aria-label="Filtrar por estado de la cuota">
          {([
            ["todos", "Todos"],
            ["porVencer", `Por vencer en 7 días (${cantidades.porVencer})`],
            ["vencidos", `Vencidos (${cantidades.vencidos})`],
          ] as [FiltroCuota, string][]).map(([valor, texto]) => (
            <button
              key={valor}
              type="button"
              onClick={() => setFiltro(valor)}
              aria-pressed={filtro === valor}
              className={`rounded-full border px-3 py-1 text-sm font-medium transition-colors ${filtro === valor ? "border-marca-600 bg-marca-50 text-marca-700" : "border-slate-300 bg-white text-slate-600 hover:bg-slate-50"}`}
            >
              {texto}
            </button>
          ))}
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
            setCobros((c) => c + 1);
          }}
        />
      )}
    </div>
  );
}