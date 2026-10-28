import { useEffect, useMemo, useState } from "react";
import { Loader2, Plus, Save, Trash2, X } from "lucide-react";
import toast from "react-hot-toast";
import { enviarJson, pedir, type EjercicioDto, type RutinaDetalle } from "../../../api/rutinasApi";
import { GRUPOS_MUSCULARES } from "./EjercicioForm";

const NIVELES = ["Principiante", "Intermedio", "Avanzado"];
const MAX_DIAS = 7;

interface Renglon {
  clave: number;
  ejercicioId: string;
  series: string;
  repeticiones: string;
  descanso: string;
  indicaciones: string;
}

interface Props {
  /** Si viene un id se edita (o se mira, con soloLectura); si no, se crea una rutina nueva. */
  rutinaId?: number | null;
  soloLectura?: boolean;
  onClose: (guardado: boolean) => void;
}

let contador = 0;
const renglonVacio = (): Renglon => ({ clave: ++contador, ejercicioId: "", series: "3", repeticiones: "10-12", descanso: "60", indicaciones: "" });

const campo = "block w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:bg-slate-50 disabled:text-slate-600";

export function RutinaForm({ rutinaId, soloLectura = false, onClose }: Props) {
  const esEdicion = rutinaId != null;
  const [cargando, setCargando] = useState(true);
  const [guardando, setGuardando] = useState(false);
  const [catalogo, setCatalogo] = useState<EjercicioDto[]>([]);
  const [nombre, setNombre] = useState("");
  const [objetivo, setObjetivo] = useState("");
  const [nivel, setNivel] = useState(NIVELES[0]);
  const [activa, setActiva] = useState(true);
  const [dias, setDias] = useState<Renglon[][]>([[renglonVacio()]]);

  useEffect(() => {
    (async () => {
      try {
        const ejercicios = await pedir<EjercicioDto[]>("/api/ejercicios?incluirInactivos=true", "No se pudo cargar el catálogo de ejercicios");
        setCatalogo(ejercicios);
        if (rutinaId != null) {
          const r = await pedir<RutinaDetalle>(`/api/rutinas/${rutinaId}`, "No se pudo cargar la rutina");
          setNombre(r.nombre);
          setObjetivo(r.objetivo);
          setNivel(r.nivel);
          setActiva(r.activa);
          const maximo = Math.max(1, ...r.ejercicios.map((e) => e.dia));
          setDias(Array.from({ length: maximo }, (_, i) =>
            r.ejercicios.filter((e) => e.dia === i + 1).map((e) => ({
              clave: ++contador,
              ejercicioId: String(e.ejercicioId),
              series: String(e.series),
              repeticiones: e.repeticiones,
              descanso: String(e.descansoSegundos),
              indicaciones: e.indicaciones,
            })),
          ));
        }
      } catch (error: any) {
        toast.error(error.message);
        onClose(false);
      } finally {
        setCargando(false);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rutinaId]);

  const porGrupo = useMemo(
    () => GRUPOS_MUSCULARES.map((g) => ({ grupo: g, items: catalogo.filter((e) => e.grupoMuscular === g) })).filter((x) => x.items.length > 0),
    [catalogo],
  );

  const cambiar = (dia: number, clave: number, cambios: Partial<Renglon>) =>
    setDias((actual) => actual.map((renglones, i) => (i === dia ? renglones.map((r) => (r.clave === clave ? { ...r, ...cambios } : r)) : renglones)));

  const guardar = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!nombre.trim()) return void toast.error("Ingresá el nombre de la rutina");

    const ejercicios: unknown[] = [];
    for (let d = 0; d < dias.length; d++) {
      if (dias[d].length === 0) return void toast.error(`El día ${d + 1} no tiene ejercicios: agregá uno o quitá el día`);
      for (const r of dias[d]) {
        const series = Number(r.series);
        const descanso = Number(r.descanso);
        if (!r.ejercicioId) return void toast.error(`Elegí el ejercicio de cada renglón (día ${d + 1})`);
        if (!Number.isInteger(series) || series < 1 || series > 20) return void toast.error(`Las series del día ${d + 1} deben estar entre 1 y 20`);
        if (!r.repeticiones.trim()) return void toast.error(`Indicá las repeticiones del día ${d + 1}`);
        if (!Number.isInteger(descanso) || descanso < 0 || descanso > 600) return void toast.error(`El descanso del día ${d + 1} debe estar entre 0 y 600 segundos`);
        ejercicios.push({ ejercicioId: Number(r.ejercicioId), dia: d + 1, series, repeticiones: r.repeticiones.trim(), descansoSegundos: descanso, indicaciones: r.indicaciones.trim() });
      }
    }

    setGuardando(true);
    try {
      await pedir(
        esEdicion ? `/api/rutinas/${rutinaId}` : "/api/rutinas",
        "No se pudo guardar la rutina",
        enviarJson(esEdicion ? "PUT" : "POST", { nombre, objetivo, nivel, activa, ejercicios }),
      );
      toast.success(esEdicion ? "Rutina actualizada" : "Rutina creada");
      onClose(true);
    } catch (error: any) {
      toast.error(error.message);
    } finally {
      setGuardando(false);
    }
  };

  const titulo = soloLectura ? "Rutina" : esEdicion ? "Editar rutina" : "Nueva rutina";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
      <div className="flex max-h-[92vh] w-full max-w-4xl flex-col rounded-xl bg-white shadow-xl ring-1 ring-slate-900/5">
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-slate-900">{titulo}</h2>
          <button onClick={() => onClose(false)} aria-label="Cerrar" className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-500">
            <X className="h-5 w-5" />
          </button>
        </div>

        {cargando ? (
          <div className="p-10 text-center text-slate-500">Cargando...</div>
        ) : (
          <form onSubmit={guardar} className="flex min-h-0 flex-1 flex-col">
            <div className="min-h-0 flex-1 space-y-5 overflow-y-auto p-6">
              <div className="grid gap-4 sm:grid-cols-3">
                <div className="sm:col-span-2">
                  <label htmlFor="ru-nombre" className="mb-1 block text-sm font-medium text-slate-700">Nombre</label>
                  <input id="ru-nombre" disabled={soloLectura} maxLength={100} value={nombre} onChange={(e) => setNombre(e.target.value)} placeholder="Ej: Full body principiante" className={campo} />
                </div>
                <div>
                  <label htmlFor="ru-nivel" className="mb-1 block text-sm font-medium text-slate-700">Nivel</label>
                  <select id="ru-nivel" disabled={soloLectura} value={nivel} onChange={(e) => setNivel(e.target.value)} className={campo}>
                    {NIVELES.map((n) => <option key={n}>{n}</option>)}
                  </select>
                </div>
                <div className="sm:col-span-3">
                  <label htmlFor="ru-objetivo" className="mb-1 block text-sm font-medium text-slate-700">Objetivo</label>
                  <input id="ru-objetivo" disabled={soloLectura} maxLength={200} value={objetivo} onChange={(e) => setObjetivo(e.target.value)} placeholder="Ej: Ganar fuerza y masa muscular" className={campo} />
                </div>
              </div>

              {dias.map((renglones, d) => (
                <section key={d} className="rounded-lg border border-slate-200">
                  <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-4 py-2">
                    <h3 className="text-sm font-semibold text-slate-800">Día {d + 1}</h3>
                    {!soloLectura && dias.length > 1 && (
                      <button type="button" onClick={() => setDias((a) => a.filter((_, i) => i !== d))} className="text-xs font-medium text-red-600 hover:text-red-700">
                        Quitar día
                      </button>
                    )}
                  </div>
                  <div className="space-y-3 p-4">
                    {renglones.length === 0 && <p className="text-sm text-slate-500">Todavía no hay ejercicios en este día.</p>}
                    {renglones.map((r) => (
                      <div key={r.clave} className="grid grid-cols-2 items-end gap-2 sm:grid-cols-12">
                        <div className="col-span-2 sm:col-span-4">
                          <label className="mb-1 block text-xs font-medium text-slate-500">Ejercicio</label>
                          <select disabled={soloLectura} value={r.ejercicioId} onChange={(e) => cambiar(d, r.clave, { ejercicioId: e.target.value })} className={campo} aria-label={`Ejercicio del día ${d + 1}`}>
                            <option value="">Elegí un ejercicio</option>
                            {porGrupo.map(({ grupo, items }) => (
                              <optgroup key={grupo} label={grupo}>
                                {items.filter((x) => x.activo || String(x.id) === r.ejercicioId).map((x) => (
                                  <option key={x.id} value={x.id}>{x.nombre}{x.activo ? "" : " (desactivado)"}</option>
                                ))}
                              </optgroup>
                            ))}
                          </select>
                        </div>
                        <div className="sm:col-span-1">
                          <label className="mb-1 block text-xs font-medium text-slate-500">Series</label>
                          <input type="number" min={1} max={20} disabled={soloLectura} value={r.series} onChange={(e) => cambiar(d, r.clave, { series: e.target.value })} className={campo} />
                        </div>
                        <div className="sm:col-span-2">
                          <label className="mb-1 block text-xs font-medium text-slate-500">Repeticiones</label>
                          <input maxLength={20} disabled={soloLectura} value={r.repeticiones} onChange={(e) => cambiar(d, r.clave, { repeticiones: e.target.value })} placeholder="10-12" className={campo} />
                        </div>
                        <div className="sm:col-span-2">
                          <label className="mb-1 block text-xs font-medium text-slate-500">Descanso (seg)</label>
                          <input type="number" min={0} max={600} disabled={soloLectura} value={r.descanso} onChange={(e) => cambiar(d, r.clave, { descanso: e.target.value })} className={campo} />
                        </div>
                        <div className="col-span-2 sm:col-span-2">
                          <label className="mb-1 block text-xs font-medium text-slate-500">Indicaciones</label>
                          <input maxLength={200} disabled={soloLectura} value={r.indicaciones} onChange={(e) => cambiar(d, r.clave, { indicaciones: e.target.value })} className={campo} />
                        </div>
                        {!soloLectura && (
                          <div className="sm:col-span-1">
                            <button type="button" aria-label="Quitar ejercicio" title="Quitar ejercicio"
                              onClick={() => setDias((a) => a.map((rs, i) => (i === d ? rs.filter((x) => x.clave !== r.clave) : rs)))}
                              className="rounded-md p-2 text-slate-400 hover:bg-red-50 hover:text-red-600">
                              <Trash2 className="h-4 w-4" />
                            </button>
                          </div>
                        )}
                      </div>
                    ))}
                    {!soloLectura && (
                      <button type="button" onClick={() => setDias((a) => a.map((rs, i) => (i === d ? [...rs, renglonVacio()] : rs)))}
                        className="inline-flex items-center text-sm font-medium text-marca-700 hover:text-marca-800">
                        <Plus className="mr-1 h-4 w-4" /> Agregar ejercicio
                      </button>
                    )}
                  </div>
                </section>
              ))}

              {!soloLectura && dias.length < MAX_DIAS && (
                <button type="button" onClick={() => setDias((a) => [...a, [renglonVacio()]])}
                  className="inline-flex items-center rounded-md border border-dashed border-slate-300 px-3 py-2 text-sm font-medium text-slate-600 hover:border-slate-400 hover:bg-slate-50">
                  <Plus className="mr-1 h-4 w-4" /> Agregar día
                </button>
              )}

              {!soloLectura && esEdicion && (
                <label className="flex items-center gap-2 text-sm text-slate-700">
                  <input type="checkbox" checked={activa} onChange={(e) => setActiva(e.target.checked)} className="h-4 w-4 rounded border-slate-300" />
                  Disponible para asignar a socios
                </label>
              )}
            </div>

            <div className="flex justify-end gap-3 border-t border-slate-100 px-6 py-4">
              <button type="button" onClick={() => onClose(false)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
                {soloLectura ? "Cerrar" : "Cancelar"}
              </button>
              {!soloLectura && (
                <button type="submit" disabled={guardando} className="inline-flex items-center rounded-md bg-marca-600 px-4 py-2 text-sm font-medium text-white hover:bg-marca-700 disabled:opacity-50">
                  {guardando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
                  Guardar rutina
                </button>
              )}
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
