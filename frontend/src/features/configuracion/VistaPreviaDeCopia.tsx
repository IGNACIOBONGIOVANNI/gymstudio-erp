import { useEffect, useRef, useState } from 'react';
import { CircleCheck, Info, Loader2, TriangleAlert, X } from 'lucide-react';
import { descargarCopia, guardarArchivo, restaurarCopia, type ResultadoDeRestauracion } from '../../api/copiasApi';
import {
  CONFIRMACION_DE_RESTAURACION, agruparPorModulo, confirmacionValida, diferenciaDeFilas, nombreDeArchivoDeCopia,
  resumirComparacion, textoDeFilas, textoDeFirma, type ComparacionDeCopia, type TablaComparada,
} from './copiasUtil';

interface Props {
  archivo: File;
  comparacion: ComparacionDeCopia;
  onCerrar: () => void;
}

type Paso = 'vista' | 'confirmar' | 'trabajando' | 'listo';

const plural = (n: number, uno: string, varios: string) => `${n} ${n === 1 ? uno : varios}`;

function resultadoDeTabla(t: TablaComparada): { texto: string; clase: string } {
  if (t.esAuditoria && t.estado === 'cambia') return { texto: 'Se actualiza sola', clase: 'text-slate-500' };
  switch (t.estado) {
    case 'igual': return { texto: 'Igual', clase: 'text-slate-500' };
    case 'cambia': return { texto: 'Cambia', clase: 'bg-amber-100 text-amber-900' };
    case 'solo-copia': return { texto: 'Solo en la copia', clase: 'bg-red-100 text-red-800' };
    case 'solo-ahora': return { texto: 'Solo en el sistema', clase: 'bg-red-100 text-red-800' };
  }
}

