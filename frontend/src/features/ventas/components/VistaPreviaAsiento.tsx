import { useEffect, useState } from "react";
import { CheckCircle2 } from "lucide-react";
import { enviarJson, pedir } from "../../../api/rutinasApi";

interface Props {
  tipoOperacion: string;
  condicionPago: string;
  subtotal: number;
  iva: number;
  total: number;
}

interface Linea { cuentaId: number; codigo: string; cuenta: string; debe: number; haber: number; glosa: string }
interface Previa { lineas: Linea[]; totalDebe: number; totalHaber: number; efectos: string[] }

const pesos = new Intl.NumberFormat("es-AR", { style: "currency", currency: "ARS" });

/**
 * Muestra, antes de emitir, el asiento contable que se va a registrar. Lo calcula el servidor con la misma
 * función que arma el asiento real, así que lo que se ve es exactamente lo que se guarda.
 */
export function VistaPreviaAsiento({ tipoOperacion, condicionPago, subtotal, iva, total }: Props) {
  const [previa, setPrevia] = useState<Previa | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!(subtotal > 0)) {
      setPrevia(null);
      setError(null);
      return;
    }
    let vigente = true;
    const espera = setTimeout(() => {
      pedir<Previa>("/api/facturas/vista-previa-asiento", "No se pudo armar la vista previa",
        enviarJson("POST", { tipoOperacion, condicionPago, subtotal, iva, total }))
        .then((d) => { if (vigente) { setPrevia(d); setError(null); } })
        .catch((e: Error) => { if (vigente) { setPrevia(null); setError(e.message); } });
    }, 250);
    return () => { vigente = false; clearTimeout(espera); };
  }, [tipoOperacion, condicionPago, subtotal, iva, total]);

  return (
    <details open className="rounded-lg border border-slate-200 bg-white">
      <summary className="cursor-pointer select-none px-4 py-3 text-sm font-semibold text-slate-800">
        Vista previa del asiento contable
        <span className="ml-2 font-normal text-slate-500">cómo se va a registrar esta factura</span>
      </summary>
      <div className="border-t border-slate-100 px-4 py-3">
        {!(subtotal > 0) ? (
          <p className="text-sm text-slate-500">Cargá los ítems de la factura para ver el asiento que se va a generar.</p>
        ) : error ? (
          <p className="text-sm text-amber-700">{error}</p>
        ) : !previa ? (
          <p className="text-sm text-slate-500">Calculando...</p>
        ) : (
          <>
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 text-left text-slate-500">
                  <th className="py-1.5 font-medium">Cuenta</th>
                  <th className="py-1.5 text-right font-medium">Debe</th>
                  <th className="py-1.5 text-right font-medium">Haber</th>
                </tr>
              </thead>
              <tbody>
                {/* Primero las cuentas que se debitan y después las que se acreditan, sangradas, como en el Libro Diario. */}
                {[...previa.lineas].sort((a, b) => (b.debe > 0 ? 1 : 0) - (a.debe > 0 ? 1 : 0)).map((l, i) => (
                  <tr key={i} className="border-b border-slate-100">
                    <td className={`py-1.5 text-slate-800 ${l.haber > 0 ? "pl-8" : ""}`}>
                      {l.cuenta} <span className="text-xs text-slate-400">({l.codigo})</span>
                    </td>
                    <td className="py-1.5 text-right tabular-nums">{l.debe > 0 ? pesos.format(l.debe) : ""}</td>
                    <td className="py-1.5 text-right tabular-nums">{l.haber > 0 ? pesos.format(l.haber) : ""}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr className="font-semibold text-slate-900">
                  <td className="py-1.5">Totales</td>
                  <td className="py-1.5 text-right tabular-nums">{pesos.format(previa.totalDebe)}</td>
                  <td className="py-1.5 text-right tabular-nums">{pesos.format(previa.totalHaber)}</td>
                </tr>
              </tfoot>
            </table>
            <p className="mt-2 flex items-center gap-1.5 text-sm text-emerald-700">
              <CheckCircle2 className="h-4 w-4" aria-hidden />
              Partida doble: el Debe es igual al Haber.
            </p>
            <ul className="mt-2 list-disc space-y-0.5 pl-5 text-xs text-slate-500">
              {previa.efectos.map((e) => <li key={e}>{e}</li>)}
            </ul>
          </>
        )}
      </div>
    </details>
  );
}
