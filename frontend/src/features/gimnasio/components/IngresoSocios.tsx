import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { AlertTriangle, CheckCircle2, CreditCard, Dumbbell, Loader2, LogIn, Trash2, XCircle } from "lucide-react";
import toast from "react-hot-toast";
import {
  deshacerIngreso, ingresosDeHoy, registrarIngreso,
  type CuotaDelSocio, type EstadoCuota, type IngresoDeHoy, type ResultadoIngreso,
} from "../../../api/asistenciasApi";
import { textoDescanso } from "../../../api/rutinasApi";
import { useAuth } from "../../../context/AuthContext";
import { formatearFechaLocal } from "../../../utils/formatters";

interface SocioParaCobrar { id: string; nombre: string; apellido: string; dni: string }

interface Props {
  /** Abre el cobro de la cuota del socio que acaba de ingresar. */
  onCobrar: (socio: SocioParaCobrar) => void;
  /** Cambia cada vez que se cobra una cuota, para volver a mirar cómo quedó el socio que se está mostrando. */
  refrescar: number;
  /** Se llama después de registrar o deshacer un ingreso, para que la pantalla actualice lo que depende de las visitas. */
  onIngreso: () => void;
}

type Tono = "ok" | "aviso" | "mal";

const ESTILO_DEL_TONO: Record<Tono, string> = {
  ok: "border-emerald-200 bg-emerald-50 text-emerald-900",
  aviso: "border-amber-300 bg-amber-50 text-amber-950",
  mal: "border-rose-300 bg-rose-50 text-rose-900",
};

/** Cómo se muestra la cuota: siempre con ícono y texto, nunca solo con color. */
function describirCuota(c: CuotaDelSocio): { tono: Tono; titulo: string; detalle: string } {
  const vence = c.vence ? formatearFechaLocal(c.vence) : "";
  switch (c.estado) {
    case "AlDia":
      return { tono: "ok", titulo: "Cuota al día", detalle: `Vence el ${vence}.` };
    case "PorVencer": {
      const dias = c.diasParaVencer ?? 0;
      const cuando = dias === 0 ? "vence hoy" : dias === 1 ? `vence mañana (${vence})` : `vence el ${vence} (en ${dias} días)`;
      return { tono: "aviso", titulo: "Cuota por vencer", detalle: `Su cuota ${cuando}.` };
    }
    case "Vencida":
      return { tono: "mal", titulo: "Cuota vencida", detalle: `Venció el ${vence}. Hay que cobrarla para renovarla.` };
    default:
      return { tono: "mal", titulo: "Sin plan", detalle: "Todavía no pagó ninguna cuota." };
  }
}

const ETIQUETA_DEL_ESTADO: Record<EstadoCuota, string> = { AlDia: "Al día", PorVencer: "Por vencer", Vencida: "Vencida", SinPlan: "Sin plan" };

const hora = (iso: string) => new Date(iso).toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit" });

function IconoDelTono({ tono }: { tono: Tono }) {
  const clase = "mt-0.5 h-5 w-5 shrink-0";
  if (tono === "ok") return <CheckCircle2 className={clase} aria-hidden />;
  if (tono === "aviso") return <AlertTriangle className={clase} aria-hidden />;
  return <XCircle className={clase} aria-hidden />;
}

/**
 * Ingreso de socios al gimnasio: se escribe el DNI, el sistema registra la visita y muestra cómo está la cuota
 * y qué día de la rutina le toca. Debajo, los ingresos del día.
 */
