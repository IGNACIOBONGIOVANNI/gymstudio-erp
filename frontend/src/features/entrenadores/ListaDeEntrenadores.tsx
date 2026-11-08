import { useEffect, useState } from 'react';
import { obtenerEntrenadores } from '../../api/entrenadoresApi';
import type { Entrenador } from '../calendario/calendarioUtil';
import { PestanaEntrenadores } from './PestanaEntrenadores';

const botonSecundario = 'inline-flex items-center gap-1.5 rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50';

interface Props {
  /** Administrador y Contador: pueden cargar y editar, y ven el valor de la hora. */
  puedeEditar: boolean;
}

/**
 * La lista de fichas. Se vuelve a pedir cada vez que se entra (el componente se monta de nuevo al volver de otra pestaña) y después de cada
 * alta, edición o cambio de estado, así las horas por semana siempre salen del calendario guardado.
 */
export function ListaDeEntrenadores({ puedeEditar }: Props) {
  const [entrenadores, setEntrenadores] = useState<Entrenador[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [intento, setIntento] = useState(0);

  useEffect(() => {
    let cancelado = false;
    obtenerEntrenadores()
      .then((lista) => {
        if (cancelado) return;
        setEntrenadores(lista);
        setError(null);
      })
      .catch((e: unknown) => {
        if (!cancelado) setError(e instanceof Error ? e.message : 'No se pudo cargar la lista de entrenadores.');
      });
    return () => { cancelado = true; };
  }, [intento]);

  const recargar = () => setIntento((n) => n + 1);

  if (entrenadores === null && error === null) {
    return <div role="status" className="rounded-xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">Cargando los entrenadores...</div>;
  }

  if (entrenadores === null) {
    return (
      <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-6 text-sm text-red-800">
        <p>{error}</p>
        <button type="button" onClick={() => { setError(null); recargar(); }} className={`${botonSecundario} mt-3`}>Reintentar</button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {error && (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-4 py-2.5 text-sm text-red-800">
          No se pudo actualizar la lista: {error} Lo que ves puede estar desactualizado.
        </p>
      )}
      <PestanaEntrenadores entrenadores={entrenadores} puedeEditar={puedeEditar} onCambio={recargar} />
    </div>
  );
}
