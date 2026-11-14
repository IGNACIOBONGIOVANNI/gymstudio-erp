import { useEffect, useMemo, useState } from 'react';
import { CalendarCheck, Loader2, Plus, Save } from 'lucide-react';
import toast from 'react-hot-toast';
import { guardarHoras, obtenerHoras, precargarHoras } from '../../api/horasApi';
import {
  borradorDesde, cambiosDelBorrador, claveDeDia, conDiaNuevo, formatoDeHoras, formatoDeImporte, hoyEnArgentina, mensajeDeHoras, nombreDelDia,
  rangoDelMes, resumenPorEntrenador, type Borrador, type DiaDeHoras, type HorasDelPeriodo,
} from './horasUtil';
import { numeroDecimal } from '../../utils/entradaNumerica';

const boton = 'inline-flex items-center gap-1.5 rounded-md px-3 py-2 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50';
const botonPrimario = `${boton} bg-marca-600 text-white hover:bg-marca-700`;
const botonSecundario = `${boton} border border-slate-300 bg-white text-slate-700 hover:bg-slate-50`;
const campo = 'rounded-lg border px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400';
const claseDelCampo = (error: boolean) =>
  `${campo} ${error ? 'border-red-400 focus:border-red-400 focus:ring-red-400' : 'border-slate-300 focus:border-marca-500 focus:ring-marca-500'}`;

const aDDMM = (fecha: string) => fecha.slice(8, 10) + '/' + fecha.slice(5, 7);
const textoDeDias = (n: number) => `${n} ${n === 1 ? 'día' : 'días'}`;

interface Props {
  /** Avisa si hay algo escrito sin guardar (para preguntar antes de irse de la pestaña o de la pantalla). */
  onCambios: (hay: boolean) => void;
  /** Lleva a la lista de fichas, donde se carga el valor de la hora. */
  onIrALaLista: () => void;
}

/**
 * Las horas que cumplió cada entrenador, por día, de un mes. Cada día trae las horas que le tocaban según el calendario («Le tocaba») y lo
 * que se cargó («Cumplió»): vacío es «sin cargar» y 0 es «faltó». «Precargar desde el calendario» carga de una vez lo que le tocaba hasta
 * hoy; después se corrige lo que haga falta. Cada día guarda el valor de la hora que tenía al cargarse.
 */
