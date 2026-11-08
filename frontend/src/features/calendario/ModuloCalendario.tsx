import { useEffect, useMemo, useState } from 'react';
import { CalendarPlus, Loader2, Pencil, Printer, Save, Trash2, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { useAuth } from '../../context/AuthContext';
import { guardarCalendario, obtenerCalendario } from '../../api/calendarioApi';
import { obtenerEntrenadores } from '../../api/entrenadoresApi';
import { generarCalendarioPDF } from '../../utils/generarCalendarioPDF';
import { DialogoAsignarTurno } from './DialogoAsignarTurno';
import {
  DIAS, HORAS, LARGO_MAXIMO_DEL_NOMBRE, aplicarRango, buscarEntrenador, canonizarGrilla, celdasSinEntrenador, clave, encargadosDistintos,
  filasParaMostrar, grillaDesdeTurnos, grillasIguales, horasPorEncargado, limpiarNombre, nombreDelDia, ponerEncargado, textoDelTurno,
  turnosDesdeGrilla, turnosParaGuardar, type Entrenador, type Grilla, type RangoDeTurno,
} from './calendarioUtil';

// Un color suave por persona para leer el calendario de un vistazo. En el PDF no hay colores: sale en blanco y negro.
const COLORES = [
  'border-sky-200 bg-sky-50 text-sky-900',
  'border-orange-200 bg-orange-50 text-orange-900',
  'border-pink-200 bg-pink-50 text-pink-900',
  'border-emerald-200 bg-emerald-50 text-emerald-900',
  'border-yellow-200 bg-yellow-50 text-yellow-900',
  'border-cyan-200 bg-cyan-50 text-cyan-900',
  'border-lime-200 bg-lime-50 text-lime-900',
  'border-teal-200 bg-teal-50 text-teal-900',
];

const boton = 'inline-flex items-center gap-1.5 rounded-md px-3 py-2 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50';
const botonPrimario = `${boton} bg-marca-600 text-white hover:bg-marca-700`;
const botonSecundario = `${boton} border border-slate-300 bg-white text-slate-700 hover:bg-slate-50`;

interface Props {
  /** Lleva a la pantalla Entrenadores (los entrenadores se cargan ahí, no en el calendario). */
  onIrAEntrenadores: () => void;
}

export function ModuloCalendario({ onIrAEntrenadores }: Props) {
  const { user } = useAuth();
  // La API es la que decide: acá solo se oculta lo que Recepción no puede hacer.
  const puedeEditar = user?.rol === 'Administrador' || user?.rol === 'Contador';

  const [guardada, setGuardada] = useState<Grilla>({});
  const [borrador, setBorrador] = useState<Grilla>({});
  const [entrenadores, setEntrenadores] = useState<Entrenador[]>([]);
  const [editando, setEditando] = useState(false);
  const [cargando, setCargando] = useState(true);
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const [errorDeGuardado, setErrorDeGuardado] = useState<string | null>(null);
  const [asignando, setAsignando] = useState(false);
  const [confirmandoDescarte, setConfirmandoDescarte] = useState(false);

  const cargar = async () => {
    setCargando(true);
    setErrorDeCarga(null);
    try {
      const [turnos, lista] = await Promise.all([obtenerCalendario(), obtenerEntrenadores()]);
      const grilla = grillaDesdeTurnos(turnos);
      setEntrenadores(lista);
      setGuardada(grilla);
      setBorrador(grilla);
    } catch (error) {
      setErrorDeCarga(error instanceof Error ? error.message : 'No se pudo cargar el calendario.');
    } finally {
      setCargando(false);
    }
  };

  useEffect(() => {
    void cargar();
  }, []);

  const hayCambios = editando && !grillasIguales(guardada, borrador);
  const visible = editando ? borrador : guardada;
  const filas = useMemo(() => filasParaMostrar(guardada), [guardada]);
  const encargados = useMemo(() => encargadosDistintos(editando ? borrador : guardada), [editando, borrador, guardada]);
  const horas = useMemo(() => horasPorEncargado(guardada), [guardada]);
  const activos = useMemo(() => entrenadores.filter((e) => e.activo), [entrenadores]);
  // Los nombres escritos en el borrador que no son de un entrenador activo: no se pueden guardar.
  const sinFicha = useMemo(() => (editando ? celdasSinEntrenador(borrador, entrenadores) : []), [editando, borrador, entrenadores]);
  const colorDe = (nombre: string) => COLORES[Math.max(0, encargados.indexOf(nombre)) % COLORES.length];
  const vacio = turnosDesdeGrilla(guardada).length === 0;

  // Avisa si se intenta cerrar o recargar la página con cambios sin guardar.
  useEffect(() => {
    if (!hayCambios) return;
    const avisar = (e: BeforeUnloadEvent) => { e.preventDefault(); };
    window.addEventListener('beforeunload', avisar);
    return () => window.removeEventListener('beforeunload', avisar);
  }, [hayCambios]);

  const empezarAEditar = () => {
    setBorrador(guardada);
    setErrorDeGuardado(null);
    setConfirmandoDescarte(false);
    setEditando(true);
  };

  const cancelar = () => {
    if (hayCambios && !confirmandoDescarte) {
      setConfirmandoDescarte(true);
      return;
    }
    setBorrador(guardada);
    setEditando(false);
    setConfirmandoDescarte(false);
    setErrorDeGuardado(null);
  };

  const guardar = async () => {
    setGuardando(true);
    setErrorDeGuardado(null);
    try {
      const grilla = grillaDesdeTurnos(await guardarCalendario(turnosParaGuardar(borrador, entrenadores)));
      setGuardada(grilla);
      setBorrador(grilla);
      setEditando(false);
      setConfirmandoDescarte(false);
      toast.success('Calendario guardado.');
    } catch (error) {
      setErrorDeGuardado(error instanceof Error ? error.message : 'No se pudo guardar el calendario.');
    } finally {
      setGuardando(false);
    }
  };

  const aplicar = (rango: RangoDeTurno) => {
    setBorrador((actual) => aplicarRango(actual, rango));
    setAsignando(false);
  };

  // Enter y las flechas pasan a la celda de arriba o de abajo, como en una planilla.
  const moverseEntreCeldas = (e: React.KeyboardEvent<HTMLInputElement>, dia: number, hora: number) => {
    const paso = e.key === 'Enter' || e.key === 'ArrowDown' ? 1 : e.key === 'ArrowUp' ? -1 : 0;
    if (!paso) return;
    e.preventDefault();
    document.getElementById(`celda-${dia}-${hora + paso}`)?.focus();
  };

  if (cargando) {
    return <div role="status" className="rounded-xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">Cargando el calendario...</div>;
  }

  if (errorDeCarga) {
    return (
      <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-6 text-sm text-red-800">
        <p>{errorDeCarga}</p>
        <button type="button" onClick={() => void cargar()} className={`${botonSecundario} mt-3`}>Reintentar</button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <p className="max-w-xl text-sm text-slate-500">
          Qué entrenador está a cargo del gimnasio, de lunes a viernes y de 06:00 a 22:00. Es una semana tipo: se repite todas las semanas.
        </p>
        <div className="flex flex-wrap items-center gap-2">
          {!editando && puedeEditar && (
            <button type="button" onClick={empezarAEditar} className={botonPrimario}>
              <Pencil className="h-4 w-4" aria-hidden="true" /> Editar calendario
            </button>
          )}
          {editando && (
            <>
              <button type="button" onClick={() => setAsignando(true)} className={botonSecundario}>
                <CalendarPlus className="h-4 w-4" aria-hidden="true" /> Asignar turno
              </button>
              <button type="button" onClick={() => setBorrador({})} disabled={Object.keys(borrador).length === 0} className={botonSecundario}>
                <Trash2 className="h-4 w-4" aria-hidden="true" /> Vaciar todo
              </button>
              <button type="button" onClick={cancelar} disabled={guardando} className={botonSecundario}>Cancelar</button>
              <button type="button" onClick={() => void guardar()} disabled={guardando || !hayCambios || sinFicha.length > 0} className={botonPrimario}>
                {guardando ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Save className="h-4 w-4" aria-hidden="true" />}
                {guardando ? 'Guardando...' : 'Guardar cambios'}
              </button>
            </>
          )}
          <button type="button" onClick={() => generarCalendarioPDF(guardada)} disabled={hayCambios}
            title={hayCambios ? 'Guardá los cambios antes de imprimir: el PDF sale con lo guardado.' : 'Abre el calendario en PDF para imprimir o fotocopiar'}
            className={botonSecundario}>
            <Printer className="h-4 w-4" aria-hidden="true" /> Imprimir (PDF)
          </button>
        </div>
      </div>

      {activos.length === 0 && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          <span>
            Todavía no hay entrenadores cargados, y el calendario solo acepta entrenadores.
            {puedeEditar ? ' Cargalos primero en Entrenadores.' : ' El Administrador o el Contador los cargan.'}
          </span>
          <button type="button" onClick={onIrAEntrenadores} className={botonSecundario}>
            <Users className="h-4 w-4" aria-hidden="true" /> Ir a Entrenadores
          </button>
        </div>
      )}

      {editando && (
        <p className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-2.5 text-sm text-slate-600">
          Escribí el nombre del entrenador en cada hora (aparece la lista de los cargados), o usá <strong>Asignar turno</strong> para
          cargar a un entrenador en varias horas y días de una vez. Con Enter o las flechas pasás a la hora de abajo o de arriba. Los
          cambios se aplican al apretar <strong>Guardar cambios</strong>.
          {hayCambios && <strong className="ml-1 text-amber-700">Tenés cambios sin guardar.</strong>}
        </p>
      )}

      {sinFicha.length > 0 && (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
          Estos nombres no son entrenadores cargados: <strong>{sinFicha.join(', ')}</strong>. Elegilos de la lista o cargalos en
          Entrenadores; mientras tanto no se puede guardar.
        </p>
      )}

      {confirmandoDescarte && (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          <span>Hay cambios sin guardar. ¿Los descartás?</span>
          <span className="flex gap-2">
            <button type="button" onClick={() => setConfirmandoDescarte(false)} className={botonSecundario}>Seguir editando</button>
            <button type="button" onClick={cancelar} className={`${boton} bg-red-600 text-white hover:bg-red-700`}>Sí, descartar</button>
          </span>
        </div>
      )}

      {errorDeGuardado && (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">{errorDeGuardado}</p>
      )}

      {!editando && vacio && activos.length > 0 && (
        <p className="rounded-lg border border-dashed border-slate-300 bg-white px-4 py-3 text-sm text-slate-500">
          Todavía no hay entrenadores a cargo en el calendario.{puedeEditar ? ' Apretá «Editar calendario» para cargarlos.' : ' El Administrador o el Contador los cargan.'}
        </p>
      )}

      {editando && (
        <datalist id="entrenadores-activos">
          {activos.map((e) => <option key={e.id} value={e.nombre} />)}
        </datalist>
      )}

      <div className="relative overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table className="w-full min-w-[42rem] table-fixed border-collapse text-sm">
          <caption className="sr-only">Calendario semanal de entrenadores a cargo, de lunes a viernes y de 06:00 a 22:00</caption>
          <thead>
            <tr className="bg-slate-50 text-slate-700">
              <th scope="col" className="w-32 border-b border-slate-200 px-3 py-2.5 text-left font-semibold">Hora</th>
              {DIAS.map((d) => (
                <th key={d.numero} scope="col" className="border-b border-l border-slate-200 px-3 py-2.5 text-center font-semibold">{d.nombre}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {editando
              ? HORAS.map((hora) => (
                  <tr key={hora}>
                    <th scope="row" className="whitespace-nowrap border-t border-slate-200 bg-slate-50 px-3 py-1.5 text-left text-xs font-medium tabular-nums text-slate-600">{textoDelTurno(hora)}</th>
                    {DIAS.map((d) => {
                      const valor = visible[clave(d.numero, hora)] ?? '';
                      const ajeno = valor.trim() !== '' && !buscarEntrenador(entrenadores, valor);
                      return (
                        <td key={d.numero} className="border-l border-t border-slate-200 p-0.5">
                          <input
                            id={`celda-${d.numero}-${hora}`}
                            value={valor}
                            list="entrenadores-activos"
                            maxLength={LARGO_MAXIMO_DEL_NOMBRE}
                            autoComplete="off"
                            aria-label={`${nombreDelDia(d.numero)}, ${textoDelTurno(hora)}`}
                            aria-invalid={ajeno}
                            title={ajeno ? 'Este nombre no es de un entrenador cargado' : undefined}
                            onChange={(e) => setBorrador((actual) => ponerEncargado(actual, d.numero, hora, e.target.value))}
                            // Al salir de la celda, el nombre pasa a escribirse como en la ficha del entrenador.
                            onBlur={() => setBorrador((actual) => canonizarGrilla(actual, entrenadores))}
                            onKeyDown={(e) => moverseEntreCeldas(e, d.numero, hora)}
                            className={`w-full rounded px-2 py-1.5 text-center text-sm outline-none transition-colors focus:ring-2 focus:ring-marca-500 ${
                              ajeno ? 'border border-red-400 bg-red-50 font-medium text-red-800'
                                : valor ? `border ${colorDe(limpiarNombre(valor))} font-medium` : 'border border-transparent hover:bg-slate-50'}`}
                          />
                        </td>
                      );
                    })}
                  </tr>
                ))
              : filas.map(({ hora, celdas }) => (
                  <tr key={hora}>
                    <th scope="row" className="whitespace-nowrap border-t border-slate-200 bg-slate-50 px-3 py-2 text-left text-xs font-medium tabular-nums text-slate-600">{textoDelTurno(hora)}</th>
                    {celdas.map((celda, i) =>
                      celda === null ? null : (
                        <td key={DIAS[i].numero} rowSpan={celda.filas}
                          className={`border-l border-t border-slate-200 px-2 py-2 text-center align-middle ${celda.texto ? `font-medium ${colorDe(celda.texto)}` : ''}`}>
                          {celda.texto || <span className="sr-only">Libre</span>}
                        </td>
                      ),
                    )}
                  </tr>
                ))}
          </tbody>
        </table>
      </div>

      {!editando && horas.length > 0 && (
        <section aria-label="Horas semanales por entrenador" className="rounded-xl border border-slate-200 bg-white p-4">
          <h2 className="mb-2 text-sm font-semibold text-slate-800">Horas por semana de cada entrenador</h2>
          <ul className="flex flex-wrap gap-2">
            {horas.map((h) => (
              <li key={h.encargado} className={`rounded-full border px-3 py-1 text-xs font-medium ${colorDe(h.encargado)}`}>
                {h.encargado}: {h.horas} {h.horas === 1 ? 'hora' : 'horas'}
              </li>
            ))}
          </ul>
        </section>
      )}

      {asignando && <DialogoAsignarTurno grilla={borrador} entrenadores={activos} onAplicar={aplicar} onCerrar={() => setAsignando(false)} />}
    </div>
  );
}
