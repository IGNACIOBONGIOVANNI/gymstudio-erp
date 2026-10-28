import { useEffect, useState } from "react";
import { AlertTriangle, Printer } from "lucide-react";
import toast from "react-hot-toast";
import { pedir } from "../../api/rutinasApi";
import { compacto, formatearPesos, nombreDelMes, techoDelEje, type FinanzasDelMes, type ResumenInicio } from "../../api/dashboardApi";
import { useAuth } from "../../context/AuthContext";
import { generarResumenPDF } from "../../utils/generarResumenPDF";
import { hoyLocal } from "../../utils/formatters";

// Colores de las series: azul y naranja, validados para daltonismo (diferencia de color >= 24 en
// todas las variantes) y con contraste >= 3:1 sobre blanco. Ventas siempre azul, compras siempre naranja.
const ESTILO = `
.viz-root { --serie-ventas: #2a78d6; --serie-compras: #eb6834; --grilla: #e4e6e2; --eje: #66706a; --superficie: #ffffff; }
`;

const tarjeta = "rounded-xl border border-slate-200 bg-white p-5";
const titulo = "text-sm font-semibold text-slate-800";

function Indicador({ etiqueta, valor, nota, alerta }: { etiqueta: string; valor: string; nota?: string; alerta?: boolean }) {
  return (
    <div className={tarjeta}>
      <p className="text-sm text-slate-500">{etiqueta}</p>
      <p className="mt-1 text-2xl font-semibold tracking-tight text-slate-900">{valor}</p>
      {nota && (
        <p className="mt-1 flex items-center gap-1 text-xs text-slate-500">
          {alerta && <AlertTriangle className="h-3.5 w-3.5 shrink-0 text-amber-600" aria-hidden />}
          {nota}
        </p>
      )}
    </div>
  );
}

