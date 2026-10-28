import { useCallback, useEffect, useState } from "react";
import { Eye, Edit2, Plus, Trash2 } from "lucide-react";
import toast from "react-hot-toast";
import { pedir, type EjercicioDto, type RutinaResumen } from "../../../api/rutinasApi";
import { useAuth } from "../../../context/AuthContext";
import { EjercicioForm } from "./EjercicioForm";
import { RutinaForm } from "./RutinaForm";

type Pestana = "rutinas" | "ejercicios";

const boton = "inline-flex items-center justify-center rounded-md bg-marca-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-marca-700 focus:outline-none focus:ring-2 focus:ring-marca-500 focus:ring-offset-2";
const celdaTitulo = "h-12 px-4 text-left align-middle font-medium text-slate-500";

const colorNivel: Record<string, string> = {
  Principiante: "bg-emerald-50 text-emerald-700 border-emerald-200",
  Intermedio: "bg-amber-50 text-amber-700 border-amber-200",
  Avanzado: "bg-rose-50 text-rose-700 border-rose-200",
};

function Insignia({ children, clase }: { children: React.ReactNode; clase: string }) {
  return <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium ${clase}`}>{children}</span>;
}

export function RutinasGym() {
  const { user } = useAuth();
  const puedeAdministrar = user?.rol === "Administrador" || user?.rol === "Contador";
  const [pestana, setPestana] = useState<Pestana>("rutinas");

  const [rutinas, setRutinas] = useState<RutinaResumen[]>([]);
  const [ejercicios, setEjercicios] = useState<EjercicioDto[]>([]);
  const [cargando, setCargando] = useState(true);
  const [verInactivos, setVerInactivos] = useState(false);

  // null = cerrado; { id: undefined } = nueva
  const [rutinaAbierta, setRutinaAbierta] = useState<{ id?: number; soloLectura: boolean } | null>(null);
  const [ejercicioAbierto, setEjercicioAbierto] = useState<{ ejercicio?: EjercicioDto } | null>(null);

  const cargar = useCallback(async () => {
    setCargando(true);
    try {
      const [r, e] = await Promise.all([
        pedir<RutinaResumen[]>(`/api/rutinas?incluirInactivas=${verInactivos}`, "No se pudieron cargar las rutinas"),
        pedir<EjercicioDto[]>(`/api/ejercicios?incluirInactivos=${verInactivos}`, "No se pudieron cargar los ejercicios"),
      ]);
      setRutinas(r);
      setEjercicios(e);
    } catch (error: any) {
      toast.error(error.message);
    } finally {
      setCargando(false);
    }
  }, [verInactivos]);

  useEffect(() => { cargar(); }, [cargar]);

  const eliminarRutina = async (r: RutinaResumen) => {
    const aviso = r.sociosActivos > 0
      ? `"${r.nombre}" la tienen asignada ${r.sociosActivos} socio(s). Si se elimina, deja de poder asignarse pero sus rutinas siguen vigentes. ¿Continuar?`
      : `¿Eliminar la rutina "${r.nombre}"?`;
    if (!window.confirm(aviso)) return;
    try {
      const { accion } = await pedir<{ accion: string }>(`/api/rutinas/${r.id}`, "No se pudo eliminar la rutina", { method: "DELETE" });
      toast.success(accion === "eliminada" ? "Rutina eliminada" : "La rutina ya se había asignado: quedó desactivada");
      cargar();
    } catch (error: any) {
      toast.error(error.message);
    }
  };

  const eliminarEjercicio = async (e: EjercicioDto) => {
    if (!window.confirm(`¿Eliminar el ejercicio "${e.nombre}"?`)) return;
    try {
      const { accion } = await pedir<{ accion: string }>(`/api/ejercicios/${e.id}`, "No se pudo eliminar el ejercicio", { method: "DELETE" });
      toast.success(accion === "eliminado" ? "Ejercicio eliminado" : "El ejercicio está en rutinas: quedó desactivado");
      cargar();
    } catch (error: any) {
      toast.error(error.message);
    }
  };

  const pestanaClase = (p: Pestana) =>
    `-mb-px border-b-2 px-4 py-2 text-sm font-medium transition-colors ${pestana === p ? "border-marca-600 text-marca-700" : "border-transparent text-slate-500 hover:text-slate-700"}`;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-slate-500">
          Armá rutinas por día con sus ejercicios, series y repeticiones, y asignalas a los socios desde la pantalla de Socios.
        </p>
        {puedeAdministrar && (
          <button className={boton}
            onClick={() => (pestana === "rutinas" ? setRutinaAbierta({ soloLectura: false }) : setEjercicioAbierto({}))}>
            <Plus className="mr-2 h-4 w-4" />
            {pestana === "rutinas" ? "Nueva rutina" : "Nuevo ejercicio"}
          </button>
        )}
      </div>

      <div className="flex items-center justify-between border-b border-slate-200">
        <div className="flex">
          <button className={pestanaClase("rutinas")} onClick={() => setPestana("rutinas")}>Rutinas</button>
          <button className={pestanaClase("ejercicios")} onClick={() => setPestana("ejercicios")}>Ejercicios</button>
        </div>
        <label className="mb-1 flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" checked={verInactivos} onChange={(e) => setVerInactivos(e.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Ver desactivados
        </label>
      </div>

      <div className="rounded-md border border-slate-200 bg-white">
        <div className="overflow-x-auto">
          {cargando ? (
            <div className="p-8 text-center text-slate-500">Cargando...</div>
          ) : pestana === "rutinas" ? (
            <table className="w-full text-sm">
              <thead className="bg-slate-50/50">
                <tr className="border-b border-slate-200">
                  <th className={celdaTitulo}>Rutina</th>
                  <th className={celdaTitulo}>Nivel</th>
                  <th className={celdaTitulo}>Objetivo</th>
                  <th className={celdaTitulo}>Días</th>
                  <th className={celdaTitulo}>Ejercicios</th>
                  <th className={celdaTitulo}>Socios</th>
                  <th className={`${celdaTitulo} text-right`}>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {rutinas.length === 0 ? (
                  <tr><td colSpan={7} className="p-8 text-center text-slate-500">No hay rutinas cargadas.</td></tr>
                ) : rutinas.map((r) => (
                  <tr key={r.id} className="border-b border-slate-100 hover:bg-slate-50/50">
                    <td className="p-4 font-medium text-slate-900">
                      {r.nombre} {!r.activa && <Insignia clase="ml-1 border-slate-200 bg-slate-100 text-slate-500">Desactivada</Insignia>}
                    </td>
                    <td className="p-4"><Insignia clase={colorNivel[r.nivel] ?? ""}>{r.nivel}</Insignia></td>
                    <td className="max-w-xs truncate p-4 text-slate-600" title={r.objetivo}>{r.objetivo || "-"}</td>
                    <td className="p-4 text-slate-600">{r.dias}</td>
                    <td className="p-4 text-slate-600">{r.ejercicios}</td>
                    <td className="p-4 text-slate-600">{r.sociosActivos}</td>
                    <td className="p-4">
                      <div className="flex justify-end gap-2">
                        <button onClick={() => setRutinaAbierta({ id: r.id, soloLectura: true })} title="Ver" aria-label={`Ver ${r.nombre}`}
                          className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700"><Eye className="h-4 w-4" /></button>
                        {puedeAdministrar && (
                          <>
                            <button onClick={() => setRutinaAbierta({ id: r.id, soloLectura: false })} title="Editar" aria-label={`Editar ${r.nombre}`}
                              className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-blue-50 hover:text-blue-600"><Edit2 className="h-4 w-4" /></button>
                            <button onClick={() => eliminarRutina(r)} title="Eliminar" aria-label={`Eliminar ${r.nombre}`}
                              className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-red-50 hover:text-red-600"><Trash2 className="h-4 w-4" /></button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-slate-50/50">
                <tr className="border-b border-slate-200">
                  <th className={celdaTitulo}>Ejercicio</th>
                  <th className={celdaTitulo}>Grupo muscular</th>
                  <th className={celdaTitulo}>En rutinas</th>
                  {puedeAdministrar && <th className={`${celdaTitulo} text-right`}>Acciones</th>}
                </tr>
              </thead>
              <tbody>
                {ejercicios.length === 0 ? (
                  <tr><td colSpan={4} className="p-8 text-center text-slate-500">No hay ejercicios cargados.</td></tr>
                ) : ejercicios.map((e) => (
                  <tr key={e.id} className="border-b border-slate-100 hover:bg-slate-50/50">
                    <td className="p-4 font-medium text-slate-900">
                      {e.nombre} {!e.activo && <Insignia clase="ml-1 border-slate-200 bg-slate-100 text-slate-500">Desactivado</Insignia>}
                    </td>
                    <td className="p-4 text-slate-600">{e.grupoMuscular}</td>
                    <td className="p-4 text-slate-600">{e.enRutinas}</td>
                    {puedeAdministrar && (
                      <td className="p-4">
                        <div className="flex justify-end gap-2">
                          <button onClick={() => setEjercicioAbierto({ ejercicio: e })} title="Editar" aria-label={`Editar ${e.nombre}`}
                            className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-blue-50 hover:text-blue-600"><Edit2 className="h-4 w-4" /></button>
                          <button onClick={() => eliminarEjercicio(e)} title="Eliminar" aria-label={`Eliminar ${e.nombre}`}
                            className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-red-50 hover:text-red-600"><Trash2 className="h-4 w-4" /></button>
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {rutinaAbierta && (
        <RutinaForm rutinaId={rutinaAbierta.id} soloLectura={rutinaAbierta.soloLectura}
          onClose={(guardado) => { setRutinaAbierta(null); if (guardado) cargar(); }} />
      )}
      {ejercicioAbierto && (
        <EjercicioForm ejercicio={ejercicioAbierto.ejercicio}
          onClose={(guardado) => { setEjercicioAbierto(null); if (guardado) cargar(); }} />
      )}
    </div>
  );
}