export function VistaPreviaDeCopia({ archivo, comparacion, onCerrar }: Props) {
  const [paso, setPaso] = useState<Paso>('vista');
  const [soloCambios, setSoloCambios] = useState(false);
  const [confirmacion, setConfirmacion] = useState('');
  const [trabajo, setTrabajo] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [resultado, setResultado] = useState<ResultadoDeRestauracion | null>(null);
  const principal = useRef<HTMLButtonElement>(null);

  const resumen = resumirComparacion(comparacion.tablas);
  const hayDiferencias = resumen.cambian + resumen.soloEnLaCopia + resumen.soloAhora > 0;
  const visibles = soloCambios ? comparacion.tablas.filter((t) => t.estado !== 'igual') : comparacion.tablas;
  const grupos = agruparPorModulo(visibles);

  useEffect(() => {
    principal.current?.focus();
    const tecla = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && (paso === 'vista' || paso === 'confirmar')) onCerrar();
    };
    document.addEventListener('keydown', tecla);
    return () => document.removeEventListener('keydown', tecla);
  }, [paso, onCerrar]);

  const restaurar = async () => {
    setError(null);
    setPaso('trabajando');
    try {
      setTrabajo('Guardando en tu PC una copia de lo que hay ahora, por si te arrepentís…');
      guardarArchivo(await descargarCopia(), nombreDeArchivoDeCopia(new Date(), 'gymstudio_antes-de-restaurar'));
      setTrabajo('Restaurando la copia…');
      setResultado(await restaurarCopia(archivo, confirmacion.trim()));
      setPaso('listo');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo restaurar la copia. No se cambió nada.');
      setPaso('confirmar');
    }
  };

  const cerrarTrasRestaurar = () => window.location.reload();

  const fecha = comparacion.fecha ? new Date(comparacion.fecha).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' }) : 'sin fecha';

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget && paso === 'vista') onCerrar();
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="vista-copia-titulo"
        className="flex max-h-[calc(100vh-2rem)] w-[95%] max-w-4xl flex-col overflow-hidden rounded-xl bg-white shadow-2xl"
      >
        <div className="flex items-start justify-between gap-4 bg-slate-900 px-6 py-4">
          <div className="min-w-0">
            <h2 id="vista-copia-titulo" className="text-lg font-semibold leading-tight text-white">Vista previa de la copia de seguridad</h2>
            <p className="mt-0.5 text-sm text-slate-400">
              {paso === 'trabajando'
                ? 'Restaurando la copia: no cierres esta ventana.'
                : paso === 'listo'
                  ? 'La restauración terminó.'
                  : 'Compara toda la copia con el sistema actual. Todavía no se cambió nada.'}
            </p>
          </div>
          {(paso === 'vista' || paso === 'confirmar') && (
            <button type="button" onClick={onCerrar} aria-label="Cerrar" className="shrink-0 text-slate-400 transition-colors hover:text-white">
              <X className="h-5 w-5" />
            </button>
          )}
        </div>

        {paso === 'vista' && (
          <>
            <div className="space-y-4 overflow-y-auto p-6">
              <dl className="grid gap-x-6 gap-y-1 text-[13px] sm:grid-cols-2">
                <div><dt className="inline text-slate-500">Archivo: </dt><dd className="inline break-all font-medium text-slate-800">{archivo.name}</dd></div>
                <div><dt className="inline text-slate-500">Hecha el: </dt><dd className="inline font-medium text-slate-800">{fecha}</dd></div>
                <div>
                  <dt className="inline text-slate-500">Versión del sistema: </dt>
                  <dd className="inline font-medium text-slate-800">
                    {comparacion.copia.version === comparacion.actual.version
                      ? `${comparacion.copia.version} (la misma de ahora)`
                      : `${comparacion.copia.version} en la copia, ${comparacion.actual.version} ahora`}
                  </dd>
                </div>
                <div>
                  <dt className="inline text-slate-500">Base de datos: </dt>
                  <dd className="inline font-medium text-slate-800">
                    {comparacion.mismoEsquema ? `misma versión (${comparacion.actual.migraciones} migraciones)` : 'versión distinta a la actual'}
                  </dd>
                </div>
                <div className="sm:col-span-2">
                  <dt className="inline text-slate-500">Firma de la copia: </dt>
                  <dd className={`inline font-medium ${textoDeFirma(comparacion.firma).ok ? 'text-emerald-700' : 'text-amber-700'}`}>
                    {textoDeFirma(comparacion.firma).texto}
                  </dd>
                </div>
              </dl>

              <Veredicto comparacion={comparacion} cambian={resumen.cambian} iguales={resumen.iguales} />

              <div className="flex flex-wrap items-center justify-between gap-3">
                <p className="text-[13px] text-slate-500">
                  La copia trae todos los datos: facturas, asientos, caja, banco, stock, socios, usuarios y auditoría.
                </p>
                {hayDiferencias && (
                  <label className="inline-flex cursor-pointer items-center gap-2 text-[13px] text-slate-600">
                    <input type="checkbox" checked={soloCambios} onChange={(e) => setSoloCambios(e.target.checked)} className="h-4 w-4 accent-marca-600" />
                    Ver solo lo que cambia
                  </label>
                )}
              </div>

              <div className="overflow-x-auto rounded-lg ring-1 ring-slate-200">
                <table className="w-full min-w-[36rem] border-collapse text-left text-sm">
                  <thead>
                    <tr className="bg-slate-100 text-slate-700">
                      <th className="px-3 py-2 font-semibold">Datos</th>
                      <th className="px-3 py-2 text-right font-semibold">En la copia</th>
                      <th className="px-3 py-2 text-right font-semibold">Ahora</th>
                      <th className="px-3 py-2 font-semibold">Resultado</th>
                    </tr>
                  </thead>
                  <tbody>
                    {grupos.map((g) => (
                      <GrupoDeTablas key={g.grupo} grupo={g.grupo} tablas={g.tablas} />
                    ))}
                  </tbody>
                </table>
              </div>

              {!comparacion.puedeRestaurar && comparacion.motivoSiNoPuede && (
                <Aviso ojo>No se puede restaurar desde la pantalla: {comparacion.motivoSiNoPuede}</Aviso>
              )}
              <Aviso>
                La copia incluye las contraseñas de los usuarios (guardadas con hash): guardala en un lugar privado.
              </Aviso>
            </div>
            <Pie>
              <button type="button" onClick={onCerrar} ref={principal} className={botonSecundario}>Cerrar</button>
              {comparacion.puedeRestaurar && comparacion.veredicto !== 'identica' && (
                <button type="button" onClick={() => setPaso('confirmar')} className="rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-red-700">
                  Restaurar esta copia…
                </button>
              )}
            </Pie>
          </>
        )}

        {paso === 'confirmar' && (
          <>
            <div className="space-y-4 overflow-y-auto p-6">
              <div className="flex gap-3 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm leading-6 text-red-900">
                <TriangleAlert className="mt-1 h-5 w-5 shrink-0" aria-hidden="true" />
                <div className="space-y-2">
                  <p className="font-semibold">Se van a reemplazar TODOS los datos del sistema por los de esta copia.</p>
                  <p>
                    Facturas, asientos, caja, banco, stock, socios y usuarios vuelven al estado de la copia{comparacion.fecha ? ` (hecha el ${fecha})` : ''}. Lo que se cargó después se pierde
                    {hayDiferencias ? `: ${plural(resumen.cambian, 'tabla cambia', 'tablas cambian')} (mirá la vista previa)` : ''}.
                  </p>
                  <p>Antes de restaurar, el sistema descarga a tu PC una copia de lo que hay ahora, por si te arrepentís. Si algo falla en el medio, no cambia nada.</p>
                </div>
              </div>
              {error && <Aviso ojo>{error}</Aviso>}
              <label className="block text-sm text-slate-700">
                Para continuar, escribí <strong className="font-mono">{CONFIRMACION_DE_RESTAURACION}</strong>
                <input
                  type="text"
                  value={confirmacion}
                  onChange={(e) => setConfirmacion(e.target.value)}
                  autoComplete="off"
                  spellCheck={false}
                  className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-sm focus:border-red-500 focus:outline-none focus:ring-2 focus:ring-red-200"
                />
              </label>
            </div>
            <Pie>
              <button type="button" onClick={() => { setPaso('vista'); setError(null); setConfirmacion(''); }} className={botonSecundario}>Volver</button>
              <button
                type="button"
                onClick={restaurar}
                disabled={!confirmacionValida(confirmacion)}
                className="rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-40"
              >
                Restaurar ahora
              </button>
            </Pie>
          </>
        )}

        {paso === 'trabajando' && (
          <div className="flex flex-col items-center gap-3 px-6 py-12 text-center">
            <Loader2 className="h-8 w-8 animate-spin text-slate-500" aria-hidden="true" />
            <p className="text-sm text-slate-700" role="status">{trabajo}</p>
            <p className="text-[13px] text-slate-500">No cierres esta ventana.</p>
          </div>
        )}

        {paso === 'listo' && resultado && (
          <>
            <div className="space-y-3 p-6">
              <div className="flex gap-3 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm leading-6 text-emerald-900">
                <CircleCheck className="mt-1 h-5 w-5 shrink-0" aria-hidden="true" />
                <div>
                  <p className="font-semibold">Listo: el sistema volvió al estado de la copia.</p>
                  <p>
                    Se restauraron {plural(resultado.tablas, 'tabla', 'tablas')} ({resultado.filas.toLocaleString('es-AR')} filas).
                    Controles de integridad contable: {resultado.integridad.correctos} de {resultado.integridad.total} correctos.
                  </p>
                </div>
              </div>
              <p className="text-[13px] text-slate-500">
                Como también se restauraron los usuarios, conviene volver a cargar el sistema. La copia de lo que había antes quedó en tu PC
                (el archivo gymstudio_antes-de-restaurar_fecha_hora.zip, en la carpeta de descargas).
              </p>
            </div>
            <Pie>
              <button type="button" onClick={cerrarTrasRestaurar} ref={principal} className="rounded-lg bg-marca-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-marca-700">
                Volver a cargar el sistema
              </button>
            </Pie>
          </>
        )}
      </div>
    </div>
  );
}

