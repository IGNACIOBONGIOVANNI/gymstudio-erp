import { useCallback, useRef, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { ListaDeEntrenadores } from './ListaDeEntrenadores';
import { PestanaHoras } from './PestanaHoras';
import { PestanaPresupuesto } from './PestanaPresupuesto';

type Pestana = 'lista' | 'horas' | 'presupuesto';
const PESTANAS: readonly { id: Pestana; titulo: string }[] = [
  { id: 'lista', titulo: 'Lista' },
  { id: 'horas', titulo: 'Horas' },
  { id: 'presupuesto', titulo: 'Presupuesto' },
];

const botonSecundario = 'inline-flex items-center gap-1.5 rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50';

interface Props {
  /** Avisa al menú qué hay sin guardar (o null si nada), para que pregunte antes de cambiar de pantalla. */
  onCambiosSinGuardar: (descripcion: string | null) => void;
}

/**
 * Entrenadores. El Administrador y el Contador ven tres pestañas: la **Lista** de fichas, las **Horas** que cumplió cada uno y el
 * **Presupuesto** de pago. Recepción ve solo la lista y sin pestañas: las horas y los valores son dinero y el servidor no se los da.
 */
export function ModuloEntrenadores({ onCambiosSinGuardar }: Props) {
  const { user } = useAuth();
  // La API es la que decide: acá solo se ocultan las pestañas y los botones que Recepción no puede usar.
  const esGestion = user?.rol === 'Administrador' || user?.rol === 'Contador';

  const [pestana, setPestana] = useState<Pestana>('lista');
  const [pestanaPendiente, setPestanaPendiente] = useState<Pestana | null>(null);
  const cambiosSinGuardar = useRef(false);

  // Las horas escritas y sin guardar: se avisan al menú y se recuerdan para preguntar antes de cambiar de pestaña.
  const alCambiarLoEscrito = useCallback((hay: boolean) => {
    cambiosSinGuardar.current = hay;
    onCambiosSinGuardar(hay ? 'horas sin guardar' : null);
  }, [onCambiosSinGuardar]);

  const irAPestana = (nueva: Pestana) => {
    if (nueva === pestana) return;
    if (cambiosSinGuardar.current) { setPestanaPendiente(nueva); return; }
    setPestana(nueva);
  };

  // Las flechas izquierda y derecha pasan de una pestaña a la otra, como en cualquier juego de pestañas.
  const moverseEntrePestanas = (e: React.KeyboardEvent<HTMLButtonElement>) => {
    if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return;
    e.preventDefault();
    const actual = PESTANAS.findIndex((p) => p.id === pestana);
    const siguiente = PESTANAS[(actual + (e.key === 'ArrowRight' ? 1 : PESTANAS.length - 1)) % PESTANAS.length].id;
    irAPestana(siguiente);
    document.getElementById(`pestana-${siguiente}`)?.focus();
  };

  if (!esGestion) return <ListaDeEntrenadores puedeEditar={false} />;

  return (
    <div className="space-y-4">
      <div role="tablist" aria-label="Secciones de Entrenadores" className="flex gap-1 border-b border-slate-200">
        {PESTANAS.map(({ id, titulo }) => (
          <button key={id} type="button" role="tab" id={`pestana-${id}`} aria-selected={pestana === id} aria-controls={`panel-${id}`}
            tabIndex={pestana === id ? 0 : -1} onClick={() => irAPestana(id)} onKeyDown={moverseEntrePestanas}
            className={`-mb-px border-b-2 px-4 py-2 text-sm font-medium transition-colors ${
              pestana === id ? 'border-marca-600 text-marca-700' : 'border-transparent text-slate-500 hover:text-slate-800'}`}>
            {titulo}
          </button>
        ))}
      </div>

      {pestanaPendiente && (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          <span>Hay cambios sin guardar. ¿Los descartás?</span>
          <span className="flex gap-2">
            <button type="button" onClick={() => setPestanaPendiente(null)} className={botonSecundario}>Seguir editando</button>
            <button type="button" onClick={() => { setPestana(pestanaPendiente); setPestanaPendiente(null); }}
              className="inline-flex items-center rounded-md bg-red-600 px-3 py-2 text-sm font-medium text-white hover:bg-red-700">Sí, descartar</button>
          </span>
        </div>
      )}

      <div role="tabpanel" id={`panel-${pestana}`} aria-labelledby={`pestana-${pestana}`}>
        {pestana === 'lista' && <ListaDeEntrenadores puedeEditar />}
        {pestana === 'horas' && <PestanaHoras onCambios={alCambiarLoEscrito} onIrALaLista={() => irAPestana('lista')} />}
        {pestana === 'presupuesto' && <PestanaPresupuesto />}
      </div>
    </div>
  );
}