/** Columnas agrupadas: ventas y compras (neto) de los últimos 6 meses. */
function GraficoVentasCompras({ serie }: { serie: FinanzasDelMes["serie"] }) {
  const [activo, setActivo] = useState<number | null>(null);
  const ancho = 640, alto = 260, izq = 52, der = 12, arriba = 16, abajo = 30;
  const maximo = techoDelEje(Math.max(...serie.flatMap((m) => [m.ventas, m.compras])));
  const hAreal = alto - arriba - abajo;
  const anchoGrupo = (ancho - izq - der) / serie.length;
  const grosor = Math.min(24, anchoGrupo / 2 - 6);
  const y = (v: number) => arriba + hAreal - (v / maximo) * hAreal;
  const ticks = [0, 0.25, 0.5, 0.75, 1].map((f) => f * maximo);
  const ultimo = serie.length - 1;

  // Columna con la punta redondeada (4 px) y la base recta, que arranca en la línea de base.
  const columna = (x: number, valor: number, color: string) => {
    const h = Math.max(0, (valor / maximo) * hAreal);
    if (h === 0) return null;
    const r = Math.min(4, h, grosor / 2);
    const x2 = x + grosor, base = arriba + hAreal, tope = base - h;
    return <path d={`M${x},${base} V${tope + r} Q${x},${tope} ${x + r},${tope} H${x2 - r} Q${x2},${tope} ${x2},${tope + r} V${base} Z`} fill={color} />;
  };

  return (
    <div className="relative">
      <svg viewBox={`0 0 ${ancho} ${alto}`} className="w-full" role="img"
        aria-label={`Ventas y compras netas de los últimos ${serie.length} meses. Los datos están también en la tabla.`}
        onMouseLeave={() => setActivo(null)}>
        {ticks.map((t) => (
          <g key={t}>
            <line x1={izq} x2={ancho - der} y1={y(t)} y2={y(t)} stroke="var(--grilla)" strokeWidth={1} />
            <text x={izq - 8} y={y(t) + 4} textAnchor="end" fontSize={11} fill="var(--eje)">{compacto(t)}</text>
          </g>
        ))}
        {serie.map((m, i) => {
          const centro = izq + anchoGrupo * (i + 0.5);
          return (
            <g key={m.mes}>
              {columna(centro - grosor - 1, m.ventas, "var(--serie-ventas)")}
              {columna(centro + 1, m.compras, "var(--serie-compras)")}
              <text x={centro} y={alto - 10} textAnchor="middle" fontSize={12} fill="var(--eje)" fontWeight={i === ultimo ? 600 : 400}>
                {nombreDelMes(m.mes, true)}
              </text>
              {/* Solo se rotula el mes elegido; el resto, en el tooltip y en la tabla. */}
              {i === ultimo && (
                <>
                  {m.ventas > 0 && <text x={centro - grosor / 2 - 1} y={y(m.ventas) - 5} textAnchor="middle" fontSize={11} fill="#272e2a">{compacto(m.ventas)}</text>}
                  {m.compras > 0 && <text x={centro + grosor / 2 + 1} y={y(m.compras) - 5} textAnchor="middle" fontSize={11} fill="#272e2a">{compacto(m.compras)}</text>}
                </>
              )}
              <rect x={izq + anchoGrupo * i} y={arriba} width={anchoGrupo} height={hAreal + abajo} fill="transparent"
                onMouseEnter={() => setActivo(i)} onFocus={() => setActivo(i)} tabIndex={0}
                aria-label={`${nombreDelMes(m.mes)}: ventas ${formatearPesos(m.ventas)}, compras ${formatearPesos(m.compras)}`} />
            </g>
          );
        })}
        <line x1={izq} x2={ancho - der} y1={arriba + hAreal} y2={arriba + hAreal} stroke="var(--eje)" strokeWidth={1} />
      </svg>
      {activo !== null && (
        <div className={`pointer-events-none absolute top-2 z-10 rounded-md border border-slate-200 bg-white px-3 py-2 text-xs shadow-md ${activo >= serie.length / 2 ? "-translate-x-full" : ""}`}
          style={{ left: `${((izq + anchoGrupo * (activo >= serie.length / 2 ? activo : activo + 1)) / ancho) * 100}%` }}>
          <p className="mb-1 font-semibold text-slate-800">{nombreDelMes(serie[activo].mes)}</p>
          <p className="flex items-center gap-1.5 text-slate-600"><span className="h-2.5 w-2.5 rounded-sm" style={{ background: "var(--serie-ventas)" }} />Ventas {formatearPesos(serie[activo].ventas)}</p>
          <p className="flex items-center gap-1.5 text-slate-600"><span className="h-2.5 w-2.5 rounded-sm" style={{ background: "var(--serie-compras)" }} />Compras {formatearPesos(serie[activo].compras)}</p>
        </div>
      )}
    </div>
  );
}

/** Barras horizontales de una sola serie, con el valor en la punta. */
function BarrasHorizontales({ filas, vacio }: { filas: { etiqueta: string; valor: number; detalle?: string }[]; vacio: string }) {
  const maximo = Math.max(...filas.map((f) => f.valor), 0);
  if (filas.length === 0 || maximo <= 0) return <p className="text-sm text-slate-500">{vacio}</p>;
  return (
    <ul className="space-y-3">
      {filas.map((f) => (
        <li key={f.etiqueta}>
          <div className="mb-1 flex items-baseline justify-between gap-3 text-sm">
            <span className="truncate text-slate-700">{f.etiqueta}</span>
            <span className="shrink-0 font-medium text-slate-900">{formatearPesos(f.valor)}</span>
          </div>
          <div className="h-2.5 rounded-full bg-slate-100" title={f.detalle}>
            <div className="h-2.5 rounded-full" style={{ width: f.valor <= 0 ? "0%" : `${Math.max(2, (f.valor / maximo) * 100)}%`, background: "var(--serie-ventas)" }} />
          </div>
          {f.detalle && <p className="mt-0.5 text-xs text-slate-500">{f.detalle}</p>}
        </li>
      ))}
    </ul>
  );
}

