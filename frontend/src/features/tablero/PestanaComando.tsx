import { useCallback, useEffect, useState } from 'react';
import { Check, Loader2, Pencil, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { guardarMeta, obtenerIndicadores } from '../../api/tableroApi';
import { nombreDelMes } from '../../api/dashboardApi';
import { numeroDecimal } from '../../utils/entradaNumerica';
import { PERSPECTIVAS, TEXTO_DEL_SEMAFORO, textoDeValor, textoDeVariacion, type Indicador, type IndicadoresDelMes, type Semaforo } from './tableroUtil';

const COLOR: Record<Semaforo, string> = {
  verde: 'bg-emerald-500',
  amarillo: 'bg-amber-400',
  rojo: 'bg-red-500',
  'sin-meta': 'bg-slate-300',
  'sin-datos': 'bg-slate-200',
};

const QUE_MIDE: Record<string, string> = {
  Financiera: '¿El gimnasio gana plata?',
  Socios: '¿Los socios llegan y se quedan?',
  Procesos: '¿El día a día funciona?',
  Personas: '¿El equipo rinde lo que cuesta?',
};

interface Props {
  mes: string;
  onMes: (mes: string) => void;
  hoy: string;
  puedeEditarMetas: boolean;
}

/** Tablero de comando con las cuatro perspectivas del Balanced Scorecard y el ciclo de control (meta, valor, desvío). */
export function PestanaComando({ mes, onMes, hoy, puedeEditarMetas }: Props) {
  const [datos, setDatos] = useState<IndicadoresDelMes | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [intento, setIntento] = useState(0);
  const [listoPara, setListoPara] = useState<string | null>(null);
  const pedido = `${mes}|${hoy}|${intento}`;
  const cargando = listoPara !== pedido;
  const cargar = useCallback(() => setIntento((n) => n + 1), []);

  useEffect(() => {
    let cancelado = false;
    obtenerIndicadores(mes, hoy)
      .then((d) => { if (!cancelado) { setDatos(d); setError(null); } })
      .catch((e) => { if (!cancelado) setError(e instanceof Error ? e.message : 'No se pudieron calcular los indicadores.'); })
      .finally(() => { if (!cancelado) setListoPara(`${mes}|${hoy}|${intento}`); });
    return () => { cancelado = true; };
  }, [mes, hoy, intento]);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-4">
        <div>
          <label htmlFor="tablero-mes" className="mb-1 block text-xs font-semibold text-slate-600">Mes</label>
          <input id="tablero-mes" type="month" value={mes} max={hoy.slice(0, 7)} onChange={(e) => e.target.value && onMes(e.target.value)}
            className="rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-marca-500 focus:outline-none focus:ring-2 focus:ring-marca-500" />
        </div>
        {datos && (
          <p className="pb-2 text-xs text-slate-500">
            Comparado con {nombreDelMes(datos.mesAnterior)}. Medido hasta el {datos.cierre.split('-').reverse().join('/')}.
          </p>
        )}
        {cargando && <Loader2 className="mb-2 h-4 w-4 animate-spin text-slate-400" aria-label="Calculando" />}
      </div>

      <ol className="grid gap-2 rounded-xl border border-marca-100 bg-marca-50/60 p-3 text-xs text-slate-700 sm:grid-cols-4" aria-label="Ciclo de control">
        <li><b>1. Planificar:</b> el Administrador fija la meta de cada indicador.</li>
        <li><b>2. Medir:</b> el sistema calcula el valor real del mes con los datos cargados.</li>
        <li><b>3. Comparar:</b> el semáforo muestra el desvío contra la meta y la flecha, contra el mes anterior.</li>
        <li><b>4. Corregir:</b> en Decisiones se ve a quién contactar y qué pasaría si cambian los precios.</li>
      </ol>

      {error && (
        <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {error} <button type="button" onClick={cargar} className="ml-2 font-medium underline">Reintentar</button>
        </div>
      )}

      {datos && (
        <div className="grid gap-4 lg:grid-cols-2">
          {PERSPECTIVAS.map((p) => (
            <section key={p} aria-labelledby={`perspectiva-${p}`} className="rounded-xl border border-slate-200 bg-white p-4">
              <h2 id={`perspectiva-${p}`} className="text-sm font-bold text-slate-900">Perspectiva {p}</h2>
              <p className="mb-3 text-xs text-slate-500">{QUE_MIDE[p]}</p>
              <ul className="divide-y divide-slate-100">
                {datos.indicadores.filter((i) => i.perspectiva === p).map((i) => (
                  <FilaDeIndicador key={i.clave} indicador={i} puedeEditar={puedeEditarMetas} onGuardado={cargar} />
                ))}
              </ul>
            </section>
          ))}
        </div>
      )}
    </div>
  );
}