export function IngresoSocios({ onCobrar, refrescar, onIngreso }: Props) {
  const { user } = useAuth();
  const puedeDeshacer = user?.rol === "Administrador" || user?.rol === "Contador";
  const [dni, setDni] = useState("");
  const [enviando, setEnviando] = useState(false);
  const [resultado, setResultado] = useState<ResultadoIngreso | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [deHoy, setDeHoy] = useState<IngresoDeHoy[] | null>(null);
  const entrada = useRef<HTMLInputElement>(null);
  const ultimoDni = useRef<string | null>(null);

  const cargarDeHoy = useCallback(async () => {
    try { setDeHoy(await ingresosDeHoy()); } catch { /* si falla, la lista queda como estaba */ }
  }, []);

  useEffect(() => { void cargarDeHoy(); }, [cargarDeHoy]);

  // Después de cobrar una cuota se vuelve a pedir el mismo socio (no duplica la visita) para mostrar su cuota nueva.
  useEffect(() => {
    if (refrescar === 0 || !ultimoDni.current) return;
    registrarIngreso(ultimoDni.current).then(setResultado).catch(() => { /* se mantiene lo que se veía */ });
  }, [refrescar]);

  const enviar = async (e: FormEvent) => {
    e.preventDefault();
    const escrito = dni.trim();
    if (!escrito || enviando) return;
    setEnviando(true);
    setError(null);
    try {
      const r = await registrarIngreso(escrito);
      ultimoDni.current = r.socio.dni;
      setResultado(r);
      setDni("");
      void cargarDeHoy();
      onIngreso();
    } catch (err) {
      setResultado(null);
      setError((err as Error).message);
    } finally {
      setEnviando(false);
      entrada.current?.focus();
    }
  };

  const deshacer = async (i: IngresoDeHoy) => {
    if (!window.confirm(`¿Deshacer el ingreso de ${i.socio.apellido} ${i.socio.nombre}?`)) return;
    try {
      await deshacerIngreso(i.id);
      toast.success("Ingreso deshecho");
      if (resultado?.id === i.id) setResultado(null);
      void cargarDeHoy();
      onIngreso();
    } catch (err) {
      toast.error((err as Error).message);
    }
  };

  const cuota = resultado ? describirCuota(resultado.cuota) : null;
  const r = resultado;

  return (
    <div className="space-y-4">
      <section aria-labelledby="ingreso-titulo" className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 id="ingreso-titulo" className="text-base font-semibold text-slate-900">Ingreso de socios</h2>
        <p className="mt-0.5 text-sm text-slate-500">Escribí el DNI: el sistema registra la visita y muestra la cuota y la rutina de hoy.</p>

        <form onSubmit={enviar} className="mt-3 flex flex-col gap-2 sm:flex-row">
          <label htmlFor="ingreso-dni" className="sr-only">DNI del socio</label>
          <input
            id="ingreso-dni" ref={entrada} value={dni} onChange={(ev) => setDni(ev.target.value)}
            inputMode="numeric" autoComplete="off" autoFocus maxLength={12} placeholder="DNI, con o sin puntos"
            className="min-w-0 flex-1 rounded-lg border-2 border-slate-200 px-4 py-3 text-lg tabular-nums focus:border-marca-600 focus:outline-none"
          />
          <button type="submit" disabled={!dni.trim() || enviando}
            className="inline-flex items-center justify-center gap-2 rounded-lg bg-marca-600 px-5 py-3 text-base font-medium text-white hover:bg-marca-700 disabled:opacity-50">
            {enviando ? <Loader2 className="h-5 w-5 animate-spin" aria-hidden /> : <LogIn className="h-5 w-5" aria-hidden />}
            Registrar ingreso
          </button>
        </form>

        <div aria-live="polite" className="mt-4 space-y-3">
          {error && (
            <p role="alert" className="flex items-start gap-2 rounded-lg border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-900">
              <XCircle className="mt-0.5 h-5 w-5 shrink-0" aria-hidden /> {error}
            </p>
          )}

          {r && cuota && (
            <div className="rounded-xl border border-slate-200 p-4">
              <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
                <h3 className="text-xl font-semibold tracking-tight text-slate-900">{r.socio.nombre} {r.socio.apellido}</h3>
                <p className="text-sm text-slate-500">
                  DNI {r.socio.dni} · {r.yaRegistrado ? `ya había ingresado hoy a las ${hora(r.registradaEn)}` : `ingresó a las ${hora(r.registradaEn)}`}
                </p>
              </div>

              <div className={`mt-3 flex flex-wrap items-start justify-between gap-3 rounded-lg border px-4 py-3 ${ESTILO_DEL_TONO[cuota.tono]}`}>
                <div className="flex items-start gap-2.5">
                  <IconoDelTono tono={cuota.tono} />
                  <div>
                    <p className="font-semibold">{cuota.titulo}</p>
                    <p className="text-sm">{cuota.detalle}</p>
                  </div>
                </div>
                {r.cuota.estado !== "AlDia" && (
                  <button type="button" onClick={() => onCobrar({ id: String(r.socio.id), nombre: r.socio.nombre, apellido: r.socio.apellido, dni: r.socio.dni })}
                    className="inline-flex items-center gap-1.5 rounded-md bg-slate-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-800">
                    <CreditCard className="h-4 w-4" aria-hidden /> {r.cuota.estado === "PorVencer" ? "Renovar cuota" : "Cobrar cuota"}
                  </button>
                )}
              </div>

              <p className="mt-3 text-sm text-slate-600">
                {r.visitasDelMes} {r.visitasDelMes === 1 ? "visita" : "visitas"} este mes ·{" "}
                {r.ultimaVisitaAnterior ? `la anterior fue el ${formatearFechaLocal(r.ultimaVisitaAnterior)}` : "es su primera visita registrada"}
              </p>

              {r.rutina ? (
                <div className="mt-3">
                  <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                    <Dumbbell className="h-4 w-4 text-slate-500" aria-hidden />
                    <h4 className="text-sm font-semibold text-slate-900">
                      Hoy le toca: día {r.rutina.dia ?? "-"} de {r.rutina.diasTotales}
                    </h4>
                    <span className="text-sm text-slate-500">· {r.rutina.nombre} ({r.rutina.nivel})</span>
                  </div>
                  {r.rutina.vencida && (
                    <p className="mt-2 flex items-center gap-2 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-950 ring-1 ring-amber-200">
                      <AlertTriangle className="h-4 w-4 shrink-0" aria-hidden /> La rutina venció el {formatearFechaLocal(r.rutina.hasta)}: conviene renovarla.
                    </p>
                  )}
                  <div className="mt-2 overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="border-b border-slate-200 text-left text-slate-500">
                          <th className="py-1.5 pr-3 font-medium">Ejercicio</th>
                          <th className="py-1.5 pr-3 font-medium">Series</th>
                          <th className="py-1.5 pr-3 font-medium">Descanso</th>
                          <th className="py-1.5 font-medium">Indicaciones</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {r.rutina.ejercicios.map((e, i) => (
                          <tr key={i}>
                            <td className="py-1.5 pr-3 text-slate-900">{e.ejercicio} <span className="text-xs text-slate-500">{e.grupoMuscular}</span></td>
                            <td className="py-1.5 pr-3 tabular-nums">{e.series} × {e.repeticiones}</td>
                            <td className="py-1.5 pr-3 tabular-nums">{textoDescanso(e.descansoSegundos)}</td>
                            <td className="py-1.5 text-slate-600">{e.indicaciones || "-"}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              ) : (
                <p className="mt-3 text-sm text-slate-600">No tiene una rutina asignada: asignale una desde <span className="font-medium">Socios</span>.</p>
              )}
            </div>
          )}
        </div>
      </section>

      <section aria-labelledby="hoy-titulo" className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 id="hoy-titulo" className="text-base font-semibold text-slate-900">Ingresos de hoy{deHoy ? ` (${deHoy.length})` : ""}</h2>
        {!deHoy ? (
          <p className="mt-2 text-sm text-slate-500">Cargando...</p>
        ) : deHoy.length === 0 ? (
          <p className="mt-2 text-sm text-slate-500">Todavía no ingresó nadie hoy.</p>
        ) : (
          <div className="mt-2 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 text-left text-slate-500">
                  <th className="py-1.5 pr-3 font-medium">Hora</th>
                  <th className="py-1.5 pr-3 font-medium">Socio</th>
                  <th className="py-1.5 pr-3 font-medium">DNI</th>
                  <th className="py-1.5 pr-3 font-medium">Cuota</th>
                  <th className="py-1.5 pr-3 font-medium">Lo registró</th>
                  {puedeDeshacer && <th className="py-1.5 font-medium"><span className="sr-only">Deshacer</span></th>}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {deHoy.map((i) => (
                  <tr key={i.id}>
                    <td className="py-1.5 pr-3 tabular-nums">{hora(i.registradaEn)}</td>
                    <td className="py-1.5 pr-3 font-medium text-slate-900">{i.socio.apellido}, {i.socio.nombre}</td>
                    <td className="py-1.5 pr-3 tabular-nums text-slate-600">{i.socio.dni}</td>
                    <td className="py-1.5 pr-3">
                      <span className={`inline-flex items-center gap-1 text-xs font-medium ${i.estado === "AlDia" ? "text-emerald-700" : i.estado === "PorVencer" ? "text-amber-800" : "text-rose-700"}`}>
                        {i.estado === "AlDia" ? <CheckCircle2 className="h-3.5 w-3.5" aria-hidden /> : i.estado === "PorVencer" ? <AlertTriangle className="h-3.5 w-3.5" aria-hidden /> : <XCircle className="h-3.5 w-3.5" aria-hidden />}
                        {ETIQUETA_DEL_ESTADO[i.estado] ?? i.estado}
                      </span>
                    </td>
                    <td className="py-1.5 pr-3 text-slate-500">{i.registradaPor || "-"}</td>
                    {puedeDeshacer && (
                      <td className="py-1.5 text-right">
                        <button type="button" onClick={() => deshacer(i)} aria-label={`Deshacer el ingreso de ${i.socio.apellido} ${i.socio.nombre}`}
                          className="rounded-md p-1.5 text-slate-400 hover:bg-slate-100 hover:text-rose-700">
                          <Trash2 className="h-4 w-4" aria-hidden />
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