export function PestanaHoras({ onCambios, onIrALaLista }: Props) {
  const hoy = hoyEnArgentina();
  const [mes, setMes] = useState(hoy.slice(0, 7));
  const [datos, setDatos] = useState<{ mes: string; entrenadores: HorasDelPeriodo['entrenadores'] } | null>(null);
  const [dias, setDias] = useState<DiaDeHoras[]>([]);
  const [borrador, setBorrador] = useState<Borrador>({});
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null);
  const [intento, setIntento] = useState(0);
  const [guardando, setGuardando] = useState(false);
  const [precargando, setPrecargando] = useState(false);
  // Después de guardar o precargar la pantalla vuelve a leer lo guardado y reemplaza todo lo escrito: hasta que llega esa lectura no se puede escribir.
  const [recargando, setRecargando] = useState(false);
  const [errorDeGuardado, setErrorDeGuardado] = useState<string | null>(null);
  const [precarga, setPrecarga] = useState<{ creados: number; sinValor: string[] } | null>(null);
  const [mesPendiente, setMesPendiente] = useState<string | null>(null);
  const [nuevoEntrenador, setNuevoEntrenador] = useState('');
  const [nuevaFecha, setNuevaFecha] = useState('');
  const [errorDelDiaNuevo, setErrorDelDiaNuevo] = useState<string | null>(null);

  useEffect(() => {
    let cancelado = false;
    const { desde, hasta } = rangoDelMes(mes);
    obtenerHoras(desde, hasta)
      .then((r) => {
        if (cancelado) return;
        setDatos({ mes, entrenadores: r.entrenadores });
        setDias(r.dias);
        setBorrador(borradorDesde(r.dias));
        setErrorDeCarga(null);
        setRecargando(false);
      })
      .catch((e: unknown) => {
        if (cancelado) return;
        setErrorDeCarga(e instanceof Error ? e.message : 'No se pudieron cargar las horas.');
        setRecargando(false);
      });
    return () => { cancelado = true; };
  }, [mes, intento]);

  const cambios = useMemo(() => cambiosDelBorrador(dias, borrador), [dias, borrador]);
  const hayErrores = useMemo(() => dias.some((d) => mensajeDeHoras(borrador[claveDeDia(d.entrenadorId, d.fecha)]?.horas ?? '') !== null), [dias, borrador]);
  const haySinGuardar = cambios.length > 0 || hayErrores;
  const resumen = useMemo(() => resumenPorEntrenador(dias, borrador), [dias, borrador]);
  const cambiadas = useMemo(() => new Set(cambios.map((c) => claveDeDia(c.entrenadorId, c.fecha))), [cambios]);

  // Avisa a quien tiene arriba (pestañas y menú) si hay algo sin guardar, y al navegador si se intenta cerrar o recargar la página.
  useEffect(() => {
    onCambios(haySinGuardar);
    return () => onCambios(false);
  }, [haySinGuardar, onCambios]);

  useEffect(() => {
    if (!haySinGuardar) return;
    const avisar = (e: BeforeUnloadEvent) => { e.preventDefault(); };
    window.addEventListener('beforeunload', avisar);
    return () => window.removeEventListener('beforeunload', avisar);
  }, [haySinGuardar]);

  const irAlMes = (nuevo: string) => {
    setMesPendiente(null);
    setErrorDeGuardado(null);
    setErrorDeCarga(null);
    setPrecarga(null);
    setErrorDelDiaNuevo(null);
    setMes(nuevo);
  };

  const cambiarMes = (nuevo: string) => {
    if (!/^\d{4}-\d{2}$/.test(nuevo) || nuevo === mes) return;
    if (haySinGuardar) { setMesPendiente(nuevo); return; }
    irAlMes(nuevo);
  };

  const escribir = (d: DiaDeHoras, que: 'horas' | 'nota', valor: string) => {
    const clave = claveDeDia(d.entrenadorId, d.fecha);
    setBorrador((actual) => ({ ...actual, [clave]: { ...(actual[clave] ?? { horas: '', nota: '' }), [que]: valor } }));
  };

  const guardar = async () => {
    setGuardando(true);
    setErrorDeGuardado(null);
    try {
      await guardarHoras(cambios);
      toast.success('Horas guardadas.');
      setRecargando(true);
      setIntento((n) => n + 1);   // vuelve a leer lo guardado: así se ven también los valores de la hora de los días nuevos
    } catch (error) {
      setErrorDeGuardado(error instanceof Error ? error.message : 'No se pudieron guardar las horas.');
    } finally {
      setGuardando(false);
    }
  };

  const precargar = async () => {
    setPrecargando(true);
    setErrorDeGuardado(null);
    setPrecarga(null);
    try {
      const { desde, hasta } = rangoDelMes(mes);
      setPrecarga(await precargarHoras(desde, hasta));
      setRecargando(true);
      setIntento((n) => n + 1);
    } catch (error) {
      setErrorDeGuardado(error instanceof Error ? error.message : 'No se pudieron precargar las horas.');
    } finally {
      setPrecargando(false);
    }
  };

  const agregarDia = () => {
    const id = Number(nuevoEntrenador);
    const { desde, hasta } = rangoDelMes(mes);
    if (!id) { setErrorDelDiaNuevo('Elegí un entrenador.'); return; }
    if (!/^\d{4}-\d{2}-\d{2}$/.test(nuevaFecha)) { setErrorDelDiaNuevo('Elegí la fecha del día.'); return; }
    if (nuevaFecha < desde || nuevaFecha > hasta) { setErrorDelDiaNuevo('La fecha tiene que estar dentro del mes elegido.'); return; }
    if (nuevaFecha > hoy) { setErrorDelDiaNuevo('No se pueden cargar horas de un día que todavía no pasó.'); return; }
    const resultado = conDiaNuevo(dias, borrador, id, nuevaFecha);
    setDias(resultado.dias);
    setBorrador(resultado.borrador);
    setErrorDelDiaNuevo(null);
    const fecha = nuevaFecha;
    setNuevaFecha('');
    setTimeout(() => document.getElementById(`horas-${id}-${fecha}`)?.focus(), 0);
  };

  const cargando = datos === null || datos.mes !== mes;
  const ocupado = guardando || precargando || recargando;

  if (cargando && errorDeCarga) {
    return (
      <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-6 text-sm text-red-800">
        <p>{errorDeCarga}</p>
        <button type="button" onClick={() => { setErrorDeCarga(null); setIntento((n) => n + 1); }} className={`${botonSecundario} mt-3`}>Reintentar</button>
      </div>
    );
  }

  const entrenadores = datos?.mes === mes ? datos.entrenadores : [];
  // Solo se le pueden crear días nuevos a un entrenador activo y con valor de la hora (el servidor lo exige; acá no se ofrecen los demás).
  const conValor = entrenadores.filter((e) => e.valorHora !== null && e.activo);
  const { desde: primerDia, hasta: ultimoDia } = rangoDelMes(mes);
  // El error del servidor que nombra a un entrenador va junto a su sección; el resto, arriba.
  // Si un nombre contiene a otro (Ana y Ana María), gana el más largo.
  const entrenadorDelError = errorDeGuardado
    ? entrenadores.filter((e) => errorDeGuardado.includes(e.nombre)).sort((a, b) => b.nombre.length - a.nombre.length)[0]
    : undefined;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <label htmlFor="horas-mes" className="mb-1 block text-xs font-semibold text-slate-600">Mes</label>
          <input id="horas-mes" type="month" value={mes} max={hoy.slice(0, 7)} onChange={(e) => cambiarMes(e.target.value)} disabled={ocupado}
            className={claseDelCampo(false)} />
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button type="button" onClick={() => void precargar()} disabled={cargando || ocupado || haySinGuardar} className={botonSecundario}
            title={haySinGuardar ? 'Guardá o descartá los cambios antes de precargar.' : 'Carga las horas que le tocaban a cada entrenador según el calendario, hasta hoy, sin pisar lo ya cargado'}>
            {precargando ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <CalendarCheck className="h-4 w-4" aria-hidden="true" />}
            {precargando ? 'Precargando...' : 'Precargar desde el calendario'}
          </button>
          <button type="button" onClick={() => void guardar()} disabled={cargando || ocupado || cambios.length === 0 || hayErrores} className={botonPrimario}
            title={hayErrores ? 'Corregí las horas marcadas en rojo.' : undefined}>
            {guardando ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Save className="h-4 w-4" aria-hidden="true" />}
            {guardando ? 'Guardando...' : 'Guardar cambios'}
          </button>
        </div>
      </div>

      <p className="max-w-3xl text-sm text-slate-500">
        «Le tocaba» son las horas del calendario. En «Cumplió» escribí las horas que hizo de verdad, de a cuarto de hora (por ejemplo 7,5); dejalo
        vacío si todavía no lo cargaste y poné 0 si faltó. Cada día guarda el valor de la hora que tenía al cargarse.
        {haySinGuardar && <strong className="ml-1 text-amber-700">Tenés cambios sin guardar.</strong>}
      </p>

      {mesPendiente && (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          <span>Hay cambios sin guardar. ¿Los descartás?</span>
          <span className="flex gap-2">
            <button type="button" onClick={() => setMesPendiente(null)} className={botonSecundario}>Seguir editando</button>
            <button type="button" onClick={() => irAlMes(mesPendiente)} className={`${boton} bg-red-600 text-white hover:bg-red-700`}>Sí, descartar</button>
          </span>
        </div>
      )}

      {precarga && (
        <div role="status" className="space-y-1 rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
          <p>
            {precarga.creados === 0
              ? 'No había días para precargar: ya está todo cargado o el calendario no tiene horas hasta hoy.'
              : `Se cargaron ${textoDeDias(precarga.creados)}.`}
          </p>
          {precarga.sinValor.length > 0 && (
            <p className="text-amber-900">
              Faltan valores de la hora para: <strong>{precarga.sinValor.join(', ')}</strong>.{' '}
              <button type="button" onClick={onIrALaLista} className="font-medium text-marca-700 underline hover:text-marca-800">Ir a la lista</button>
            </p>
          )}
        </div>
      )}

      {errorDeCarga && !cargando && (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
          <span>No se pudo actualizar la pantalla: {errorDeCarga} Lo que ves puede estar desactualizado.</span>
          <button type="button" onClick={() => { setErrorDeCarga(null); setRecargando(true); setIntento((n) => n + 1); }} className={botonSecundario}>Reintentar</button>
        </div>
      )}

      {errorDeGuardado && !entrenadorDelError && (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">{errorDeGuardado}</p>
      )}

      <div className="flex flex-wrap items-end gap-3 rounded-lg border border-slate-200 bg-white px-4 py-3">
        <div>
          <label htmlFor="horas-nuevo-entrenador" className="mb-1 block text-xs font-semibold text-slate-600">Agregar un día de</label>
          <select id="horas-nuevo-entrenador" value={nuevoEntrenador} onChange={(e) => setNuevoEntrenador(e.target.value)} disabled={cargando || ocupado || conValor.length === 0}
            className={claseDelCampo(false)}>
            <option value="">{conValor.length === 0 ? 'Ningún entrenador con valor de la hora' : 'Elegí un entrenador'}</option>
            {conValor.map((e) => <option key={e.id} value={e.id}>{e.nombre}</option>)}
          </select>
        </div>
        <div>
          <label htmlFor="horas-nueva-fecha" className="mb-1 block text-xs font-semibold text-slate-600">Fecha</label>
          <input id="horas-nueva-fecha" type="date" value={nuevaFecha} min={primerDia} max={hoy < ultimoDia ? hoy : ultimoDia} onChange={(e) => setNuevaFecha(e.target.value)}
            disabled={cargando || ocupado} className={claseDelCampo(errorDelDiaNuevo !== null)} />
        </div>
        <button type="button" onClick={agregarDia} disabled={cargando || ocupado} className={botonSecundario}>
          <Plus className="h-4 w-4" aria-hidden="true" /> Agregar
        </button>
        <p className="basis-full text-xs text-slate-500">Para un día que el calendario no tenía, por ejemplo un sábado o un reemplazo.</p>
        {errorDelDiaNuevo && <p role="alert" className="basis-full text-sm text-red-600">{errorDelDiaNuevo}</p>}
      </div>

      {cargando ? (
        <div role="status" className="rounded-xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">Cargando las horas...</div>
      ) : entrenadores.every((e) => !dias.some((d) => d.entrenadorId === e.id)) ? (
        <p className="rounded-lg border border-dashed border-slate-300 bg-white px-4 py-3 text-sm text-slate-500">
          No hay horas programadas ni cargadas en este mes. Si ya hay entrenadores en el calendario, apretá «Precargar desde el calendario»; si no, agregá un día.
        </p>
      ) : (
        entrenadores.filter((e) => dias.some((d) => d.entrenadorId === e.id)).map((e) => {
          const suyos = dias.filter((d) => d.entrenadorId === e.id);
          const suResumen = resumen.find((r) => r.entrenadorId === e.id);
          return (
            <section key={e.id} aria-labelledby={`horas-titulo-${e.id}`} className="rounded-xl border border-slate-200 bg-white">
              <header className="space-y-1 border-b border-slate-200 px-4 py-3">
                <div className="flex flex-wrap items-baseline justify-between gap-2">
                  <h3 id={`horas-titulo-${e.id}`} className="text-sm font-semibold text-slate-900">{e.nombre}</h3>
                  <p className="text-sm text-slate-600">
                    Cargadas: <strong className="tabular-nums">{formatoDeHoras(suResumen?.cargadas ?? 0)}</strong> h de <span className="tabular-nums">{formatoDeHoras(suResumen?.programadas ?? 0)}</span> h que le tocaban
                  </p>
                </div>
                {e.valorHora === null ? (
                  <p className="text-sm text-amber-900">
                    Cargá el valor de la hora para poder cargarle horas.{' '}
                    <button type="button" onClick={onIrALaLista} className="font-medium text-marca-700 underline hover:text-marca-800">Ir a la lista</button>
                  </p>
                ) : (
                  <p className="text-xs text-slate-500">Valor de la hora hoy: {formatoDeImporte(e.valorHora)}. Los días ya cargados conservan el valor que tenían.</p>
                )}
                {entrenadorDelError === e && <p role="alert" className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">{errorDeGuardado}</p>}
              </header>
              <div className="relative overflow-x-auto">
                <table className="w-full min-w-[40rem] border-collapse text-sm">
                  <caption className="sr-only">Horas de {e.nombre} en el mes elegido</caption>
                  <thead>
                    <tr className="bg-slate-50 text-slate-700">
                      <th scope="col" className="border-b border-slate-200 px-3 py-2 text-left font-semibold">Día</th>
                      <th scope="col" className="border-b border-slate-200 px-3 py-2 text-right font-semibold">Le tocaba</th>
                      <th scope="col" className="border-b border-slate-200 px-3 py-2 text-left font-semibold">Cumplió</th>
                      <th scope="col" className="border-b border-slate-200 px-3 py-2 text-right font-semibold">Valor de la hora</th>
                      <th scope="col" className="border-b border-slate-200 px-3 py-2 text-left font-semibold">Nota</th>
                    </tr>
                  </thead>
                  <tbody>
                    {suyos.map((d) => {
                      const clave = claveDeDia(d.entrenadorId, d.fecha);
                      const escrito = borrador[clave] ?? { horas: '', nota: '' };
                      const error = mensajeDeHoras(escrito.horas);
                      const futuro = d.fecha > hoy;
                      // Un día sin cargar de un entrenador sin valor de la hora no se puede cargar (el servidor lo rechaza): se avisa en el encabezado.
                      const sinValor = d.cumplidas === null && e.valorHora === null;
                      const bloqueado = futuro || sinValor || ocupado;
                      return (
                        <tr key={clave} className={fondoDelRenglon(cambiadas.has(clave), futuro)}>
                          <th scope="row" className="whitespace-nowrap border-t border-slate-200 px-3 py-2 text-left font-medium tabular-nums">
                            <span className="inline-block w-9 text-xs font-normal uppercase text-slate-500">{nombreDelDia(d.fecha)}</span>{aDDMM(d.fecha)}
                          </th>
                          <td className="border-t border-slate-200 px-3 py-2 text-right tabular-nums">{d.programadas > 0 ? formatoDeHoras(d.programadas) : '—'}</td>
                          <td className="border-t border-slate-200 px-3 py-2 align-top">
                            <input id={`horas-${d.entrenadorId}-${d.fecha}`} type="text" inputMode="decimal" autoComplete="off" value={escrito.horas} disabled={bloqueado}
                              onChange={(ev) => escribir(d, 'horas', numeroDecimal(ev.target.value, 2, 5, ','))}
                              aria-label={`Horas que cumplió ${e.nombre} el ${aDDMM(d.fecha)}`} aria-invalid={error !== null}
                              aria-describedby={error ? `error-${d.entrenadorId}-${d.fecha}` : undefined}
                              title={futuro ? 'Este día todavía no pasó.' : sinValor ? 'Cargá primero el valor de la hora.' : undefined}
                              className={`${claseDelCampo(error !== null)} w-24 text-right tabular-nums`} />
                            {error && <p id={`error-${d.entrenadorId}-${d.fecha}`} role="alert" className="mt-1 max-w-[16rem] text-xs text-red-600">{error}</p>}
                          </td>
                          <td className="border-t border-slate-200 px-3 py-2 text-right tabular-nums text-slate-600">{d.valorHora === null ? '—' : formatoDeImporte(d.valorHora)}</td>
                          <td className="border-t border-slate-200 px-3 py-2">
                            <input type="text" autoComplete="off" maxLength={200} value={escrito.nota} disabled={bloqueado}
                              onChange={(ev) => escribir(d, 'nota', ev.target.value)} aria-label={`Nota del ${aDDMM(d.fecha)} de ${e.nombre}`}
                              className={`${claseDelCampo(false)} w-full min-w-[10rem]`} />
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </section>
          );
        })
      )}
    </div>
  );
}

/** El fondo de un renglón: ámbar si tiene cambios sin guardar, gris si es un día que todavía no pasó. */
function fondoDelRenglon(modificado: boolean, futuro: boolean): string {
  if (modificado) return 'bg-amber-50';
  return futuro ? 'bg-slate-50/60 text-slate-400' : '';
}
