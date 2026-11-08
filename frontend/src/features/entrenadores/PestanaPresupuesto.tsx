import { useEffect, useState } from 'react';
import { Download } from 'lucide-react';
import { obtenerPresupuesto } from '../../api/horasApi';
import { generarPresupuestoPDF } from '../../utils/generarPresupuestoPDF';
import { formatoDeHoras, formatoDeImporte, hoyEnArgentina, mensajeDePeriodo, type Presupuesto } from './horasUtil';

const campo = 'rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-marca-500 focus:outline-none focus:ring-2 focus:ring-marca-500';
const boton = 'inline-flex items-center gap-1.5 rounded-md px-3 py-2 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50';
const botonPrimario = `${boton} bg-marca-600 text-white hover:bg-marca-700`;
const botonSecundario = `${boton} border border-slate-300 bg-white text-slate-700 hover:bg-slate-50`;

/**
 * Presupuesto de pago por período: por entrenador, los días con horas, las horas, el valor de la hora y el monto, más el total. Se arma en el
 * servidor (cada día con el valor que tenía al cargarlo) y se puede bajar en PDF. Por defecto, del 1 del mes hasta hoy.
 */
export function PestanaPresupuesto() {
  const hoy = hoyEnArgentina();
  const [desde, setDesde] = useState(`${hoy.slice(0, 7)}-01`);
  const [hasta, setHasta] = useState(hoy);
  const [presupuesto, setPresupuesto] = useState<Presupuesto | null>(null);
  const [fallo, setFallo] = useState<{ periodo: string; mensaje: string } | null>(null);
  const [intento, setIntento] = useState(0);

  const errorDelPeriodo = mensajeDePeriodo(desde, hasta);

  useEffect(() => {
    if (mensajeDePeriodo(desde, hasta) !== null) return;
    let cancelado = false;
    obtenerPresupuesto(desde, hasta)
      .then((p) => { if (!cancelado) setPresupuesto(p); })
      .catch((e: unknown) => {
        if (!cancelado) setFallo({ periodo: `${desde}|${hasta}`, mensaje: e instanceof Error ? e.message : 'No se pudo armar el presupuesto.' });
      });
    return () => { cancelado = true; };
  }, [desde, hasta, intento]);

  // Lo que se muestra es solo lo que corresponde al período elegido ahora: si cambia, o es inválido, lo viejo no se ve.
  const vigente = errorDelPeriodo === null && presupuesto?.desde === desde && presupuesto?.hasta === hasta ? presupuesto : null;
  const falloVigente = errorDelPeriodo === null && fallo?.periodo === `${desde}|${hasta}` ? fallo.mensaje : null;
  const cargando = errorDelPeriodo === null && vigente === null && falloVigente === null;
  const hayValoresVarios = vigente?.filas.some((f) => f.valorHora === null) ?? false;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-wrap items-start gap-3">
          <div>
            <label htmlFor="presupuesto-desde" className="mb-1 block text-xs font-semibold text-slate-600">Desde</label>
            <input id="presupuesto-desde" type="date" value={desde} onChange={(e) => setDesde(e.target.value)}
              aria-invalid={errorDelPeriodo !== null} aria-describedby={errorDelPeriodo ? 'presupuesto-error' : undefined} className={campo} />
          </div>
          <div>
            <label htmlFor="presupuesto-hasta" className="mb-1 block text-xs font-semibold text-slate-600">Hasta</label>
            <input id="presupuesto-hasta" type="date" value={hasta} onChange={(e) => setHasta(e.target.value)}
              aria-invalid={errorDelPeriodo !== null} aria-describedby={errorDelPeriodo ? 'presupuesto-error' : undefined} className={campo} />
          </div>
        </div>
        <button type="button" onClick={() => vigente && generarPresupuestoPDF(vigente)} disabled={!vigente || vigente.filas.length === 0} className={botonPrimario}
          title="Abre el presupuesto en PDF, en blanco y negro, para imprimir o guardar">
          <Download className="h-4 w-4" aria-hidden="true" /> Descargar PDF
        </button>
      </div>

      {errorDelPeriodo && <p id="presupuesto-error" role="alert" className="text-sm text-red-600">{errorDelPeriodo}</p>}

      {cargando && <div role="status" className="rounded-xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">Armando el presupuesto...</div>}

      {falloVigente && (
        <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-6 text-sm text-red-800">
          <p>{falloVigente}</p>
          <button type="button" onClick={() => { setFallo(null); setIntento((n) => n + 1); }} className={`${botonSecundario} mt-3`}>Reintentar</button>
        </div>
      )}

      {vigente && vigente.filas.length === 0 && (
        <p className="rounded-lg border border-dashed border-slate-300 bg-white px-4 py-3 text-sm text-slate-500">No hay horas cargadas en este período.</p>
      )}

      {vigente && vigente.filas.length > 0 && (
        <>
          <div className="relative overflow-x-auto rounded-xl border border-slate-200 bg-white">
            <table className="w-full min-w-[34rem] border-collapse text-sm">
              <caption className="sr-only">Presupuesto de pago a entrenadores del período elegido</caption>
              <thead>
                <tr className="bg-slate-50 text-slate-700">
                  <th scope="col" className="border-b border-slate-200 px-3 py-2.5 text-left font-semibold">Entrenador</th>
                  <th scope="col" className="border-b border-slate-200 px-3 py-2.5 text-right font-semibold">Días</th>
                  <th scope="col" className="border-b border-slate-200 px-3 py-2.5 text-right font-semibold">Horas</th>
                  <th scope="col" className="border-b border-slate-200 px-3 py-2.5 text-right font-semibold">Valor de la hora</th>
                  <th scope="col" className="border-b border-slate-200 px-3 py-2.5 text-right font-semibold">Monto</th>
                </tr>
              </thead>
              <tbody>
                {vigente.filas.map((f) => (
                  <tr key={f.entrenadorId}>
                    <th scope="row" className="border-t border-slate-200 px-3 py-2.5 text-left font-medium">{f.nombre}</th>
                    <td className="border-t border-slate-200 px-3 py-2.5 text-right tabular-nums">{f.dias}</td>
                    <td className="border-t border-slate-200 px-3 py-2.5 text-right tabular-nums">{formatoDeHoras(f.horas)}</td>
                    <td className="border-t border-slate-200 px-3 py-2.5 text-right tabular-nums">
                      {f.valorHora === null
                        ? <span title="Cambió dentro del período: el monto suma cada día con el valor que tenía.">varios</span>
                        : formatoDeImporte(f.valorHora)}
                    </td>
                    <td className="border-t border-slate-200 px-3 py-2.5 text-right font-medium tabular-nums">{formatoDeImporte(f.monto)}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr className="bg-slate-50 font-semibold">
                  <th scope="row" className="border-t-2 border-slate-300 px-3 py-2.5 text-left">Total</th>
                  <td className="border-t-2 border-slate-300 px-3 py-2.5" />
                  <td className="border-t-2 border-slate-300 px-3 py-2.5 text-right tabular-nums">{formatoDeHoras(vigente.totalHoras)}</td>
                  <td className="border-t-2 border-slate-300 px-3 py-2.5" />
                  <td className="border-t-2 border-slate-300 px-3 py-2.5 text-right tabular-nums">{formatoDeImporte(vigente.totalMonto)}</td>
                </tr>
              </tfoot>
            </table>
          </div>
          {hayValoresVarios && (
            <p className="text-xs text-slate-500">
              Valor de la hora «varios»: cambió dentro del período. El monto suma cada día con el valor que tenía cuando se cargó.
            </p>
          )}
        </>
      )}
    </div>
  );
}