function FilaDeIndicador({ indicador: i, puedeEditar, onGuardado }: { indicador: Indicador; puedeEditar: boolean; onGuardado: () => void }) {
  const [editando, setEditando] = useState(false);
  const [texto, setTexto] = useState('');
  const [guardando, setGuardando] = useState(false);

  const empezar = () => {
    setTexto(i.meta === null ? '' : String(i.meta).replace('.', ','));
    setEditando(true);
  };

  const guardar = async () => {
    const limpio = texto.trim();
    const valor = limpio === '' ? null : Number(limpio.replace(',', '.'));
    if (valor !== null && (!Number.isFinite(valor) || valor < 0)) { toast.error('Escribí un número mayor o igual a 0.'); return; }
    setGuardando(true);
    try {
      await guardarMeta(i.clave, valor);
      toast.success(valor === null ? `Meta de «${i.nombre}» borrada.` : `Meta de «${i.nombre}» guardada.`);
      setEditando(false);
      onGuardado();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'No se pudo guardar la meta.');
    } finally {
      setGuardando(false);
    }
  };

  // Si al indicador le conviene bajar, una flecha hacia abajo es buena noticia.
  const mejoro = i.variacion === null || i.variacion === 0 ? null : (i.variacion > 0) === (i.sentido === 'mas');
  return (
    <li className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1 py-2.5">
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium text-slate-800">{i.nombre}</p>
        {i.detalle && <p className="text-xs text-slate-500">{i.detalle}</p>}
      </div>
      <div className="flex items-center gap-4">
        <div className="text-right">
          <p className="text-base font-bold tabular-nums text-slate-900">{textoDeValor(i.valor, i.esPorcentaje, i.clave)}</p>
          <p className={`text-xs tabular-nums ${mejoro === null ? 'text-slate-400' : mejoro ? 'text-emerald-700' : 'text-red-600'}`}
            title="Contra el mes anterior">
            {textoDeVariacion(i.variacion, i.esPorcentaje)}
          </p>
        </div>
        <div className="w-40 text-xs">
          <p className="flex items-center gap-1.5 font-medium text-slate-700">
            <span className={`inline-block h-3 w-3 rounded-full ${COLOR[i.semaforo]}`} aria-hidden="true" />
            {TEXTO_DEL_SEMAFORO[i.semaforo]}
          </p>
          {!i.admiteMeta ? (
            <p className="text-slate-400">Sin meta: es informativo</p>
          ) : editando ? (
            <form className="mt-1 flex items-center gap-1" onSubmit={(e) => { e.preventDefault(); void guardar(); }}>
              <input aria-label={`Meta de ${i.nombre}`} value={texto} autoFocus inputMode="decimal" disabled={guardando}
                onChange={(e) => setTexto(numeroDecimal(e.target.value, 2, 15, ','))} placeholder="vacío = sin meta"
                className="w-24 rounded border border-slate-300 px-1.5 py-0.5 text-xs focus:border-marca-500 focus:outline-none focus:ring-1 focus:ring-marca-500" />
              <button type="submit" disabled={guardando} aria-label="Guardar la meta" className="rounded p-0.5 text-emerald-700 hover:bg-emerald-50"><Check className="h-4 w-4" /></button>
              <button type="button" onClick={() => setEditando(false)} disabled={guardando} aria-label="Cancelar" className="rounded p-0.5 text-slate-500 hover:bg-slate-100"><X className="h-4 w-4" /></button>
            </form>
          ) : (
            <p className="flex items-center gap-1 text-slate-500">
              Meta: {i.meta === null ? '—' : textoDeValor(i.meta, i.esPorcentaje, i.clave)}
              {puedeEditar && (
                <button type="button" onClick={empezar} aria-label={`Cambiar la meta de ${i.nombre}`} title="Cambiar la meta"
                  className="rounded p-0.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700"><Pencil className="h-3.5 w-3.5" /></button>
              )}
            </p>
          )}
        </div>
      </div>
    </li>
  );
}
