import { useCallback, useEffect, useMemo, useState } from "react";
import { Loader2, Pencil, Printer, Save, Trash2, X } from "lucide-react";
import toast from "react-hot-toast";
import { enviarJson, pedir } from "../../../api/rutinasApi";
import { METRICAS, formatearMedida, marcasDelEje, puntosDe, variacion, type ClaveMetrica, type Medicion } from "../../../api/medicionesApi";
import { useAuth } from "../../../context/AuthContext";
import { formatearFechaLocal, hoyLocal } from "../../../utils/formatters";
import { generarSeguimientoPDF } from "../../../utils/generarSeguimientoPDF";

interface Props {
  socio: { id: number | string; nombre: string; apellido: string };
  onClose: () => void;
}

const campo = "block w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500";
const VACIO = { fecha: "", pesoKg: "", grasaCorporalPct: "", cinturaCm: "", caderaCm: "", pechoCm: "", brazoCm: "", musloCm: "", notas: "" };
type Formulario = typeof VACIO;

const aNumero = (s: string) => (s.trim() === "" ? null : Number(s.replace(",", ".")));
const diaDelAnio = (f: string) => Date.UTC(Number(f.slice(0, 4)), Number(f.slice(5, 7)) - 1, Number(f.slice(8, 10))) / 86_400_000;

/** Línea de evolución de una medida. Una sola serie: el título del gráfico dice qué es, no hace falta leyenda. */
function GraficoEvolucion({ puntos, unidad, etiqueta }: { puntos: { fecha: string; valor: number }[]; unidad: string; etiqueta: string }) {
  const [activo, setActivo] = useState<number | null>(null);
  const ancho = 640, alto = 230, izq = 48, der = 24, arriba = 24, abajo = 30;

  const marcas = marcasDelEje(Math.min(...puntos.map((p) => p.valor)), Math.max(...puntos.map((p) => p.valor)));
  const minY = marcas[0], maxY = marcas[marcas.length - 1];
  const t0 = diaDelAnio(puntos[0].fecha), t1 = diaDelAnio(puntos[puntos.length - 1].fecha);
  const x = (f: string) => izq + (t1 === t0 ? (ancho - izq - der) / 2 : ((diaDelAnio(f) - t0) / (t1 - t0)) * (ancho - izq - der));
  const y = (v: number) => arriba + (1 - (v - minY) / (maxY - minY || 1)) * (alto - arriba - abajo);
  const camino = puntos.map((p, i) => `${i === 0 ? "M" : "L"}${x(p.fecha)},${y(p.valor)}`).join(" ");
  const ticksX = [0, 1 / 3, 2 / 3, 1].map((f) => {
    const d = new Date(Date.UTC(1970, 0, 1) + (t0 + (t1 - t0) * f) * 86_400_000);
    return { f, texto: `${String(d.getUTCDate()).padStart(2, "0")}/${String(d.getUTCMonth() + 1).padStart(2, "0")}` };
  });
  const ultimo = puntos.length - 1;

  return (
    <div className="relative">
      <svg viewBox={`0 0 ${ancho} ${alto}`} className="w-full" role="img" aria-label={`Evolución de ${etiqueta.toLowerCase()}: ${puntos.map((p) => `${formatearFechaLocal(p.fecha)} ${formatearMedida(p.valor)} ${unidad}`).join(", ")}`} onMouseLeave={() => setActivo(null)}>
        {marcas.map((m) => (
          <g key={m}>
            <line x1={izq} x2={ancho - der} y1={y(m)} y2={y(m)} stroke="#e4e6e2" strokeWidth={1} />
            <text x={izq - 8} y={y(m) + 4} textAnchor="end" fontSize={11} fill="#66706a">{String(m).replace(".", ",")}</text>
          </g>
        ))}
        {ticksX.map(({ f, texto }) => (
          <text key={f} x={izq + f * (ancho - izq - der)} y={alto - 8} textAnchor={f === 0 ? "start" : f === 1 ? "end" : "middle"} fontSize={11} fill="#66706a">{texto}</text>
        ))}
        {puntos.length > 1 && <path d={camino} fill="none" stroke="#2a78d6" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />}
        {puntos.map((p, i) => (
          <g key={p.fecha}>
            <circle cx={x(p.fecha)} cy={y(p.valor)} r={activo === i ? 6 : 4} fill="#2a78d6" stroke="#ffffff" strokeWidth={2} />
            {/* Solo se rotulan el primer y el último valor; el resto, en el tooltip y en la tabla. */}
            {(i === 0 || i === ultimo) && (
              <text x={x(p.fecha)} y={y(p.valor) - 10} textAnchor={i === 0 && ultimo > 0 ? "start" : "end"} fontSize={12} fontWeight={600} fill="#272e2a">{formatearMedida(p.valor)}</text>
            )}
            <circle cx={x(p.fecha)} cy={y(p.valor)} r={12} fill="transparent" tabIndex={0}
              onMouseEnter={() => setActivo(i)} onFocus={() => setActivo(i)} aria-label={`${formatearFechaLocal(p.fecha)}: ${formatearMedida(p.valor)} ${unidad}`} />
          </g>
        ))}
      </svg>
      {activo !== null && (
        <div className="pointer-events-none absolute z-10 -translate-x-1/2 -translate-y-full rounded-md border border-slate-200 bg-white px-2.5 py-1.5 text-xs shadow-md"
          style={{ left: `${(x(puntos[activo].fecha) / ancho) * 100}%`, top: `${(y(puntos[activo].valor) / alto) * 100 - 4}%` }}>
          <span className="font-semibold text-slate-800">{formatearFechaLocal(puntos[activo].fecha)}</span>
          <span className="ml-2 text-slate-600">{formatearMedida(puntos[activo].valor)} {unidad}</span>
        </div>
      )}
    </div>
  );
}

