import { useEffect, useRef, useState } from "react";
import { Loader2, MessageCircle, Send, X } from "lucide-react";
import { enviarJson, pedir } from "../api/rutinasApi";
import { useAuth } from "../context/AuthContext";
import { hoyLocal } from "../utils/formatters";

interface Fila { etiqueta: string; valor: string }
interface Respuesta { texto: string; filas: Fila[]; sugerencias: string[] }
interface Mensaje { de: "yo" | "asistente"; texto: string; filas?: Fila[]; error?: boolean }

const SUGERENCIAS_INICIALES: Record<string, string[]> = {
  gestion: ["¿Cuánto vendí este mes?", "¿Quién me debe?", "¿Está todo bien con la contabilidad?", "¿Qué productos están sin stock?"],
  recepcion: ["¿Cuántas cuotas vencen esta semana?", "¿Qué socios tienen la cuota vencida?", "¿Quiénes no tienen rutina?", "¿Qué productos están sin stock?"],
};

/**
 * Asistente por reglas: contesta preguntas frecuentes con los datos del sistema, sin internet ni IA externa.
 * El servidor decide qué puede contestar según el rol del usuario.
 */
export function Asistente() {
  const { user } = useAuth();
  const esGestion = user?.rol === "Administrador" || user?.rol === "Contador";
  const [abierto, setAbierto] = useState(false);
  const [texto, setTexto] = useState("");
  const [pensando, setPensando] = useState(false);
  const [mensajes, setMensajes] = useState<Mensaje[]>([]);
  const [sugerencias, setSugerencias] = useState<string[]>(SUGERENCIAS_INICIALES[esGestion ? "gestion" : "recepcion"]);
  const entrada = useRef<HTMLInputElement>(null);
  const fin = useRef<HTMLDivElement>(null);

  useEffect(() => { if (abierto) entrada.current?.focus(); }, [abierto]);
  useEffect(() => { fin.current?.scrollIntoView({ block: "end" }); }, [mensajes, pensando]);

  const preguntar = async (pregunta: string) => {
    const limpia = pregunta.trim();
    if (!limpia || pensando) return;
    setMensajes((m) => [...m, { de: "yo", texto: limpia }]);
    setTexto("");
    setPensando(true);
    try {
      const r = await pedir<Respuesta>("/api/asistente/preguntar", "No pude contestar en este momento", enviarJson("POST", { texto: limpia, hoy: hoyLocal() }));
      setMensajes((m) => [...m, { de: "asistente", texto: r.texto, filas: r.filas }]);
      setSugerencias(r.sugerencias);
    } catch (error: any) {
      setMensajes((m) => [...m, { de: "asistente", texto: error.message, error: true }]);
    } finally {
      setPensando(false);
      entrada.current?.focus();
    }
  };

  return (
    <div className="print:hidden">
      {abierto && (
        <section
          role="dialog"
          aria-label="Asistente del sistema"
          onKeyDown={(e) => { if (e.key === "Escape") setAbierto(false); }}
          className="fixed bottom-20 right-4 z-40 flex max-h-[min(34rem,calc(100vh-7rem))] w-[calc(100vw-2rem)] flex-col overflow-hidden rounded-xl bg-white shadow-2xl ring-1 ring-slate-900/10 sm:w-96"
        >
          <header className="flex items-center justify-between bg-slate-900 px-4 py-3 text-white">
            <div>
              <h2 className="text-sm font-semibold">Asistente</h2>
              <p className="text-xs text-slate-400">Responde con los datos del sistema. No usa internet.</p>
            </div>
            <button onClick={() => setAbierto(false)} aria-label="Cerrar el asistente" className="rounded-full p-1.5 text-slate-300 hover:bg-slate-800 hover:text-white">
              <X className="h-4 w-4" />
            </button>
          </header>

          <div className="flex-1 space-y-3 overflow-y-auto bg-slate-50 p-4" aria-live="polite">
            {mensajes.length === 0 && (
              <p className="rounded-lg bg-white p-3 text-sm text-slate-700 shadow-sm ring-1 ring-slate-200">
                Hola, preguntame lo que quieras saber del negocio: por ejemplo, ventas de un mes, quién te debe, el stock de un producto o cuántas cuotas vencen.
                Escribí <span className="font-medium">ayuda</span> para ver todo lo que puedo contestar.
              </p>
            )}
            {mensajes.map((m, i) => (
              <div key={i} className={m.de === "yo" ? "flex justify-end" : "flex justify-start"}>
                <div className={`max-w-[88%] rounded-lg px-3 py-2 text-sm shadow-sm ${m.de === "yo" ? "bg-marca-600 text-white" : m.error ? "bg-amber-50 text-amber-900 ring-1 ring-amber-200" : "bg-white text-slate-800 ring-1 ring-slate-200"}`}>
                  <p>{m.texto}</p>
                  {m.filas && m.filas.length > 0 && (
                    <table className="mt-2 w-full text-xs">
                      <tbody>
                        {m.filas.map((f, j) => (
                          <tr key={j} className="border-t border-slate-100">
                            <td className="py-1 pr-2 text-slate-600">{f.etiqueta}</td>
                            <td className="py-1 text-right font-medium tabular-nums text-slate-900">{f.valor}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                </div>
              </div>
            ))}
            {pensando && (
              <div className="flex items-center gap-2 text-xs text-slate-500"><Loader2 className="h-3.5 w-3.5 animate-spin" /> Buscando en los datos...</div>
            )}
            <div ref={fin} />
          </div>

          <div className="border-t border-slate-200 bg-white p-3">
            <div className="mb-2 flex flex-wrap gap-1.5">
              {sugerencias.map((s) => (
                <button key={s} onClick={() => preguntar(s)} disabled={pensando}
                  className="rounded-full border border-slate-300 px-2.5 py-1 text-xs text-slate-600 hover:bg-slate-50 disabled:opacity-50">
                  {s}
                </button>
              ))}
            </div>
            <form onSubmit={(e) => { e.preventDefault(); preguntar(texto); }} className="flex gap-2">
              <label htmlFor="pregunta-asistente" className="sr-only">Tu pregunta</label>
              <input id="pregunta-asistente" ref={entrada} value={texto} onChange={(e) => setTexto(e.target.value)} maxLength={300} placeholder="Escribí tu pregunta..."
                className="min-w-0 flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500" />
              <button type="submit" disabled={pensando || !texto.trim()} aria-label="Enviar la pregunta"
                className="rounded-md bg-marca-600 px-3 text-white hover:bg-marca-700 disabled:opacity-50">
                <Send className="h-4 w-4" />
              </button>
            </form>
          </div>
        </section>
      )}

      <button
        onClick={() => setAbierto((v) => !v)}
        aria-expanded={abierto}
        aria-label={abierto ? "Cerrar el asistente" : "Abrir el asistente"}
        className="fixed bottom-4 right-4 z-40 inline-flex items-center gap-2 rounded-full bg-slate-900 px-4 py-3 text-sm font-medium text-white shadow-lg hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-marca-500 focus:ring-offset-2"
      >
        <MessageCircle className="h-5 w-5" aria-hidden />
        Asistente
      </button>
    </div>
  );
}
