import { useCallback, useEffect, useRef, useState } from "react";
import { AlertTriangle, Bell, CheckCircle2, Info, Loader2, RefreshCw } from "lucide-react";
import toast from "react-hot-toast";
import { pedir } from "../api/rutinasApi";
import type { ResumenInicio } from "../api/dashboardApi";
import { avisosNuevos, claveDeAviso, textosDeAtencion, type Novedad } from "../utils/avisos";
import { hoyLocal } from "../utils/formatters";

const CADA_CUANTO_MS = 60_000;
const MINIMO_ENTRE_PEDIDOS_MS = 20_000;

interface Props {
  /** Pantalla que se está viendo: al cambiar de pantalla se vuelve a mirar si hay avisos nuevos. */
  vistaActual: string;
  onVerResumen: () => void;
}

/**
 * Campanita flotante con los avisos del sistema (stock agotado, rutinas vencidas, caja en negativo, etc.).
 * Usa las mismas novedades que el resumen de inicio, así que el servidor decide qué ve cada perfil.
 * Al entrar avisa una vez cuántos requieren atención y, después, solo los que aparecen nuevos.
 */
export function Campanita({ vistaActual, onVerResumen }: Props) {
  const [novedades, setNovedades] = useState<Novedad[] | null>(null);
  const [fallo, setFallo] = useState(false);
  const [abierto, setAbierto] = useState(false);
  const [actualizado, setActualizado] = useState<Date | null>(null);
  const contenedor = useRef<HTMLDivElement>(null);
  const boton = useRef<HTMLButtonElement>(null);
  const vistos = useRef<Set<string> | null>(null);
  // Carteles que mostró la campanita: se cierran al abrirla, porque ya se está viendo lo mismo en el panel.
  const carteles = useRef<string[]>([]);
  const contadorDeCarteles = useRef(0);
  const panelAbierto = useRef(false);
  const ultimoPedido = useRef(0);
  const enCurso = useRef(false);

  const avisar = useCallback((lista: Novedad[]) => {
    const atencion = textosDeAtencion(lista);
    const anteriores = vistos.current;
    // Se recuerdan solo los avisos de ahora: si uno se resuelve y vuelve a aparecer, se avisa de nuevo.
    vistos.current = new Set(atencion.map(claveDeAviso));
    const mostrar = (texto: string, icon: string, duration: number) => {
      if (panelAbierto.current) return; // con el panel abierto, el aviso ya se está viendo ahí
      const id = `campanita-${++contadorDeCarteles.current}`;
      carteles.current.push(id);
      toast(texto, { id, icon, duration });
    };
    if (anteriores === null) {
      if (atencion.length > 0) {
        mostrar(`${atencion.length === 1 ? "Hay 1 aviso que requiere" : `Hay ${atencion.length} avisos que requieren`} atención. Mirá la campanita.`, "🔔", 7000);
      }
      return;
    }
    avisosNuevos(anteriores, atencion).slice(0, 3).forEach((texto) => mostrar(texto, "⚠️", 8000));
  }, []);

  const actualizar = useCallback(async (forzar: boolean) => {
    if (enCurso.current) return;
    if (!forzar && Date.now() - ultimoPedido.current < MINIMO_ENTRE_PEDIDOS_MS) return;
    enCurso.current = true;
    ultimoPedido.current = Date.now();
    try {
      const resumen = await pedir<ResumenInicio>(`/api/dashboard?hoy=${hoyLocal()}`, "No se pudieron cargar los avisos");
      const lista = Array.isArray(resumen?.novedades) ? resumen.novedades : [];
      avisar(lista);
      setNovedades(lista);
      setFallo(false);
      setActualizado(new Date());
    } catch {
      // Sin molestar con un cartel cada minuto: la campanita sigue mostrando lo último que supo.
      setFallo(true);
    } finally {
      enCurso.current = false;
    }
  }, [avisar]);

  // Al entrar y cada minuto mientras la pestaña está a la vista.
  useEffect(() => {
    void actualizar(true);
    const temporizador = window.setInterval(() => { if (document.visibilityState === "visible") void actualizar(true); }, CADA_CUANTO_MS);
    const alVolver = () => { if (document.visibilityState === "visible") void actualizar(false); };
    document.addEventListener("visibilitychange", alVolver);
    return () => {
      window.clearInterval(temporizador);
      document.removeEventListener("visibilitychange", alVolver);
    };
  }, [actualizar]);

  // Al cambiar de pantalla (por ejemplo, después de facturar o de cargar un movimiento).
  useEffect(() => { void actualizar(false); }, [vistaActual, actualizar]);

  useEffect(() => {
    panelAbierto.current = abierto;
    if (!abierto) return;
    carteles.current.forEach((id) => toast.dismiss(id));
    carteles.current = [];
    void actualizar(true);
    const afuera = (e: PointerEvent) => { if (!contenedor.current?.contains(e.target as Node)) setAbierto(false); };
    const tecla = (e: KeyboardEvent) => { if (e.key === "Escape") { setAbierto(false); boton.current?.focus(); } };
    document.addEventListener("pointerdown", afuera);
    document.addEventListener("keydown", tecla);
    return () => {
      document.removeEventListener("pointerdown", afuera);
      document.removeEventListener("keydown", tecla);
    };
  }, [abierto, actualizar]);

  const cantidad = textosDeAtencion(novedades).length;
  const etiqueta = cantidad > 0
    ? `Avisos: ${cantidad === 1 ? "1 requiere" : `${cantidad} requieren`} atención`
    : "Avisos: todo en orden";

  return (
    <div ref={contenedor} className="fixed right-4 top-[10px] z-30 print:hidden lg:top-4">
      <button
        ref={boton}
        onClick={() => setAbierto((v) => !v)}
        aria-expanded={abierto}
        aria-label={etiqueta}
        className="relative inline-flex h-9 w-9 items-center justify-center rounded-full bg-slate-900 text-white shadow-md ring-1 ring-white/20 hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-marca-500 focus:ring-offset-2"
      >
        <Bell className="h-[18px] w-[18px]" aria-hidden />
        {cantidad > 0 && (
          <span aria-hidden className="absolute -right-1 -top-1 flex h-[18px] min-w-[18px] items-center justify-center rounded-full bg-amber-400 px-1 text-[10px] font-bold leading-none text-slate-950 ring-2 ring-slate-50">
            {cantidad > 9 ? "9+" : cantidad}
          </span>
        )}
      </button>

      {abierto && (
        <section
          role="dialog"
          aria-label="Avisos del sistema"
          className="absolute right-0 top-11 flex max-h-[calc(100vh-6rem)] w-[min(24rem,calc(100vw-2rem))] flex-col overflow-hidden rounded-xl bg-white shadow-2xl ring-1 ring-slate-900/10"
        >
          <header className="flex items-center justify-between border-b border-slate-200 px-4 py-3">
            <div>
              <h2 className="text-sm font-semibold text-slate-900">Avisos</h2>
              <p className="text-xs text-slate-500">
                {actualizado ? `Actualizado a las ${actualizado.toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit" })}` : "Buscando..."}
              </p>
            </div>
            <button onClick={() => void actualizar(true)} aria-label="Actualizar los avisos" className="rounded-full p-1.5 text-slate-500 hover:bg-slate-100 hover:text-slate-800">
              <RefreshCw className="h-4 w-4" aria-hidden />
            </button>
          </header>

          <div className="flex-1 overflow-y-auto p-2" aria-live="polite">
            {fallo && (
              <p className="m-2 rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-900 ring-1 ring-amber-200">
                {novedades ? "No se pudo actualizar: se muestran los últimos avisos que se conocen." : "No se pudieron cargar los avisos. Verificá que el sistema esté levantado."}
              </p>
            )}
            {!novedades && !fallo && (
              <p className="flex items-center gap-2 p-3 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Buscando avisos...</p>
            )}
            {novedades && (
              <ul className="space-y-1">
                {novedades.map((n, i) => (
                  <li key={i} className={`flex items-start gap-2.5 rounded-lg px-3 py-2 text-sm text-slate-700 ${n?.nivel === "atencion" ? "bg-amber-50" : ""}`}>
                    {n?.nivel === "atencion" ? <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-label="Atención" />
                      : n?.nivel === "ok" ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" aria-label="En orden" />
                      : <Info className="mt-0.5 h-4 w-4 shrink-0 text-slate-400" aria-label="Dato" />}
                    <span>{n?.texto}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <footer className="border-t border-slate-200 p-2">
            <button onClick={() => { setAbierto(false); onVerResumen(); }} className="w-full rounded-md px-3 py-2 text-sm font-medium text-marca-700 hover:bg-slate-50">
              Ver el resumen de inicio
            </button>
          </footer>
        </section>
      )}
    </div>
  );
}
