import { useCallback, useEffect, useState } from "react";
import { Loader2, Printer, X } from "lucide-react";
import toast from "react-hot-toast";
import {
  enviarJson, estadoDeAsignacion, pedir,
  type AsignacionDetalle, type AsignacionResumen, type RutinaResumen,
} from "../../../api/rutinasApi";
import { formatearFechaLocal, hoyLocal } from "../../../utils/formatters";
import { generarRutinaPDF } from "../../../utils/generarRutinaPDF";

interface Props {
  socio: { id: number | string; nombre: string; apellido: string };
  onClose: () => void;
}

const campo = "block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500";

const colorEstado = {
  Vigente: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Vencida: "border-amber-200 bg-amber-50 text-amber-700",
  Finalizada: "border-slate-200 bg-slate-100 text-slate-600",
} as const;

export function ModalRutinaSocio({ socio, onClose }: Props) {
  const hoy = hoyLocal();
  const [historial, setHistorial] = useState<AsignacionResumen[]>([]);
  const [rutinas, setRutinas] = useState<RutinaResumen[]>([]);
  const [cargando, setCargando] = useState(true);
  const [trabajando, setTrabajando] = useState(false);

  const [rutinaId, setRutinaId] = useState("");
  const [inicio, setInicio] = useState(hoy);
  const [fin, setFin] = useState("");
  const [observaciones, setObservaciones] = useState("");

  const cargar = useCallback(async () => {
    try {
      const [h, r] = await Promise.all([
        pedir<AsignacionResumen[]>(`/api/socios/${socio.id}/rutinas`, "No se pudo cargar el historial de rutinas"),
        pedir<RutinaResumen[]>("/api/rutinas", "No se pudieron cargar las rutinas"),
      ]);
      setHistorial(h);
      setRutinas(r);
    } catch (error: any) {
      toast.error(error.message);
    } finally {
      setCargando(false);
    }
  }, [socio.id]);

  useEffect(() => { cargar(); }, [cargar]);

  const actual = historial.find((a) => a.activa);

  const imprimir = async (id: number) => {
    try {
      generarRutinaPDF(await pedir<AsignacionDetalle>(`/api/asignaciones-rutina/${id}`, "No se pudo armar el PDF de la rutina"));
    } catch (error: any) {
      toast.error(error.message);
    }
  };

  const asignar = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!rutinaId) return void toast.error("Elegí la rutina");
    if (!inicio) return void toast.error("Indicá la fecha de inicio");
    if (fin && fin < inicio) return void toast.error("El vencimiento no puede ser anterior al inicio");
    setTrabajando(true);
    try {
      await pedir(`/api/socios/${socio.id}/rutinas`, "No se pudo asignar la rutina",
        enviarJson("POST", { rutinaId: Number(rutinaId), fechaInicio: inicio, fechaFin: fin || null, observaciones }));
      toast.success("Rutina asignada");
      setRutinaId(""); setFin(""); setObservaciones("");
      await cargar();
    } catch (error: any) {
      toast.error(error.message);
    } finally {
      setTrabajando(false);
    }
  };

  const finalizar = async (a: AsignacionResumen) => {
    if (!window.confirm(`¿Finalizar hoy la rutina "${a.rutinaNombre}" de ${socio.apellido} ${socio.nombre}?`)) return;
    setTrabajando(true);
    try {
      await pedir(`/api/asignaciones-rutina/${a.id}/finalizar`, "No se pudo finalizar la rutina", enviarJson("POST", { fecha: hoy }));
      toast.success("Rutina finalizada");
      await cargar();
    } catch (error: any) {
      toast.error(error.message);
    } finally {
      setTrabajando(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
      <div className="flex max-h-[92vh] w-full max-w-3xl flex-col rounded-xl bg-white shadow-xl ring-1 ring-slate-900/5">
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <div>
            <h2 className="text-lg font-semibold text-slate-900">Rutina de {socio.nombre} {socio.apellido}</h2>
            <p className="text-sm text-slate-500">Asigná, imprimí y consultá las rutinas de este socio.</p>
          </div>
          <button onClick={onClose} aria-label="Cerrar" className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-500">
            <X className="h-5 w-5" />
          </button>
        </div>

        {cargando ? (
          <div className="p-10 text-center text-slate-500">Cargando...</div>
        ) : (
          <div className="space-y-6 overflow-y-auto p-6">
            <section>
              <h3 className="mb-2 text-sm font-semibold text-slate-800">Rutina actual</h3>
              {actual ? (() => {
                const estado = estadoDeAsignacion(actual, hoy);
                return (
                  <div className="rounded-lg border border-slate-200 p-4">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <p className="font-medium text-slate-900">
                          {actual.rutinaNombre}{" "}
                          <span className={`ml-1 inline-flex rounded-full border px-2.5 py-0.5 text-xs font-medium ${colorEstado[estado]}`}>{estado}</span>
                        </p>
                        <p className="mt-1 text-sm text-slate-500">
                          Nivel {actual.nivel} · desde {formatearFechaLocal(actual.fechaInicio)}
                          {actual.fechaFin ? ` · vence ${formatearFechaLocal(actual.fechaFin)}` : " · sin vencimiento"}
                        </p>
                        {actual.observaciones && <p className="mt-2 text-sm text-slate-600">{actual.observaciones}</p>}
                      </div>
                      <div className="flex gap-2">
                        <button onClick={() => imprimir(actual.id)} className="inline-flex items-center rounded-md bg-marca-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-marca-700">
                          <Printer className="mr-1.5 h-4 w-4" /> Imprimir PDF
                        </button>
                        <button onClick={() => finalizar(actual)} disabled={trabajando} className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                          Finalizar
                        </button>
                      </div>
                    </div>
                  </div>
                );
              })() : (
                <p className="rounded-lg border border-dashed border-slate-300 p-4 text-sm text-slate-500">Este socio todavía no tiene una rutina asignada.</p>
              )}
            </section>

            <section>
              <h3 className="mb-2 text-sm font-semibold text-slate-800">{actual ? "Cambiar de rutina" : "Asignar rutina"}</h3>
              <form onSubmit={asignar} className="grid gap-3 rounded-lg border border-slate-200 p-4 sm:grid-cols-6">
                <div className="sm:col-span-3">
                  <label htmlFor="as-rutina" className="mb-1 block text-xs font-medium text-slate-500">Rutina</label>
                  <select id="as-rutina" value={rutinaId} onChange={(e) => setRutinaId(e.target.value)} className={campo}>
                    <option value="">Elegí una rutina</option>
                    {rutinas.map((r) => <option key={r.id} value={r.id}>{r.nombre} ({r.nivel})</option>)}
                  </select>
                </div>
                <div className="sm:col-span-2">
                  <label htmlFor="as-inicio" className="mb-1 block text-xs font-medium text-slate-500">Desde</label>
                  <input id="as-inicio" type="date" value={inicio} onChange={(e) => setInicio(e.target.value)} className={campo} />
                </div>
                <div className="sm:col-span-1">
                  <label htmlFor="as-fin" className="mb-1 block text-xs font-medium text-slate-500">Hasta</label>
                  <input id="as-fin" type="date" value={fin} min={inicio} onChange={(e) => setFin(e.target.value)} className={campo} />
                </div>
                <div className="sm:col-span-6">
                  <label htmlFor="as-obs" className="mb-1 block text-xs font-medium text-slate-500">Observaciones del profesor (opcional)</label>
                  <input id="as-obs" maxLength={500} value={observaciones} onChange={(e) => setObservaciones(e.target.value)} placeholder="Ej: cuidar la rodilla izquierda" className={campo} />
                </div>
                <div className="flex items-center justify-between sm:col-span-6">
                  <p className="text-xs text-slate-500">{actual ? "La rutina actual se cierra el día que empieza la nueva." : "Sin fecha de vencimiento, la rutina no vence."}</p>
                  <button type="submit" disabled={trabajando} className="inline-flex items-center rounded-md bg-marca-600 px-4 py-2 text-sm font-medium text-white hover:bg-marca-700 disabled:opacity-50">
                    {trabajando && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    {actual ? "Cambiar rutina" : "Asignar rutina"}
                  </button>
                </div>
              </form>
            </section>

            {historial.length > 0 && (
              <section>
                <h3 className="mb-2 text-sm font-semibold text-slate-800">Historial</h3>
                <div className="overflow-x-auto rounded-lg border border-slate-200">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50 text-left text-slate-500">
                      <tr>
                        <th className="px-3 py-2 font-medium">Rutina</th>
                        <th className="px-3 py-2 font-medium">Desde</th>
                        <th className="px-3 py-2 font-medium">Hasta</th>
                        <th className="px-3 py-2 font-medium">Estado</th>
                        <th className="px-3 py-2" />
                      </tr>
                    </thead>
                    <tbody>
                      {historial.map((a) => {
                        const estado = estadoDeAsignacion(a, hoy);
                        return (
                          <tr key={a.id} className="border-t border-slate-100">
                            <td className="px-3 py-2 text-slate-900">{a.rutinaNombre}</td>
                            <td className="px-3 py-2 text-slate-600">{formatearFechaLocal(a.fechaInicio)}</td>
                            <td className="px-3 py-2 text-slate-600">{a.fechaFin ? formatearFechaLocal(a.fechaFin) : "-"}</td>
                            <td className="px-3 py-2"><span className={`inline-flex rounded-full border px-2 py-0.5 text-xs font-medium ${colorEstado[estado]}`}>{estado}</span></td>
                            <td className="px-3 py-2 text-right">
                              <button onClick={() => imprimir(a.id)} title="Imprimir PDF" aria-label={`Imprimir ${a.rutinaNombre}`} className="rounded-md p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700">
                                <Printer className="h-4 w-4" />
                              </button>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </section>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
