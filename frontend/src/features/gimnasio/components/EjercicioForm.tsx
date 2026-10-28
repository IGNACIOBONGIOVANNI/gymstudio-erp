import { useState } from "react";
import { Loader2, Save, X } from "lucide-react";
import toast from "react-hot-toast";
import { enviarJson, pedir, type EjercicioDto } from "../../../api/rutinasApi";

export const GRUPOS_MUSCULARES = ["Pecho", "Espalda", "Piernas", "Glúteos", "Hombros", "Brazos", "Abdomen", "Cardio", "Cuerpo completo"];

interface Props {
  ejercicio?: EjercicioDto | null;
  onClose: (guardado: boolean) => void;
}

export function EjercicioForm({ ejercicio, onClose }: Props) {
  const esEdicion = Boolean(ejercicio);
  const [nombre, setNombre] = useState(ejercicio?.nombre ?? "");
  const [grupo, setGrupo] = useState(ejercicio?.grupoMuscular ?? GRUPOS_MUSCULARES[0]);
  const [activo, setActivo] = useState(ejercicio?.activo ?? true);
  const [guardando, setGuardando] = useState(false);

  const guardar = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!nombre.trim()) return void toast.error("Ingresá el nombre del ejercicio");
    setGuardando(true);
    try {
      await pedir(
        esEdicion ? `/api/ejercicios/${ejercicio!.id}` : "/api/ejercicios",
        "No se pudo guardar el ejercicio",
        enviarJson(esEdicion ? "PUT" : "POST", { nombre, grupoMuscular: grupo, activo }),
      );
      toast.success(esEdicion ? "Ejercicio actualizado" : "Ejercicio creado");
      onClose(true);
    } catch (error: any) {
      toast.error(error.message);
    } finally {
      setGuardando(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
      <div className="w-full max-w-sm rounded-xl bg-white shadow-xl ring-1 ring-slate-900/5">
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-slate-900">{esEdicion ? "Editar ejercicio" : "Nuevo ejercicio"}</h2>
          <button onClick={() => onClose(false)} aria-label="Cerrar" className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-500">
            <X className="h-5 w-5" />
          </button>
        </div>
        <form onSubmit={guardar} className="space-y-4 p-6">
          <div>
            <label htmlFor="ej-nombre" className="block text-sm font-medium text-slate-700">Nombre</label>
            <input id="ej-nombre" autoFocus maxLength={100} value={nombre} onChange={(e) => setNombre(e.target.value)} placeholder="Ej: Sentadilla con barra"
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500" />
          </div>
          <div>
            <label htmlFor="ej-grupo" className="block text-sm font-medium text-slate-700">Grupo muscular</label>
            <select id="ej-grupo" value={grupo} onChange={(e) => setGrupo(e.target.value)}
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500">
              {GRUPOS_MUSCULARES.map((g) => <option key={g}>{g}</option>)}
            </select>
          </div>
          {esEdicion && (
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={activo} onChange={(e) => setActivo(e.target.checked)} className="h-4 w-4 rounded border-slate-300" />
              Disponible para armar rutinas
            </label>
          )}
          <div className="flex justify-end gap-3 border-t border-slate-100 pt-4">
            <button type="button" onClick={() => onClose(false)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancelar</button>
            <button type="submit" disabled={guardando} className="inline-flex items-center rounded-md bg-marca-600 px-4 py-2 text-sm font-medium text-white hover:bg-marca-700 disabled:opacity-50">
              {guardando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
              Guardar
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
