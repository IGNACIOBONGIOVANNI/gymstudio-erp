import { Fragment } from "react";
import type { AsientoDto } from "./asientosApi";
import { formatearFechaLocal } from "../utils/formatters";
import { agruparPorDia } from "../utils/totalesPorDia";

const moneda = new Intl.NumberFormat("es-AR", { style: "currency", currency: "ARS" });
const ESTADO_CONFIRMADO = 1;

/**
 * Libro Diario con el formato de la planilla: Fecha | Detalle | Debe | Haber, una
 * línea por cuenta y los totales al pie. Sólo los asientos confirmados (los anulados
 * se ven en la vista por asiento).
 */
export function DiarioPlanilla({ asientos }: { asientos: AsientoDto[] }) {
  const confirmados = asientos.filter((a) => a.estado === ESTADO_CONFIRMADO);
  const totalDebe = confirmados.reduce((s, a) => s + (a.totalDebe || 0), 0);
  const totalHaber = confirmados.reduce((s, a) => s + (a.totalHaber || 0), 0);
  const diferencia = Math.round((totalDebe - totalHaber) * 100) / 100;
  // Cada día cierra con su total; con un solo día no hace falta (sería el mismo número que el total general).
  const dias = agruparPorDia(confirmados);
  const conTotalPorDia = dias.length > 1;

  return (
    <div className="rounded-xl border border-slate-200/80 bg-white shadow-sm overflow-hidden">
      <div className="overflow-x-auto w-full">
        <table className="w-full min-w-[640px] text-sm">
          <thead className="bg-slate-50/50">
            <tr className="border-b-2 border-slate-300 text-slate-600">
              <th className="h-10 w-28 px-4 text-left font-semibold">Fecha</th>
              <th className="h-10 px-4 text-left font-semibold">Detalle</th>
              <th className="h-10 w-40 px-4 text-right font-semibold">Debe (D)</th>
              <th className="h-10 w-40 px-4 text-right font-semibold">Haber (H)</th>
            </tr>
          </thead>
          <tbody>
            {confirmados.length === 0 && (
              <tr><td colSpan={4} className="p-6 text-center text-slate-500">No hay asientos confirmados.</td></tr>
            )}
            {dias.map((d) => (
              <Fragment key={d.dia}>
                {d.asientos.map((a) => {
                  // Como en un libro: primero las cuentas al Debe, después las del Haber.
                  const lineas = [...a.lineas].sort((x, y) => (y.debe > 0 ? 1 : 0) - (x.debe > 0 ? 1 : 0) || x.orden - y.orden);
                  return (
                    <Fragment key={a.id}>
                      {lineas.map((l, i) => (
                        <tr key={l.id ?? i} className={i === 0 ? "border-t border-slate-200" : ""}>
                          <td className="px-4 py-1.5 whitespace-nowrap tabular-nums text-slate-600">{i === 0 ? formatearFechaLocal(a.fecha) : ""}</td>
                          <td className={`px-4 py-1.5 text-slate-800 ${l.haber > 0 ? "pl-12" : ""}`}>{l.nombreCuenta}</td>
                          <td className="px-4 py-1.5 text-right tabular-nums text-slate-800">{l.debe > 0 ? moneda.format(l.debe) : ""}</td>
                          <td className="px-4 py-1.5 text-right tabular-nums text-slate-800">{l.haber > 0 ? moneda.format(l.haber) : ""}</td>
                        </tr>
                      ))}
                      <tr>
                        <td />
                        <td colSpan={3} className="px-4 pb-2 text-xs italic text-slate-500">
                          As. N° {String(a.numeroAsiento).padStart(4, "0")} — {a.descripcion}
                        </td>
                      </tr>
                    </Fragment>
                  );
                })}
                {conTotalPorDia && (
                  <tr className="border-y border-slate-300 bg-slate-50 font-semibold text-slate-900">
                    <td className="px-4 py-2" />
                    <td className="px-4 py-2">Total del {formatearFechaLocal(d.dia)}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{moneda.format(d.debe)}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{moneda.format(d.haber)}</td>
                  </tr>
                )}
              </Fragment>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t-2 border-slate-400 bg-slate-50 font-bold text-slate-900">
              <td className="px-4 py-3" />
              <td className="px-4 py-3">TOTALES</td>
              <td className="px-4 py-3 text-right tabular-nums">{moneda.format(totalDebe)}</td>
              <td className="px-4 py-3 text-right tabular-nums">{moneda.format(totalHaber)}</td>
            </tr>
            <tr className="bg-slate-50 text-sm">
              <td />
              <td className="px-4 pb-3 text-slate-600">Diferencia (Debe − Haber)</td>
              <td colSpan={2} className={`px-4 pb-3 text-right font-semibold tabular-nums ${diferencia === 0 ? "text-slate-700" : "text-red-600"}`}>
                {moneda.format(diferencia)}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
}
