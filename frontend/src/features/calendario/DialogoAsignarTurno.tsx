import { useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import {
  DIAS, HORAS, LARGO_MAXIMO_DEL_NOMBRE, PRIMERA_HORA, ULTIMA_HORA, celdasDelRango, limpiarNombre, problemaDelRango, textoDeHora, turnosQueSePisan,
  type Grilla, type RangoDeTurno,
} from './calendarioUtil';

interface Props {
  /** El calendario que se está editando, para avisar a cuántos turnos ya cargados pisaría. */
  grilla: Grilla;
  onAplicar: (rango: RangoDeTurno) => void;
  onCerrar: () => void;
}

const campo = 'w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-marca-500 focus:outline-none focus:ring-2 focus:ring-marca-500';
const botonSecundario = 'rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50';

/**
 * Carga a una persona en un rango de horas y de días de una sola vez (por ejemplo, Ana de lunes a viernes de 08:00 a 14:00),
 * en lugar de escribir su nombre celda por celda. Solo modifica el calendario en pantalla: se guarda con «Guardar cambios».
 */
export function DialogoAsignarTurno({ grilla, onAplicar, onCerrar }: Props) {
  const [encargado, setEncargado] = useState('');
  const [dias, setDias] = useState<number[]>(DIAS.map((d) => d.numero));
  const [desde, setDesde] = useState(PRIMERA_HORA);
  const [hasta, setHasta] = useState(PRIMERA_HORA + 8);
  const [intento, setIntento] = useState(false);
  const nombreRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    nombreRef.current?.focus();
    const tecla = (e: KeyboardEvent) => { if (e.key === 'Escape') onCerrar(); };
    document.addEventListener('keydown', tecla);
    return () => document.removeEventListener('keydown', tecla);
  }, [onCerrar]);

  const rango: RangoDeTurno = { encargado, dias, desde, hasta };
  const problema = problemaDelRango(rango);
  const celdas = celdasDelRango(rango).length;
  const pisa = problema ? 0 : turnosQueSePisan(grilla, rango);
  const horasPorDia = Math.max(0, hasta - desde);

  const alternarDia = (numero: number) =>
    setDias((actuales) => (actuales.includes(numero) ? actuales.filter((d) => d !== numero) : [...actuales, numero]));

  const aplicar = () => {
    if (problema) { setIntento(true); return; }
    onAplicar({ ...rango, encargado: limpiarNombre(encargado) });
  };

  const dejarLibres = () => {
    const sinNombre = { ...rango, encargado: '' };
    if (problemaDelRango(sinNombre, true)) { setIntento(true); return; }
    onAplicar(sinNombre);
  };

  const errorDeNombre = intento && !limpiarNombre(encargado) ? 'Escribí el nombre del encargado.' : null;
  const errorDeDias = intento && dias.length === 0 ? 'Elegí al menos un día.' : null;
  const errorDeHoras = intento && hasta <= desde ? 'La hora de fin tiene que ser posterior a la de inicio.' : null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
      onMouseDown={(e) => { if (e.target === e.currentTarget) onCerrar(); }}>
      <div role="dialog" aria-modal="true" aria-labelledby="asignar-turno-titulo"
        className="w-full max-w-md overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl">
        <div className="flex items-start justify-between gap-4 bg-slate-900 px-6 py-4 text-white">
          <div>
            <h2 id="asignar-turno-titulo" className="text-base font-bold">Asignar turno</h2>
            <p className="mt-0.5 text-xs text-slate-400">Cargá a una persona en varias horas y días de una vez.</p>
          </div>
          <button type="button" onClick={onCerrar} aria-label="Cerrar" className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-800 hover:text-white">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form className="space-y-4 p-6" noValidate onSubmit={(e) => { e.preventDefault(); aplicar(); }}>
          <div>
            <label htmlFor="turno-encargado" className="mb-1 block text-xs font-semibold text-slate-600">Encargado *</label>
            <input id="turno-encargado" ref={nombreRef} value={encargado} maxLength={LARGO_MAXIMO_DEL_NOMBRE} autoComplete="off"
              onChange={(e) => setEncargado(e.target.value)} placeholder="Ej: Ana López"
              aria-invalid={Boolean(errorDeNombre)} aria-describedby={errorDeNombre ? 'error-turno-encargado' : undefined}
              className={`${campo} ${errorDeNombre ? 'border-red-400 focus:border-red-400 focus:ring-red-400' : ''}`} />
            {errorDeNombre && <p id="error-turno-encargado" role="alert" className="mt-1 text-xs text-red-600">{errorDeNombre}</p>}
          </div>

          <fieldset>
            <legend className="mb-1 text-xs font-semibold text-slate-600">Días *</legend>
            <div className="flex flex-wrap gap-2">
              {DIAS.map((d) => {
                const elegido = dias.includes(d.numero);
                return (
                  <label key={d.numero}
                    className={`inline-flex cursor-pointer select-none items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm transition-colors ${
                      elegido ? 'border-marca-500 bg-marca-50 font-medium text-marca-800' : 'border-slate-300 bg-white text-slate-600 hover:bg-slate-50'}`}>
                    <input type="checkbox" checked={elegido} onChange={() => alternarDia(d.numero)} className="sr-only" />
                    {d.nombre}
                  </label>
                );
              })}
            </div>
            {errorDeDias && <p role="alert" className="mt-1 text-xs text-red-600">{errorDeDias}</p>}
          </fieldset>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor="turno-desde" className="mb-1 block text-xs font-semibold text-slate-600">Desde las</label>
              <select id="turno-desde" value={desde} onChange={(e) => setDesde(Number(e.target.value))} className={campo}>
                {HORAS.map((h) => <option key={h} value={h}>{textoDeHora(h)}</option>)}
              </select>
            </div>
            <div>
              <label htmlFor="turno-hasta" className="mb-1 block text-xs font-semibold text-slate-600">Hasta las</label>
              <select id="turno-hasta" value={hasta} onChange={(e) => setHasta(Number(e.target.value))}
                aria-invalid={Boolean(errorDeHoras)} aria-describedby={errorDeHoras ? 'error-turno-hasta' : undefined}
                className={`${campo} ${errorDeHoras ? 'border-red-400 focus:border-red-400 focus:ring-red-400' : ''}`}>
                {HORAS.map((h) => <option key={h + 1} value={h + 1}>{textoDeHora(h + 1)}</option>)}
              </select>
            </div>
            {errorDeHoras && <p id="error-turno-hasta" role="alert" className="col-span-2 -mt-2 text-xs text-red-600">{errorDeHoras}</p>}
          </div>

          <p className="rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-600" aria-live="polite">
            {celdas > 0
              ? <>Se van a completar <strong>{celdas}</strong> turnos de una hora ({dias.length} {dias.length === 1 ? 'día' : 'días'} × {horasPorDia} {horasPorDia === 1 ? 'hora' : 'horas'}, hasta las {textoDeHora(Math.min(hasta, ULTIMA_HORA + 1))}).
                {pisa > 0 && <> <strong className="text-amber-700">Reemplaza {pisa} {pisa === 1 ? 'turno ya cargado' : 'turnos ya cargados'}</strong> de otra persona.</>}</>
              : 'Elegí los días y el rango de horas.'}
          </p>

          <div className="flex flex-wrap items-center justify-between gap-2 pt-1">
            <button type="button" onClick={dejarLibres} className="text-sm font-medium text-slate-600 underline-offset-2 hover:text-red-700 hover:underline">
              Dejar libres esas horas
            </button>
            <div className="flex gap-2">
              <button type="button" onClick={onCerrar} className={botonSecundario}>Cancelar</button>
              <button type="submit" className="rounded-lg bg-marca-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-marca-700">Asignar</button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
