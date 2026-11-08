import { useEffect, useState } from 'react';
import { Database, Download, Loader2, Save } from 'lucide-react';
import toast from 'react-hot-toast';
import { descargarCopiaAutomatica, guardarArchivo, guardarProgramacion, hacerCopiaAhora, listarCopiasAutomaticas, obtenerProgramacion } from '../../api/copiasApi';
import {
  DIAS_DE_LA_SEMANA, FRECUENCIAS, horaComoTexto, parsearHora, programacionesIguales, resumenDeProgramacion, tamanoLegible, textoDeFecha,
  validarProgramacion, type CopiaGuardada, type EstadoDeCopias, type ErroresDeProgramacion, type Frecuencia, type Programacion,
} from './copiasAutomaticasUtil';

type Campo = keyof ErroresDeProgramacion;

const campo = 'w-full rounded-lg border px-3 py-2 text-sm focus:outline-none focus:ring-2 disabled:opacity-60';
const claseDelCampo = (error?: string) =>
  `${campo} ${error ? 'border-red-400 focus:border-red-400 focus:ring-red-400' : 'border-slate-300 focus:border-marca-500 focus:ring-marca-500'}`;
const boton = 'inline-flex items-center gap-2 rounded-lg px-4 py-2 text-xs font-semibold shadow-sm transition-all active:scale-95 disabled:cursor-not-allowed disabled:opacity-60';

const programacionDe = (e: EstadoDeCopias): Programacion => ({
  activa: e.activa, frecuencia: e.frecuencia, hora: e.hora, minuto: e.minuto, diaDeLaSemana: e.diaDeLaSemana, diaDelMes: e.diaDelMes, conservar: e.conservar,
});

// El servidor nombra el campo en su mensaje: el error va junto a ese campo, no como aviso flotante.
function campoDelError(mensaje: string): Campo | null {
  if (/minuto/i.test(mensaje)) return 'minuto';
  if (/hora/i.test(mensaje)) return 'hora';
  if (/conservar/i.test(mensaje)) return 'conservar';
  if (/d[ií]a de la semana/i.test(mensaje)) return 'diaDeLaSemana';
  if (/d[ií]a del mes/i.test(mensaje)) return 'diaDelMes';
  return null;
}

/**
 * Bloque «Copia automática» de Configuración › Copia de seguridad (solo Administrador): se programa cada cuánto se hace una copia,
 * se ve cuándo es la próxima y cómo le fue a la última, se puede hacer una en el momento y se descargan las guardadas.
 */
