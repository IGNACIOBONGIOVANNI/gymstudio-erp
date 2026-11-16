import { useEffect, useMemo, useState } from 'react';
import { Loader2, X } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, ComposedChart, Legend, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { obtenerAnalisis, obtenerPlanes, type PlanDelFiltro } from '../../api/tableroApi';
import { compacto, formatearPesos, nombreDelMes } from '../../api/dashboardApi';
import { DIAS_DE_LA_SEMANA, FRANJAS, PRIMERA_HORA, type Analisis } from './tableroUtil';

const campo = 'rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-marca-500 focus:outline-none focus:ring-2 focus:ring-marca-500';
const tarjeta = 'rounded-xl border border-slate-200 bg-white p-4';

/** «aaaa-mm» corrido n meses. */
function correrMes(mes: string, n: number): string {
  const [a, m] = mes.split('-').map(Number);
  const d = new Date(a, m - 1 + n, 1);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
}

const pesosDelEje = (v: number) => `$${compacto(v)}`;
const tooltipPesos = (v: unknown) => formatearPesos(Number(v));

/**
 * Análisis como en Power BI: segmentadores (período y plan), filtro cruzado (un clic en un mes o en un plan filtra los demás
 * gráficos) y bajada al detalle (de meses a días).
 */
export function PestanaAnalisis({ mesFinal }: { mesFinal: string }) {
  const [desde, setDesde] = useState(correrMes(mesFinal, -5));
  const [hasta, setHasta] = useState(mesFinal);
  const [planId, setPlanId] = useState<number | null>(null);
  const [planes, setPlanes] = useState<PlanDelFiltro[]>([]);
  const [datos, setDatos] = useState<Analisis | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [listoPara, setListoPara] = useState<string | null>(null);
  const [rangoPrevio, setRangoPrevio] = useState<{ desde: string; hasta: string } | null>(null);

  useEffect(() => { obtenerPlanes().then(setPlanes).catch(() => setPlanes([])); }, []);

  // Lo que se está pidiendo: mientras la respuesta no sea de este pedido, se muestra «calculando».
  const pedido = `${desde}|${hasta}|${planId ?? ''}`;
  const cargando = listoPara !== pedido;
  useEffect(() => {
    let cancelado = false;
    obtenerAnalisis(desde, hasta, planId)
      .then((a) => { if (!cancelado) { setDatos(a); setError(null); } })
      .catch((e) => { if (!cancelado) setError(e instanceof Error ? e.message : 'No se pudo armar el análisis.'); })
      .finally(() => { if (!cancelado) setListoPara(`${desde}|${hasta}|${planId ?? ''}`); });
    return () => { cancelado = true; };
  }, [desde, hasta, planId]);

  const nombreDelPlan = planes.find((p) => p.id === planId)?.nombre ?? null;
  const maximo = useMemo(() => Math.max(1, ...(datos?.asistencia.flat() ?? [0])), [datos]);

  // Clic en un mes: baja a ese mes, día por día (y recuerda el período para volver).
  const bajarAlMes = (periodo: unknown) => {
    if (typeof periodo !== 'string' || !datos || datos.granularidad !== 'mes') return;
    setRangoPrevio({ desde, hasta });
    setDesde(periodo);
    setHasta(periodo);
  };
  const volver = () => {
    if (rangoPrevio) { setDesde(rangoPrevio.desde); setHasta(rangoPrevio.hasta); }
    setRangoPrevio(null);
  };
  // Clic en un plan: filtra por ese plan (otro clic en el mismo, lo quita).
  const filtrarPlan = (nombre: unknown) => {
    const plan = planes.find((p) => p.nombre === nombre);
    if (plan) setPlanId((actual) => (actual === plan.id ? null : plan.id));
  };

  const etiquetaDelPeriodo = (p: string) => (p.length === 7 ? nombreDelMes(p, true) : p.slice(8));
  const filtros = [
    ...(rangoPrevio ? [{ texto: `Detalle de ${nombreDelMes(desde)}`, quitar: volver }] : []),
    ...(nombreDelPlan ? [{ texto: `Plan: ${nombreDelPlan}`, quitar: () => setPlanId(null) }] : []),
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label htmlFor="analisis-desde" className="mb-1 block text-xs font-semibold text-slate-600">Desde</label>
          <input id="analisis-desde" type="month" value={desde} max={hasta} className={campo}
            onChange={(e) => { if (e.target.value) { setDesde(e.target.value); setRangoPrevio(null); } }} />
        </div>
        <div>
          <label htmlFor="analisis-hasta" className="mb-1 block text-xs font-semibold text-slate-600">Hasta</label>
          <input id="analisis-hasta" type="month" value={hasta} min={desde} className={campo}
            onChange={(e) => { if (e.target.value) { setHasta(e.target.value); setRangoPrevio(null); } }} />
        </div>
        <div>
          <label htmlFor="analisis-plan" className="mb-1 block text-xs font-semibold text-slate-600">Plan</label>
          <select id="analisis-plan" value={planId ?? ''} className={campo}
            onChange={(e) => setPlanId(e.target.value === '' ? null : Number(e.target.value))}>
            <option value="">Todos los planes</option>
            {planes.map((p) => <option key={p.id} value={p.id}>{p.nombre}</option>)}
          </select>
        </div>
        {cargando && <Loader2 className="mb-2.5 h-4 w-4 animate-spin text-slate-400" aria-label="Calculando" />}
      </div>

      <div className="flex flex-wrap items-center gap-2 text-xs" aria-live="polite">
        <span className="font-semibold text-slate-600">Filtros activos:</span>
        {filtros.length === 0 && <span className="text-slate-400">ninguno (tocá un mes o un plan en los gráficos para filtrar)</span>}
        {filtros.map((f) => (
          <span key={f.texto} className="inline-flex items-center gap-1 rounded-full bg-marca-50 px-2.5 py-1 font-medium text-marca-800">
            {f.texto}
            <button type="button" onClick={f.quitar} aria-label={`Quitar el filtro ${f.texto}`} className="rounded-full hover:bg-marca-100"><X className="h-3.5 w-3.5" /></button>
          </span>
        ))}
      </div>

      {error && <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</div>}

      {datos && (
        <>
          <section className={tarjeta} aria-labelledby="titulo-evolucion">
            <h2 id="titulo-evolucion" className="text-sm font-bold text-slate-900">
              Evolución {datos.granularidad === 'dia' ? `día por día de ${nombreDelMes(desde)}` : 'mes a mes'}
            </h2>
            <p className="mb-2 text-xs text-slate-500">
              {datos.granularidad === 'mes' ? 'Tocá una barra para ver ese mes día por día.' : 'Quitá el filtro «Detalle» para volver a los meses.'}
              {nombreDelPlan && ' Las cuotas son solo del plan elegido.'}
            </p>
            <div className="h-72">
              <ResponsiveContainer width="100%" height="100%">
                <ComposedChart data={datos.evolucion} margin={{ top: 8, right: 8, left: 8, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} />
                  <XAxis dataKey="periodo" tickFormatter={etiquetaDelPeriodo} tick={{ fontSize: 11 }} />
                  <YAxis tickFormatter={pesosDelEje} tick={{ fontSize: 11 }} width={60} />
                  <Tooltip formatter={tooltipPesos} labelFormatter={(p) => (String(p).length === 7 ? nombreDelMes(String(p)) : String(p).split('-').reverse().join('/'))} />
                  <Legend wrapperStyle={{ fontSize: 12 }} />
                  <Bar dataKey="ventas" name="Ventas" fill="#0f766e" cursor="pointer" onClick={(d) => bajarAlMes((d as { payload?: { periodo?: string } }).payload?.periodo)} />
                  <Bar dataKey="compras" name="Compras" fill="#f59e0b" cursor="pointer" onClick={(d) => bajarAlMes((d as { payload?: { periodo?: string } }).payload?.periodo)} />
                  <Bar dataKey="cuotas" name="Cuotas" fill="#6366f1" cursor="pointer" onClick={(d) => bajarAlMes((d as { payload?: { periodo?: string } }).payload?.periodo)} />
                  <Line dataKey="resultado" name="Resultado" stroke="#0f172a" strokeWidth={2} dot={false} />
                </ComposedChart>
              </ResponsiveContainer>
            </div>
          </section>

          <div className="grid gap-4 lg:grid-cols-2">
            <section className={tarjeta} aria-labelledby="titulo-planes">
              <h2 id="titulo-planes" className="text-sm font-bold text-slate-900">Ingresos por plan</h2>
              <p className="mb-2 text-xs text-slate-500">Cuotas cobradas en el período. Tocá un plan para filtrar todo el análisis.</p>
              {datos.ingresosPorPlan.length === 0 ? <p className="py-8 text-center text-sm text-slate-400">No hay cuotas cobradas en el período.</p> : (
                <div className="h-56">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={datos.ingresosPorPlan} layout="vertical" margin={{ left: 8, right: 16 }}>
                      <XAxis type="number" tickFormatter={pesosDelEje} tick={{ fontSize: 11 }} />
                      <YAxis type="category" dataKey="plan" width={110} tick={{ fontSize: 11 }} />
                      <Tooltip formatter={tooltipPesos} />
                      <Bar dataKey="neto" name="Neto" fill="#6366f1" cursor="pointer" onClick={(d) => filtrarPlan((d as { payload?: { plan?: string } }).payload?.plan)} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              )}
            </section>

            <section className={tarjeta} aria-labelledby="titulo-asistencia">
              <h2 id="titulo-asistencia" className="text-sm font-bold text-slate-900">Mapa de asistencia</h2>
              <p className="mb-2 text-xs text-slate-500">Ingresos por día de la semana y hora{nombreDelPlan ? `, socios del plan ${nombreDelPlan}` : ''}. Más oscuro = más gente.</p>
              <div className="overflow-x-auto">
                <table className="w-full border-separate border-spacing-0.5 text-[10px]">
                  <thead>
                    <tr>
                      <th scope="col" className="sr-only">Día</th>
                      {Array.from({ length: FRANJAS }, (_, k) => (
                        <th key={k} scope="col" className="font-normal text-slate-500">{String(PRIMERA_HORA + k).padStart(2, '0')}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {datos.asistencia.map((fila, d) => (
                      <tr key={DIAS_DE_LA_SEMANA[d]}>
                        <th scope="row" className="pr-1 text-left font-medium text-slate-600">{DIAS_DE_LA_SEMANA[d].slice(0, 3)}</th>
                        {fila.map((n, k) => (
                          <td key={k} title={`${DIAS_DE_LA_SEMANA[d]} de ${PRIMERA_HORA + k} a ${PRIMERA_HORA + k + 1} h: ${n} ingresos`}
                            className="h-6 min-w-6 rounded text-center tabular-nums"
                            style={{ backgroundColor: n === 0 ? '#f1f5f9' : `rgba(15, 118, 110, ${0.15 + 0.85 * (n / maximo)})`, color: n / maximo > 0.5 ? 'white' : '#334155' }}>
                            {n || ''}
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>

            <section className={tarjeta} aria-labelledby="titulo-productos">
              <h2 id="titulo-productos" className="mb-2 text-sm font-bold text-slate-900">Productos más vendidos</h2>
              {datos.topProductos.length === 0 ? <p className="py-8 text-center text-sm text-slate-400">No hay ventas de productos en el período.</p> : (
                <div className="h-56">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={datos.topProductos} layout="vertical" margin={{ left: 8, right: 16 }}>
                      <XAxis type="number" tickFormatter={pesosDelEje} tick={{ fontSize: 11 }} />
                      <YAxis type="category" dataKey="producto" width={120} tick={{ fontSize: 11 }} />
                      <Tooltip formatter={tooltipPesos} />
                      <Bar dataKey="total" name="Vendido" fill="#0f766e" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              )}
            </section>

            <section className={tarjeta} aria-labelledby="titulo-proveedores">
              <h2 id="titulo-proveedores" className="mb-2 text-sm font-bold text-slate-900">En qué se gasta: compras por proveedor</h2>
              {datos.comprasPorProveedor.length === 0 ? <p className="py-8 text-center text-sm text-slate-400">No hay compras en el período.</p> : (
                <div className="h-56">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={datos.comprasPorProveedor} layout="vertical" margin={{ left: 8, right: 16 }}>
                      <XAxis type="number" tickFormatter={pesosDelEje} tick={{ fontSize: 11 }} />
                      <YAxis type="category" dataKey="proveedor" width={140} tick={{ fontSize: 11 }} />
                      <Tooltip formatter={tooltipPesos} />
                      <Bar dataKey="neto" name="Comprado" fill="#f59e0b" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              )}
            </section>
          </div>
        </>
      )}
    </div>
  );
}
