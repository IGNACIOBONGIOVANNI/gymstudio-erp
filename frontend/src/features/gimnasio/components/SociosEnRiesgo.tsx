import { useEffect, useState } from "react";
import { Loader2, MessageCircle } from "lucide-react";
import { sociosEnRiesgo, type SocioEnRiesgo } from "../../../api/asistenciasApi";
import { datosDelNegocio } from "../../../utils/pdfComun";
import { formatearFechaLocal } from "../../../utils/formatters";
import { enlaceWhatsApp, mensajeDeRegreso } from "../../../utils/whatsapp";

const OPCIONES_DE_DIAS = [7, 14, 21, 30];

/**
 * Socios con la cuota al día que hace tiempo no vienen: pagan y no usan el gimnasio, que suele pasar antes de una baja.
 * Cada uno tiene un botón para escribirle por WhatsApp con el mensaje ya armado.
 */
export function SociosEnRiesgo({ refrescar }: { refrescar: number }) {
  const [dias, setDias] = useState(14);
  const [lista, setLista] = useState<SocioEnRiesgo[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let vigente = true;
    setError(null);
    sociosEnRiesgo(dias)
      .then((l) => vigente && setLista(Array.isArray(l) ? l : []))
      .catch((e: Error) => { if (vigente) { setError(e.message); setLista([]); } });
    return () => { vigente = false; };
  }, [dias, refrescar]);

  const negocio = datosDelNegocio();
  const gimnasio = negocio.nombreFantasia || negocio.razonSocial;

  return (
    <section aria-labelledby="riesgo-titulo" className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 id="riesgo-titulo" className="text-base font-semibold text-slate-900">
            Socios en riesgo de abandono{lista ? ` (${lista.length})` : ""}
          </h2>
          <p className="mt-0.5 text-sm text-slate-500">Tienen la cuota al día, pero hace {dias} días o más que no vienen.</p>
        </div>
        <div className="flex items-center gap-2">
          <label htmlFor="riesgo-dias" className="text-sm text-slate-600">Sin venir hace</label>
          <select id="riesgo-dias" value={dias} onChange={(e) => setDias(Number(e.target.value))}
            className="rounded-md border border-slate-300 px-2 py-1.5 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500">
            {OPCIONES_DE_DIAS.map((d) => <option key={d} value={d}>{d} días o más</option>)}
          </select>
        </div>
      </div>

      {error && <p role="alert" className="mt-3 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-950 ring-1 ring-amber-200">{error}</p>}

      {!lista ? (
        <p className="mt-3 flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Buscando...</p>
      ) : lista.length === 0 ? (
        !error && <p className="mt-3 text-sm text-slate-500">Nadie por ahora: todos los socios al día vinieron en los últimos {dias} días.</p>
      ) : (
        <ul className="mt-3 divide-y divide-slate-100">
          {lista.map((s) => {
            const enlace = enlaceWhatsApp(s.telefono, mensajeDeRegreso({ nombre: s.nombre, gimnasio, diasSinVenir: s.ultimaVisita ? s.diasSinVenir : null }));
            return (
              <li key={s.id} className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 py-2.5">
                <div>
                  <p className="text-sm font-medium text-slate-900">{s.apellido}, {s.nombre}</p>
                  <p className="text-xs text-slate-500">
                    {s.ultimaVisita ? `${s.diasSinVenir} días sin venir (última visita: ${formatearFechaLocal(s.ultimaVisita)})` : `Todavía no vino desde que se anotó (${s.diasSinVenir} días)`}
                    {" · "}cuota hasta el {formatearFechaLocal(s.venceCuota)}
                  </p>
                </div>
                {enlace ? (
                  <a href={enlace} target="_blank" rel="noopener noreferrer" title="Abrir WhatsApp con el mensaje ya escrito"
                    className="inline-flex items-center rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">
                    <MessageCircle className="mr-1.5 h-3.5 w-3.5" aria-hidden /> Escribirle
                  </a>
                ) : (
                  <span title={s.telefono ? "El teléfono cargado no es válido: corregilo en Socios" : "Este socio no tiene teléfono cargado"}
                    className="inline-flex cursor-not-allowed items-center rounded-md border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs font-medium text-slate-400">
                    <MessageCircle className="mr-1.5 h-3.5 w-3.5" aria-hidden /> Sin teléfono
                  </span>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