export function CopiasAutomaticas() {
  const [estado, setEstado] = useState<EstadoDeCopias | null>(null);
  const [formulario, setFormulario] = useState<Programacion | null>(null);
  const [copias, setCopias] = useState<CopiaGuardada[]>([]);
  const [cargando, setCargando] = useState(true);
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null);
  const [errores, setErrores] = useState<ErroresDeProgramacion>({});
  const [guardando, setGuardando] = useState(false);
  const [haciendo, setHaciendo] = useState(false);
  const [errorDeLaCopia, setErrorDeLaCopia] = useState<string | null>(null);
  const [descargando, setDescargando] = useState<string | null>(null);

  // Se pide la programación y la lista al abrir el bloque, y de nuevo cada vez que se aprieta «Reintentar» (sube `intento`).
  const [intento, setIntento] = useState(0);
  useEffect(() => {
    let cancelado = false;
    Promise.all([obtenerProgramacion(), listarCopiasAutomaticas()])
      .then(([programacion, lista]) => {
        if (cancelado) return;
        setEstado(programacion);
        setFormulario(programacionDe(programacion));
        setCopias(lista);
      })
      .catch((error: unknown) => {
        if (!cancelado) setErrorDeCarga(error instanceof Error ? error.message : 'No se pudo cargar la programación de las copias.');
      })
      .finally(() => {
        if (!cancelado) setCargando(false);
      });
    return () => { cancelado = true; };
  }, [intento]);

  const reintentar = () => {
    setCargando(true);
    setErrorDeCarga(null);
    setIntento((n) => n + 1);
  };

  if (cargando) {
    return <div role="status" className="rounded-xl border border-slate-200 bg-slate-50/50 p-5 text-xs text-slate-500">Cargando la copia automática…</div>;
  }
  if (errorDeCarga || !estado || !formulario) {
    return (
      <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-5 text-xs text-red-800">
        <p>{errorDeCarga ?? 'No se pudo cargar la programación de las copias.'}</p>
        <button type="button" onClick={reintentar} className={`${boton} mt-3 border border-slate-300 bg-white text-slate-700 hover:bg-slate-50`}>Reintentar</button>
      </div>
    );
  }

  const sinCambios = programacionesIguales(formulario, programacionDe(estado));
  const cambiar = (cambios: Partial<Programacion>, quitaError?: Campo[]) => {
    setFormulario({ ...formulario, ...cambios });
    if (quitaError?.some((c) => errores[c])) setErrores((previos) => ({ ...previos, ...Object.fromEntries(quitaError.map((c) => [c, undefined])) }));
  };

  const cambiarFrecuencia = (frecuencia: Frecuencia) =>
    cambiar({
      frecuencia,
      // Al pasar a semanal o mensual se propone un día para que el formulario no quede incompleto.
      diaDeLaSemana: frecuencia === 'Semanal' ? formulario.diaDeLaSemana ?? 1 : formulario.diaDeLaSemana,
      diaDelMes: frecuencia === 'Mensual' ? formulario.diaDelMes ?? 1 : formulario.diaDelMes,
    }, ['diaDeLaSemana', 'diaDelMes']);

  const enfocarPrimero = (conError: ErroresDeProgramacion) => {
    const orden: [Campo, string][] = [['hora', 'copia-hora'], ['minuto', 'copia-hora'], ['diaDeLaSemana', 'copia-dia-semana'], ['diaDelMes', 'copia-dia-mes'], ['conservar', 'copia-conservar']];
    const primero = orden.find(([c]) => conError[c]);
    if (primero) document.getElementById(primero[1])?.focus();
  };

  const guardar = async () => {
    const invalidos = validarProgramacion(formulario);
    setErrores(invalidos);
    if (Object.keys(invalidos).length > 0) { enfocarPrimero(invalidos); return; }

    setGuardando(true);
    try {
      const nuevo = await guardarProgramacion(formulario);
      setEstado(nuevo);
      setFormulario(programacionDe(nuevo));
      toast.success(nuevo.activa ? 'Programación guardada: la copia automática está activada.' : 'Programación guardada: la copia automática está desactivada.');
    } catch (error) {
      const mensaje = error instanceof Error ? error.message : 'No se pudo guardar la programación.';
      const deCampo = campoDelError(mensaje);
      if (deCampo) { setErrores({ [deCampo]: mensaje }); enfocarPrimero({ [deCampo]: mensaje }); } else toast.error(mensaje);
    } finally {
      setGuardando(false);
    }
  };

  const copiarAhora = async () => {
    setHaciendo(true);
    setErrorDeLaCopia(null);
    try {
      const copia = await hacerCopiaAhora();
      toast.success(`Copia guardada: ${copia.nombre}`);
      setEstado(await obtenerProgramacion());
      setCopias(await listarCopiasAutomaticas());
    } catch (error) {
      setErrorDeLaCopia(error instanceof Error ? error.message : 'No se pudo hacer la copia.');
    } finally {
      setHaciendo(false);
    }
  };

  const descargar = async (nombre: string) => {
    setDescargando(nombre);
    try {
      guardarArchivo(await descargarCopiaAutomatica(nombre), nombre);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'No se pudo descargar la copia.');
    } finally {
      setDescargando(null);
    }
  };

  const semanal = formulario.frecuencia === 'Semanal';
  const mensual = formulario.frecuencia === 'Mensual';

  return (
    <section aria-labelledby="copia-automatica-titulo" className="space-y-4">
      <div>
        <h4 id="copia-automatica-titulo" className="pt-2 text-sm font-bold text-slate-900">Copia automática</h4>
        <p className="mt-1 text-xs text-slate-500">
          El sistema hace solo la copia completa (la misma, firmada, que se descarga acá arriba) y la guarda en la carpeta <strong>backups\automaticas</strong> del
          proyecto. Docker tiene que estar abierto a esa hora: si estaba apagado, hace una copia apenas arranca.
        </p>
      </div>

      {/* No es un formulario HTML: Configuración ya está dentro de uno. Enter en un campo guarda la programación, y no la configuración de la empresa. */}
      <div role="group" aria-label="Programación de la copia automática" className="space-y-4 rounded-xl border border-slate-200 bg-slate-50/50 p-5"
        onKeyDown={(e) => {
          if (e.key === 'Enter' && e.target instanceof HTMLInputElement) {
            e.preventDefault();
            void guardar();
          }
        }}>
        <div className="flex items-center gap-3">
          <button type="button" role="switch" aria-checked={formulario.activa} aria-labelledby="copia-activa-texto" disabled={guardando}
            onClick={() => cambiar({ activa: !formulario.activa })}
            className={`relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-marca-500 focus:ring-offset-2 disabled:opacity-60 ${formulario.activa ? 'bg-marca-600' : 'bg-slate-300'}`}>
            <span aria-hidden="true" className={`inline-block h-5 w-5 rounded-full bg-white shadow transition-transform ${formulario.activa ? 'translate-x-5' : 'translate-x-0.5'}`} />
          </button>
          <span id="copia-activa-texto" className="text-sm font-semibold text-slate-800">
            Copia automática {formulario.activa ? 'activada' : 'desactivada'}
          </span>
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <label htmlFor="copia-frecuencia" className="mb-1 block text-xs font-semibold text-slate-600">Cada cuánto</label>
            <select id="copia-frecuencia" value={formulario.frecuencia} disabled={guardando} onChange={(e) => cambiarFrecuencia(e.target.value as Frecuencia)} className={claseDelCampo()}>
              {FRECUENCIAS.map((f) => <option key={f.valor} value={f.valor}>{f.texto}</option>)}
            </select>
          </div>

          <div>
            <label htmlFor="copia-hora" className="mb-1 block text-xs font-semibold text-slate-600">
              {formulario.frecuencia === 'CadaSeisHoras' || formulario.frecuencia === 'CadaDoceHoras' ? 'Desde las (hora de Argentina)' : 'Hora (de Argentina)'}
            </label>
            <input id="copia-hora" type="time" value={horaComoTexto(formulario.hora, formulario.minuto)} disabled={guardando}
              onChange={(e) => { const h = parsearHora(e.target.value); if (h) cambiar(h, ['hora', 'minuto']); }}
              aria-invalid={Boolean(errores.hora || errores.minuto)} aria-describedby={errores.hora || errores.minuto ? 'error-copia-hora' : undefined}
              className={claseDelCampo(errores.hora || errores.minuto)} />
            {(errores.hora || errores.minuto) && <p id="error-copia-hora" role="alert" className="mt-1 text-xs text-red-600">{errores.hora ?? errores.minuto}</p>}
          </div>

          {semanal && (
            <div>
              <label htmlFor="copia-dia-semana" className="mb-1 block text-xs font-semibold text-slate-600">Día de la semana</label>
              <select id="copia-dia-semana" value={formulario.diaDeLaSemana ?? ''} disabled={guardando}
                onChange={(e) => cambiar({ diaDeLaSemana: e.target.value === '' ? null : Number(e.target.value) }, ['diaDeLaSemana'])}
                aria-invalid={Boolean(errores.diaDeLaSemana)} aria-describedby={errores.diaDeLaSemana ? 'error-copia-dia-semana' : undefined}
                className={claseDelCampo(errores.diaDeLaSemana)}>
                <option value="">Elegí un día</option>
                {DIAS_DE_LA_SEMANA.map((d) => <option key={d.numero} value={d.numero}>{d.nombre}</option>)}
              </select>
              {errores.diaDeLaSemana && <p id="error-copia-dia-semana" role="alert" className="mt-1 text-xs text-red-600">{errores.diaDeLaSemana}</p>}
            </div>
          )}

          {mensual && (
            <div>
              <label htmlFor="copia-dia-mes" className="mb-1 block text-xs font-semibold text-slate-600">Día del mes (1 a 28)</label>
              <input id="copia-dia-mes" type="number" min={1} max={28} inputMode="numeric" value={formulario.diaDelMes ?? ''} disabled={guardando}
                onChange={(e) => cambiar({ diaDelMes: e.target.value === '' ? null : Number(e.target.value) }, ['diaDelMes'])}
                aria-invalid={Boolean(errores.diaDelMes)} aria-describedby={errores.diaDelMes ? 'error-copia-dia-mes' : undefined}
                className={claseDelCampo(errores.diaDelMes)} />
              {errores.diaDelMes && <p id="error-copia-dia-mes" role="alert" className="mt-1 text-xs text-red-600">{errores.diaDelMes}</p>}
            </div>
          )}

          <div>
            <label htmlFor="copia-conservar" className="mb-1 block text-xs font-semibold text-slate-600">Copias a conservar (1 a 60)</label>
            <input id="copia-conservar" type="number" min={1} max={60} inputMode="numeric" value={Number.isNaN(formulario.conservar) ? '' : formulario.conservar} disabled={guardando}
              onChange={(e) => cambiar({ conservar: e.target.value === '' ? Number.NaN : Number(e.target.value) }, ['conservar'])}
              aria-invalid={Boolean(errores.conservar)} aria-describedby={errores.conservar ? 'error-copia-conservar' : undefined}
              className={claseDelCampo(errores.conservar)} />
            {errores.conservar && <p id="error-copia-conservar" role="alert" className="mt-1 text-xs text-red-600">{errores.conservar}</p>}
          </div>
        </div>

        <p className="text-xs text-slate-600" aria-live="polite">
          {formulario.activa ? resumenDeProgramacion(formulario) : 'La copia automática está desactivada: no se hace ninguna copia sola.'}
          {' '}Al hacer una nueva se borran las más viejas, y quedan las últimas {Number.isNaN(formulario.conservar) ? '…' : formulario.conservar}.
        </p>

        <div className="flex flex-wrap items-center gap-3">
          <button type="button" onClick={() => void guardar()} disabled={guardando || sinCambios} className={`${boton} bg-marca-600 text-white hover:bg-marca-700`}>
            {guardando ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Save className="h-4 w-4" aria-hidden="true" />}
            {guardando ? 'Guardando…' : 'Guardar programación'}
          </button>
          <button type="button" onClick={() => void copiarAhora()} disabled={haciendo}
            className={`${boton} border border-slate-300 bg-white text-slate-700 hover:bg-slate-50`}>
            {haciendo ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Database className="h-4 w-4" aria-hidden="true" />}
            {haciendo ? 'Haciendo la copia…' : 'Hacer una copia ahora'}
          </button>
          {!sinCambios && <span className="text-xs font-medium text-amber-700">Hay cambios sin guardar.</span>}
        </div>
      </div>

      {errorDeLaCopia && <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-xs text-red-800">{errorDeLaCopia}</p>}

      <dl className="grid grid-cols-1 gap-3 text-xs sm:grid-cols-2">
        <div className="rounded-lg border border-slate-200 bg-white px-4 py-3">
          <dt className="font-semibold text-slate-600">Próxima copia</dt>
          <dd className="mt-0.5 text-sm text-slate-900">{estado.activa ? textoDeFecha(estado.proximaCopia) : 'No hay: la copia automática está desactivada'}</dd>
        </div>
        <div className="rounded-lg border border-slate-200 bg-white px-4 py-3">
          <dt className="font-semibold text-slate-600">Última copia</dt>
          <dd className="mt-0.5 text-sm text-slate-900">{estado.ultimaCopia ? textoDeFecha(estado.ultimaCopia) : 'Todavía no se hizo ninguna'}</dd>
        </div>
      </dl>

      {estado.ultimoError && (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-xs text-red-800">
          <strong>La última copia automática falló</strong> ({textoDeFecha(estado.ultimoIntento)}): {estado.ultimoError} Se reintenta a los 30 minutos.
        </p>
      )}

      {copias.length === 0 ? (
        <p className="rounded-lg border border-dashed border-slate-300 bg-white px-4 py-3 text-xs text-slate-500">Todavía no hay copias automáticas guardadas.</p>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
          <table className="w-full min-w-[30rem] border-collapse text-xs">
            <caption className="sr-only">Copias automáticas guardadas</caption>
            <thead>
              <tr className="bg-slate-50 text-left text-slate-700">
                <th scope="col" className="border-b border-slate-200 px-3 py-2 font-semibold">Copia</th>
                <th scope="col" className="border-b border-slate-200 px-3 py-2 font-semibold">Fecha</th>
                <th scope="col" className="border-b border-slate-200 px-3 py-2 text-right font-semibold">Tamaño</th>
                <th scope="col" className="border-b border-slate-200 px-3 py-2"><span className="sr-only">Descargar</span></th>
              </tr>
            </thead>
            <tbody>
              {copias.map((c) => (
                <tr key={c.nombre}>
                  <th scope="row" className="border-t border-slate-200 px-3 py-2 text-left font-mono font-normal text-slate-800">{c.nombre}</th>
                  <td className="border-t border-slate-200 px-3 py-2">{textoDeFecha(c.fecha)}</td>
                  <td className="border-t border-slate-200 px-3 py-2 text-right tabular-nums">{tamanoLegible(c.bytes)}</td>
                  <td className="border-t border-slate-200 px-3 py-2 text-right">
                    <button type="button" onClick={() => void descargar(c.nombre)} disabled={descargando === c.nombre} aria-label={`Descargar ${c.nombre}`}
                      className="inline-flex items-center gap-1 rounded-md border border-slate-300 bg-white px-2.5 py-1.5 font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">
                      <Download className="h-3.5 w-3.5" aria-hidden="true" /> Descargar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