const botonSecundario = 'rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50';

function Pie({ children }: { children: React.ReactNode }) {
  return <div className="flex flex-wrap justify-end gap-3 border-t border-slate-100 bg-white px-6 py-4">{children}</div>;
}

function Veredicto({ comparacion, cambian, iguales }: { comparacion: ComparacionDeCopia; cambian: number; iguales: number }) {
  const { veredicto } = comparacion;
  const estilo = veredicto === 'identica' ? 'border-emerald-200 bg-emerald-50 text-emerald-900'
    : veredicto === 'anterior' ? 'border-amber-200 bg-amber-50 text-amber-900'
    : 'border-red-200 bg-red-50 text-red-900';
  const Icono = veredicto === 'identica' ? CircleCheck : TriangleAlert;
  return (
    <div className={`flex gap-3 rounded-lg border px-4 py-3 text-sm leading-6 ${estilo}`}>
      <Icono className="mt-1 h-5 w-5 shrink-0" aria-hidden="true" />
      <p>
        {veredicto === 'identica' && <strong>La copia es idéntica al sistema actual: no hay nada que restaurar.</strong>}
        {veredicto === 'anterior' && (
          <>
            <strong>La copia es de un estado anterior del sistema:</strong> {plural(cambian, 'tabla de datos cambió', 'tablas de datos cambiaron')} desde que se hizo
            y {plural(iguales, 'es igual', 'son iguales')}.
          </>
        )}
        {veredicto === 'otra-version' && <strong>La copia es de otra versión de la base de datos que la actual.</strong>}
      </p>
    </div>
  );
}