export function ModalSeguimientoSocio({ socio, onClose }: Props) {
  const { user } = useAuth();
  const puedeBorrar = user?.rol === "Administrador" || user?.rol === "Contador";
  const [mediciones, setMediciones] = useState<Medicion[]>([]);
  const [cargando, setCargando] = useState(true);
  const [guardando, setGuardando] = useState(false);
  const [metrica, setMetrica] = useState<ClaveMetrica>("pesoKg");
  const [form, setForm] = useState<Formulario>({ ...VACIO, fecha: hoyLocal() });
  const [editando, setEditando] = useState<number | null>(null);

  const cargar = useCallback(async () => {
    try {
      setMediciones(await pedir<Medicion[]>(`/api/socios/${socio.id}/mediciones`, "No se pudo cargar el seguimiento"));
    } catch (error: any) {
      toast.error(error.message);
    } finally {
      setCargando(false);
    }
  }, [socio.id]);

  useEffect(() => { cargar(); }, [cargar]);

  const disponibles = useMemo(() => METRICAS.filter((m) => puntosDe(mediciones, m.clave).length > 0), [mediciones]);
  const actual = METRICAS.find((m) => m.clave === metrica)!;
  const puntos = puntosDe(mediciones, metrica);
  const cambio = variacion(mediciones, metrica);

  const limpiar = () => { setForm({ ...VACIO, fecha: hoyLocal() }); setEditando(null); };

  const guardar = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.fecha) return void toast.error("Indicá la fecha de la medición");
    const peso = aNumero(form.pesoKg);
    if (peso === null || !Number.isFinite(peso)) return void toast.error("Ingresá el peso");
    for (const m of METRICAS) {
      const v = aNumero(form[m.clave]);
      if (v !== null && (!Number.isFinite(v) || v <= 0)) return void toast.error(`${m.etiqueta}: ingresá un número válido`);
    }

    setGuardando(true);
    try {
      const cuerpo = {
        fecha: form.fecha, pesoKg: peso, grasaCorporalPct: aNumero(form.grasaCorporalPct), cinturaCm: aNumero(form.cinturaCm),
        caderaCm: aNumero(form.caderaCm), pechoCm: aNumero(form.pechoCm), brazoCm: aNumero(form.brazoCm), musloCm: aNumero(form.musloCm), notas: form.notas,
      };
      await pedir(editando ? `/api/mediciones/${editando}` : `/api/socios/${socio.id}/mediciones`, "No se pudo guardar la medición", enviarJson(editando ? "PUT" : "POST", cuerpo));
      toast.success(editando ? "Medición actualizada" : "Medición guardada");
      limpiar();
      await cargar();
    } catch (error: any) {
      toast.error(error.message);
    } finally {
      setGuardando(false);
    }
  };

  const editar = (m: Medicion) => {
    setEditando(m.id);
    setForm({
      fecha: m.fecha, pesoKg: String(m.pesoKg), grasaCorporalPct: m.grasaCorporalPct?.toString() ?? "", cinturaCm: m.cinturaCm?.toString() ?? "",
      caderaCm: m.caderaCm?.toString() ?? "", pechoCm: m.pechoCm?.toString() ?? "", brazoCm: m.brazoCm?.toString() ?? "", musloCm: m.musloCm?.toString() ?? "", notas: m.notas,
    });
  };

  const eliminar = async (m: Medicion) => {
    if (!window.confirm(`¿Eliminar la medición del ${formatearFechaLocal(m.fecha)}?`)) return;
    try {
      await pedir(`/api/mediciones/${m.id}`, "No se pudo eliminar la medición", { method: "DELETE" });
      toast.success("Medición eliminada");
      if (editando === m.id) limpiar();
      await cargar();
    } catch (error: any) {
      toast.error(error.message);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
      <div className="flex max-h-[92vh] w-full max-w-4xl flex-col rounded-xl bg-white shadow-xl ring-1 ring-slate-900/5">
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <div>
            <h2 className="text-lg font-semibold text-slate-900">Seguimiento de {socio.nombre} {socio.apellido}</h2>
            <p className="text-sm text-slate-500">Peso y medidas a lo largo del tiempo.</p>
          </div>
          <div className="flex items-center gap-1">
            {mediciones.length > 0 && (
              <button onClick={() => generarSeguimientoPDF(socio, mediciones)} className="mr-2 inline-flex items-center rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50">
                <Printer className="mr-1.5 h-4 w-4" /> Imprimir PDF
              </button>
            )}
            <button onClick={onClose} aria-label="Cerrar" className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-500"><X className="h-5 w-5" /></button>
          </div>
        </div>

        {cargando ? (
          <div className="p-10 text-center text-slate-500">Cargando...</div>
        ) : (
          <div className="space-y-6 overflow-y-auto p-6">
            <form onSubmit={guardar} className="rounded-lg border border-slate-200 p-4">
              <h3 className="mb-3 text-sm font-semibold text-slate-800">{editando ? "Corregir medición" : "Nueva medición"}</h3>
              <div className="grid gap-3 sm:grid-cols-4">
                <div>
                  <label htmlFor="me-fecha" className="mb-1 block text-xs font-medium text-slate-500">Fecha</label>
                  <input id="me-fecha" type="date" value={form.fecha} max={hoyLocal()} onChange={(e) => setForm({ ...form, fecha: e.target.value })} className={campo} />
                </div>
                {METRICAS.map((m) => (
                  <div key={m.clave}>
                    <label htmlFor={`me-${m.clave}`} className="mb-1 block text-xs font-medium text-slate-500">
                      {m.etiqueta} ({m.unidad}){m.obligatoria ? "" : " - opcional"}
                    </label>
                    <input id={`me-${m.clave}`} inputMode="decimal" value={form[m.clave]} onChange={(e) => setForm({ ...form, [m.clave]: e.target.value })} placeholder={m.obligatoria ? "Ej: 80,5" : ""} className={campo} />
                  </div>
                ))}
                <div className="sm:col-span-4">
                  <label htmlFor="me-notas" className="mb-1 block text-xs font-medium text-slate-500">Notas (opcional)</label>
                  <input id="me-notas" maxLength={300} value={form.notas} onChange={(e) => setForm({ ...form, notas: e.target.value })} placeholder="Ej: medido en ayunas" className={campo} />
                </div>
              </div>
              <div className="mt-4 flex justify-end gap-3">
                {editando && <button type="button" onClick={limpiar} className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancelar</button>}
                <button type="submit" disabled={guardando} className="inline-flex items-center rounded-md bg-marca-600 px-4 py-2 text-sm font-medium text-white hover:bg-marca-700 disabled:opacity-50">
                  {guardando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  {editando ? "Guardar cambios" : "Guardar medición"}
                </button>
              </div>
            </form>

            {mediciones.length === 0 ? (
              <p className="rounded-lg border border-dashed border-slate-300 p-4 text-sm text-slate-500">Todavía no hay mediciones de este socio. Cargá la primera arriba.</p>
            ) : (
              <>
                <section className="rounded-lg border border-slate-200 p-4">
                  <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                    <h3 className="text-sm font-semibold text-slate-800">Evolución de {actual.etiqueta.toLowerCase()} ({actual.unidad})</h3>
                    <div className="flex flex-wrap gap-1.5" role="group" aria-label="Medida a graficar">
                      {disponibles.map((m) => (
                        <button key={m.clave} type="button" onClick={() => setMetrica(m.clave)} aria-pressed={metrica === m.clave}
                          className={`rounded-full border px-2.5 py-0.5 text-xs font-medium ${metrica === m.clave ? "border-marca-600 bg-marca-50 text-marca-700" : "border-slate-300 text-slate-600 hover:bg-slate-50"}`}>
                          {m.etiqueta}
                        </button>
                      ))}
                    </div>
                  </div>
                  {puntos.length < 2 ? (
                    <p className="text-sm text-slate-500">Cargá al menos dos mediciones de {actual.etiqueta.toLowerCase()} para ver cómo evoluciona.</p>
                  ) : (
                    <>
                      <GraficoEvolucion puntos={puntos} unidad={actual.unidad} etiqueta={actual.etiqueta} />
                      {cambio && (
                        <p className="mt-2 text-sm text-slate-600">
                          De {formatearMedida(cambio.inicial)} a {formatearMedida(cambio.actual)} {actual.unidad} desde el {formatearFechaLocal(cambio.desde)}:{" "}
                          <span className="font-semibold text-slate-900">
                            {cambio.diferencia === 0 ? "sin cambios" : `${cambio.diferencia > 0 ? "+" : "-"}${formatearMedida(Math.abs(cambio.diferencia))} ${actual.unidad}`}
                          </span>
                        </p>
                      )}
                    </>
                  )}
                </section>

                <section className="overflow-x-auto rounded-lg border border-slate-200">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50 text-left text-slate-500">
                      <tr>
                        <th className="px-3 py-2 font-medium">Fecha</th>
                        {METRICAS.map((m) => <th key={m.clave} className="px-3 py-2 text-right font-medium">{m.etiqueta}</th>)}
                        <th className="px-3 py-2 font-medium">Notas</th>
                        <th className="px-3 py-2" />
                      </tr>
                    </thead>
                    <tbody>
                      {[...mediciones].reverse().map((m) => (
                        <tr key={m.id} className={`border-t border-slate-100 ${editando === m.id ? "bg-amber-50" : ""}`}>
                          <td className="px-3 py-2 text-slate-900">{formatearFechaLocal(m.fecha)}</td>
                          {METRICAS.map((x) => <td key={x.clave} className="px-3 py-2 text-right text-slate-600">{formatearMedida(m[x.clave])}</td>)}
                          <td className="max-w-[12rem] truncate px-3 py-2 text-slate-500" title={m.notas}>{m.notas}</td>
                          <td className="px-3 py-2">
                            <div className="flex justify-end gap-1">
                              <button onClick={() => editar(m)} title="Corregir" aria-label={`Corregir la medición del ${formatearFechaLocal(m.fecha)}`} className="rounded-md p-1.5 text-slate-400 hover:bg-blue-50 hover:text-blue-600"><Pencil className="h-4 w-4" /></button>
                              {puedeBorrar && <button onClick={() => eliminar(m)} title="Eliminar" aria-label={`Eliminar la medición del ${formatearFechaLocal(m.fecha)}`} className="rounded-md p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600"><Trash2 className="h-4 w-4" /></button>}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </section>
              </>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
