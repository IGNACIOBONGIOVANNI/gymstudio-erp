import { useEffect, useMemo, useState } from 'react';
import { MessageCircle } from 'lucide-react';
import { obtenerDecisiones } from '../../api/tableroApi';
import { formatearPesos } from '../../api/dashboardApi';
import { datosDelNegocio } from '../../utils/pdfComun';
import { enlaceWhatsApp, mensajeDeRegreso } from '../../utils/whatsapp';
import { simular, type Decisiones, type SocioParaAccionar } from './tableroUtil';

const tarjeta = 'rounded-xl border border-slate-200 bg-white p-4';

function mensaje(s: SocioParaAccionar, gimnasio: string): string {
  if (s.motivo === 'no-viene') return mensajeDeRegreso({ nombre: s.nombre, gimnasio, diasSinVenir: s.dias });
  const nombre = s.nombre.trim().split(/\s+/)[0] || 'socio/a';
  const cuando = s.dias === 0 ? 'vence hoy' : s.dias === 1 ? 'vence mañana' : `vence en ${s.dias} días`;
  return `Hola ${nombre}, te escribimos de ${gimnasio}. Te recordamos que tu cuota ${cuando}. Podés renovarla en recepción cuando quieras. ¡Gracias!`;
}

/** Sistema de apoyo a las decisiones: a quién contactar hoy y un simulador de precios. */
export function PestanaDecisiones({ hoy }: { hoy: string }) {
  const [datos, setDatos] = useState<Decisiones | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [aumentos, setAumentos] = useState<Record<number, number>>({});
  const [perdida, setPerdida] = useState(0);
  const negocio = datosDelNegocio();
  const gimnasio = negocio.nombreFantasia || negocio.razonSocial;

  useEffect(() => {
    obtenerDecisiones(hoy)
      .then((d) => { setDatos(d); setError(null); })
      .catch((e) => setError(e instanceof Error ? e.message : 'No se pudieron cargar las decisiones.'));
  }, [hoy]);

  const resultado = useMemo(() => (datos ? simular(datos.planes, aumentos, perdida) : null), [datos, aumentos, perdida]);

  if (error) return <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</div>;
  if (!datos || !resultado) return <p className="text-sm text-slate-500">Cargando…</p>;

  return (
    <div className="space-y-4">
      <section className={tarjeta} aria-labelledby="titulo-contactar">
        <h2 id="titulo-contactar" className="text-sm font-bold text-slate-900">A quién contactar hoy</h2>
        <p className="mb-3 text-xs text-slate-500">
          Reglas a la vista: socios con la cuota paga que no vienen hace 14 días o más (suelen ser la próxima baja) y socios
          a los que la cuota les vence en los próximos 7 días.
        </p>
        {datos.socios.length === 0 ? <p className="py-4 text-center text-sm text-slate-400">Nadie para contactar hoy.</p> : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="border-b border-slate-200 text-left text-xs text-slate-500">
                <tr>
                  <th scope="col" className="py-2 pr-3 font-semibold">Socio</th>
                  <th scope="col" className="py-2 pr-3 font-semibold">Plan</th>
                  <th scope="col" className="py-2 pr-3 font-semibold">Motivo</th>
                  <th scope="col" className="py-2 pr-3 font-semibold">Acción sugerida</th>
                  <th scope="col" className="py-2 font-semibold"><span className="sr-only">WhatsApp</span></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {datos.socios.map((s) => {
                  const enlace = enlaceWhatsApp(s.telefono, mensaje(s, gimnasio));
                  return (
                    <tr key={`${s.motivo}-${s.id}`}>
                      <td className="py-2 pr-3 font-medium text-slate-800">{s.apellido}, {s.nombre}</td>
                      <td className="py-2 pr-3 text-slate-600">{s.plan ?? '—'}</td>
                      <td className="py-2 pr-3">
                        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${s.motivo === 'no-viene' ? 'bg-red-50 text-red-700' : 'bg-amber-50 text-amber-800'}`}>
                          {s.motivo === 'no-viene' ? `No viene hace ${s.dias} días` : s.dias === 0 ? 'Vence hoy' : `Vence en ${s.dias} días`}
                        </span>
                      </td>
                      <td className="py-2 pr-3 text-xs text-slate-600">{s.accion}</td>
                      <td className="py-2">
                        {enlace ? (
                          <a href={enlace} target="_blank" rel="noopener noreferrer" aria-label={`Escribirle por WhatsApp a ${s.nombre} ${s.apellido}, se abre en otra pestaña`}
                            className="inline-flex items-center gap-1 rounded-md border border-emerald-300 px-2 py-1 text-xs font-medium text-emerald-700 hover:bg-emerald-50">
                            <MessageCircle className="h-3.5 w-3.5" aria-hidden="true" /> WhatsApp
                          </a>
                        ) : <span className="text-xs text-slate-400">Sin teléfono</span>}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className={tarjeta} aria-labelledby="titulo-simulador">
        <h2 id="titulo-simulador" className="text-sm font-bold text-slate-900">¿Qué pasaría si…?</h2>
        <p className="mb-3 text-xs text-slate-500">
          Probá un aumento por plan y cuántos socios estimás perder: el sistema calcula la recaudación mensual. No cambia ningún precio.
        </p>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="border-b border-slate-200 text-left text-xs text-slate-500">
              <tr>
                <th scope="col" className="py-2 pr-3 font-semibold">Plan</th>
                <th scope="col" className="py-2 pr-3 text-right font-semibold">Precio</th>
                <th scope="col" className="py-2 pr-3 text-right font-semibold">Socios vigentes</th>
                <th scope="col" className="py-2 pr-3 text-right font-semibold">Recauda por mes</th>
                <th scope="col" className="py-2 text-right font-semibold">Aumento (%)</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {datos.planes.map((p) => (
                <tr key={p.id}>
                  <td className="py-2 pr-3 font-medium text-slate-800">{p.nombre}</td>
                  <td className="py-2 pr-3 text-right tabular-nums">{formatearPesos(p.precioMensual)}{p.duracionMeses > 1 ? ` / ${p.duracionMeses} meses` : ''}</td>
                  <td className="py-2 pr-3 text-right tabular-nums">{p.sociosVigentes}</td>
                  <td className="py-2 pr-3 text-right tabular-nums">{formatearPesos(p.recaudacionMensual)}</td>
                  <td className="py-2 text-right">
                    <input type="number" min={-100} max={1000} step={1} value={aumentos[p.id] ?? 0} aria-label={`Aumento del plan ${p.nombre} en %`}
                      onChange={(e) => setAumentos((a) => ({ ...a, [p.id]: Number(e.target.value) }))}
                      className="w-20 rounded border border-slate-300 px-2 py-1 text-right text-sm focus:border-marca-500 focus:outline-none focus:ring-1 focus:ring-marca-500" />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="mt-4 flex flex-wrap items-end gap-6">
          <div>
            <label htmlFor="simulador-perdida" className="mb-1 block text-xs font-semibold text-slate-600">Socios que estimás perder (%)</label>
            <input id="simulador-perdida" type="number" min={0} max={100} step={1} value={perdida} onChange={(e) => setPerdida(Number(e.target.value))}
              className="w-24 rounded-lg border border-slate-300 px-3 py-2 text-right text-sm focus:border-marca-500 focus:outline-none focus:ring-2 focus:ring-marca-500" />
          </div>
          <dl className="grid grid-cols-3 gap-6 text-sm" aria-live="polite">
            <div><dt className="text-xs text-slate-500">Hoy, por mes</dt><dd className="font-bold tabular-nums">{formatearPesos(resultado.actual)}</dd></div>
            <div><dt className="text-xs text-slate-500">Proyectado, por mes</dt><dd className="font-bold tabular-nums">{formatearPesos(resultado.proyectada)}</dd></div>
            <div>
              <dt className="text-xs text-slate-500">Diferencia</dt>
              <dd className={`font-bold tabular-nums ${resultado.diferencia >= 0 ? 'text-emerald-700' : 'text-red-600'}`}>
                {resultado.diferencia >= 0 ? '+' : '−'}{formatearPesos(Math.abs(resultado.diferencia))}
              </dd>
            </div>
          </dl>
        </div>
      </section>
    </div>
  );
}