function GrupoDeTablas({ grupo, tablas }: { grupo: string; tablas: TablaComparada[] }) {
  return (
    <>
      <tr>
        <th colSpan={4} className="bg-slate-50 px-3 py-1.5 text-xs font-semibold text-slate-500">{grupo}</th>
      </tr>
      {tablas.map((t) => {
        const r = resultadoDeTabla(t);
        const cambia = t.estado !== 'igual' && !t.esAuditoria;
        const diferencia = diferenciaDeFilas(t);
        return (
          <tr key={t.tabla} className={`border-t border-slate-100 ${cambia ? 'bg-amber-50/60' : ''}`}>
            <td className="px-3 py-2 font-medium text-slate-800">{t.etiqueta}</td>
            <td className="px-3 py-2 text-right tabular-nums text-slate-700">{textoDeFilas(t.filasCopia)}</td>
            <td className="px-3 py-2 text-right tabular-nums text-slate-700">{textoDeFilas(t.filasAhora)}</td>
            <td className="px-3 py-2 text-[13px]">
              <span className={`inline-flex rounded-full px-2 py-0.5 font-medium ${r.clase}`}>{r.texto}</span>
              {diferencia && !t.esAuditoria && <span className="ml-2 tabular-nums text-slate-500">{diferencia} filas</span>}
            </td>
          </tr>
        );
      })}
    </>
  );
}

function Aviso({ ojo, children }: { ojo?: boolean; children: React.ReactNode }) {
  const Icono = ojo ? TriangleAlert : Info;
  return (
    <p className={`flex gap-2 rounded-lg px-3 py-2 text-[13px] leading-5 ${ojo ? 'bg-amber-50 text-amber-900' : 'bg-slate-50 text-slate-600'}`}>
      <Icono className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
      <span>{children}</span>
    </p>
  );
}