export function Dashboard() {
  const { user } = useAuth();
  const hoy = hoyLocal();
  const [mes, setMes] = useState(hoy.slice(0, 7));
  const [datos, setDatos] = useState<ResumenInicio | null>(null);
  const [cargando, setCargando] = useState(true);
  const [verTabla, setVerTabla] = useState(false);

  useEffect(() => {
    let vigente = true;
    setCargando(true);
    pedir<ResumenInicio>(`/api/dashboard?mes=${mes}&hoy=${hoy}`, "No se pudo cargar el resumen")
      .then((d) => vigente && setDatos(d))
      .catch((e: Error) => toast.error(e.message))
      .finally(() => vigente && setCargando(false));
    return () => { vigente = false; };
  }, [mes, hoy]);

  const f = datos?.finanzas ?? null;
  const g = datos?.gimnasio;

  return (
    <div className="viz-root space-y-6">
      <style>{ESTILO}</style>

      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm text-slate-500">
            {user?.rol === "Recepcion" ? "Estado de los socios y avisos del día." : "Cómo viene el mes: ventas, compras, caja, socios y avisos."}
          </p>
        </div>
        <div className="flex items-end gap-2">
          {f && (
            <div>
              <label htmlFor="dash-mes" className="mb-1 block text-xs font-medium text-slate-500">Mes</label>
              <input id="dash-mes" type="month" value={mes} max={hoy.slice(0, 7)} onChange={(e) => e.target.value && setMes(e.target.value)}
                className="rounded-md border border-slate-300 px-3 py-1.5 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500" />
            </div>
          )}
          {datos && (
            <button onClick={() => generarResumenPDF(datos)} className="inline-flex items-center rounded-md bg-marca-600 px-3 py-2 text-sm font-medium text-white hover:bg-marca-700">
              <Printer className="mr-1.5 h-4 w-4" /> Imprimir resumen
            </button>
          )}
        </div>
      </div>

      {cargando && !datos ? (
        <div className={`${tarjeta} text-center text-slate-500`}>Cargando...</div>
      ) : datos && g ? (
        <>
          {f && (
            <>
              <section aria-label="Indicadores del mes" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                <Indicador etiqueta={`Ventas de ${nombreDelMes(datos.mes)}`} valor={formatearPesos(f.ventas.neto)} nota={`${f.ventas.cantidad} comprobante(s), neto sin IVA`} />
                <Indicador etiqueta="Compras del mes" valor={formatearPesos(f.compras.neto)} nota={`${f.compras.cantidad} comprobante(s), neto sin IVA`} />
                <Indicador etiqueta="Resultado del mes" valor={formatearPesos(f.resultado)} nota="Ventas netas menos compras netas" />
                <Indicador etiqueta={f.ivaAPagar >= 0 ? "IVA a pagar" : "IVA a favor"} valor={formatearPesos(Math.abs(f.ivaAPagar))} nota="Débito fiscal menos crédito fiscal del mes" />
                <Indicador etiqueta="Saldo de Caja" valor={formatearPesos(f.cajaSaldo)} nota={f.cajaSaldo < 0 ? "Caja en negativo: revisá los movimientos" : "Igual al Libro Caja"} alerta={f.cajaSaldo < 0} />
                <Indicador etiqueta="Saldo de Banco" valor={formatearPesos(f.bancoSaldo)} nota="Igual al Libro Banco" />
                <Indicador etiqueta="Facturas anuladas" valor={String(f.facturasAnuladas)} nota="Del mes, no suman en los totales" />
              </section>

              <section className="grid gap-4 xl:grid-cols-3">
                <div className={`${tarjeta} xl:col-span-2`}>
                  <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                    <h2 className={titulo}>Ventas y compras, últimos 6 meses (neto)</h2>
                    <div className="flex items-center gap-4 text-xs text-slate-600">
                      <span className="flex items-center gap-1.5"><span className="h-2.5 w-2.5 rounded-sm" style={{ background: "var(--serie-ventas)" }} />Ventas</span>
                      <span className="flex items-center gap-1.5"><span className="h-2.5 w-2.5 rounded-sm" style={{ background: "var(--serie-compras)" }} />Compras</span>
                      <button onClick={() => setVerTabla((v) => !v)} className="font-medium text-marca-700 hover:text-marca-800">{verTabla ? "Ver gráfico" : "Ver tabla"}</button>
                    </div>
                  </div>
                  {verTabla ? (
                    <table className="w-full text-sm">
                      <thead><tr className="border-b border-slate-200 text-left text-slate-500"><th className="py-2 font-medium">Mes</th><th className="py-2 text-right font-medium">Ventas</th><th className="py-2 text-right font-medium">Compras</th></tr></thead>
                      <tbody>
                        {f.serie.map((m) => (
                          <tr key={m.mes} className="border-b border-slate-100"><td className="py-2">{nombreDelMes(m.mes)}</td><td className="py-2 text-right">{formatearPesos(m.ventas)}</td><td className="py-2 text-right">{formatearPesos(m.compras)}</td></tr>
                        ))}
                      </tbody>
                    </table>
                  ) : (
                    <GraficoVentasCompras serie={f.serie} />
                  )}
                </div>

                <div className={tarjeta}>
                  <h2 className={`${titulo} mb-3`}>Ingresos del mes por origen</h2>
                  <BarrasHorizontales vacio="Todavía no hay ventas en este mes."
                    filas={f.ingresosPorOrigen.map((o) => ({ etiqueta: o.origen, valor: o.neto }))} />
                  <h2 className={`${titulo} mb-3 mt-6`}>Productos más vendidos</h2>
                  <BarrasHorizontales vacio="No se vendieron productos de inventario este mes."
                    filas={f.topProductos.map((p) => ({ etiqueta: p.producto, valor: p.total, detalle: `${p.cantidad} unidad(es)` }))} />
                </div>
              </section>
            </>
          )}

          <section aria-label="Gimnasio" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
            <Indicador etiqueta="Socios con cuota al día" valor={String(g.sociosConCuotaAlDia)} />
            <Indicador etiqueta={`Cuotas que vencen en ${g.diasPorVencer} días`} valor={String(g.cuotasPorVencer)} nota="Para avisarles en recepción" alerta={g.cuotasPorVencer > 0} />
            <Indicador etiqueta="Socios con cuota vencida" valor={String(g.sociosVencidos)} />
            <Indicador etiqueta="Al día y sin rutina" valor={String(g.sociosSinRutina)} nota="Asignales una desde Socios" alerta={g.sociosSinRutina > 0} />
            <Indicador etiqueta="Rutinas vencidas" valor={String(g.rutinasVencidas)} nota="Hay que renovarlas" alerta={g.rutinasVencidas > 0} />
          </section>

          <section className={tarjeta} aria-label="Stock bajo">
            <h2 className={`${titulo} mb-3`}>Stock bajo (hasta {datos.umbralStockBajo} unidades)</h2>
            {datos.stockBajo.length === 0 ? (
              <p className="text-sm text-slate-500">Ningún producto con poco stock.</p>
            ) : (
              <ul className="grid gap-x-8 gap-y-2 sm:grid-cols-2">
                {datos.stockBajo.map((p) => (
                  <li key={p.id} className="flex items-center justify-between gap-3 border-b border-slate-100 py-1.5 text-sm">
                    <span className="truncate text-slate-700">{p.nombre}</span>
                    <span className="shrink-0 font-medium text-slate-900">{p.stock === 0 ? "Sin stock" : `${p.stock} u.`}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      ) : null}
    </div>
  );
}
