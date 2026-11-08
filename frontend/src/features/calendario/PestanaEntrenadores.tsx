import { useState } from 'react';
import { Pencil, Plus, Power } from 'lucide-react';
import toast from 'react-hot-toast';
import { cambiarEstadoDeEntrenador } from '../../api/entrenadoresApi';
import { ModalEntrenador } from './ModalEntrenador';
import type { Entrenador } from './calendarioUtil';

interface Props {
  entrenadores: Entrenador[];
  /** Administrador y Contador. La API es la que decide: acá solo se ocultan los botones que Recepción no puede usar. */
  puedeEditar: boolean;
  /** Se llama después de cada alta, edición o cambio de estado, para volver a cargar la lista y el calendario. */
  onCambio: () => void;
}

const boton = 'inline-flex items-center gap-1.5 rounded-md px-3 py-2 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50';
const botonPrimario = `${boton} bg-marca-600 text-white hover:bg-marca-700`;
const botonChico = 'inline-flex items-center gap-1 rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs font-medium text-slate-700 transition-colors hover:bg-slate-50 disabled:opacity-50';

/** La lista de entrenadores del gimnasio: quiénes se pueden poner en el calendario. */
export function PestanaEntrenadores({ entrenadores, puedeEditar, onCambio }: Props) {
  const [editando, setEditando] = useState<Entrenador | null>(null);
  const [creando, setCreando] = useState(false);
  const [cambiandoId, setCambiandoId] = useState<number | null>(null);
  // El motivo por el que no se pudo cambiar el estado va junto a la fila, no como aviso flotante.
  const [aviso, setAviso] = useState<{ id: number; mensaje: string } | null>(null);

  const cambiarEstado = async (e: Entrenador) => {
    setCambiandoId(e.id);
    setAviso(null);
    try {
      await cambiarEstadoDeEntrenador(e.id, !e.activo);
      toast.success(e.activo ? `${e.nombre} quedó desactivado.` : `${e.nombre} quedó activo.`);
      onCambio();
    } catch (error) {
      setAviso({ id: e.id, mensaje: error instanceof Error ? error.message : 'No se pudo cambiar el estado.' });
    } finally {
      setCambiandoId(null);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <p className="max-w-xl text-sm text-slate-500">
          Los entrenadores que se pueden poner en el calendario. Son fichas: no tienen usuario ni contraseña. Para sacar a alguien del
          calendario hay que liberar primero sus horas.
        </p>
        {puedeEditar && (
          <button type="button" onClick={() => setCreando(true)} className={botonPrimario}>
            <Plus className="h-4 w-4" aria-hidden="true" /> Nuevo entrenador
          </button>
        )}
      </div>

      {entrenadores.length === 0 ? (
        <p className="rounded-lg border border-dashed border-slate-300 bg-white px-4 py-3 text-sm text-slate-500">
          Todavía no hay entrenadores cargados.{puedeEditar ? ' Apretá «Nuevo entrenador» para cargar el primero.' : ' El Administrador o el Contador los cargan.'}
        </p>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
          <table className="w-full min-w-[40rem] border-collapse text-sm">
            <caption className="sr-only">Entrenadores del gimnasio</caption>
            <thead>
              <tr className="bg-slate-50 text-left text-slate-700">
                <th scope="col" className="border-b border-slate-200 px-3 py-2.5 font-semibold">Nombre</th>
                <th scope="col" className="border-b border-slate-200 px-3 py-2.5 font-semibold">Teléfono</th>
                <th scope="col" className="border-b border-slate-200 px-3 py-2.5 font-semibold">Especialidad</th>
                <th scope="col" className="border-b border-slate-200 px-3 py-2.5 text-right font-semibold">Horas por semana</th>
                <th scope="col" className="border-b border-slate-200 px-3 py-2.5 font-semibold">Estado</th>
                {puedeEditar && <th scope="col" className="border-b border-slate-200 px-3 py-2.5 font-semibold"><span className="sr-only">Acciones</span></th>}
              </tr>
            </thead>
            <tbody>
              {entrenadores.map((e) => (
                <FilaDeEntrenador key={e.id} entrenador={e} puedeEditar={puedeEditar} ocupado={cambiandoId === e.id}
                  aviso={aviso?.id === e.id ? aviso.mensaje : null}
                  onEditar={() => setEditando(e)} onCambiarEstado={() => void cambiarEstado(e)} />
              ))}
            </tbody>
          </table>
        </div>
      )}

      {(creando || editando) && (
        <ModalEntrenador entrenador={editando} onCerrar={() => { setCreando(false); setEditando(null); }} onGuardado={onCambio} />
      )}
    </div>
  );
}

interface FilaProps {
  entrenador: Entrenador;
  puedeEditar: boolean;
  ocupado: boolean;
  aviso: string | null;
  onEditar: () => void;
  onCambiarEstado: () => void;
}

function FilaDeEntrenador({ entrenador: e, puedeEditar, ocupado, aviso, onEditar, onCambiarEstado }: FilaProps) {
  const columnas = puedeEditar ? 6 : 5;
  return (
    <>
      <tr className={e.activo ? '' : 'bg-slate-50 text-slate-500'}>
        <th scope="row" className="border-t border-slate-200 px-3 py-2.5 text-left font-medium">{e.nombre}</th>
        <td className="border-t border-slate-200 px-3 py-2.5">{e.telefono ?? '—'}</td>
        <td className="border-t border-slate-200 px-3 py-2.5">{e.especialidad ?? '—'}</td>
        <td className="border-t border-slate-200 px-3 py-2.5 text-right tabular-nums">{e.horasSemanales}</td>
        <td className="border-t border-slate-200 px-3 py-2.5">
          <span className={`inline-flex rounded-full border px-2.5 py-0.5 text-xs font-medium ${
            e.activo ? 'border-emerald-200 bg-emerald-50 text-emerald-800' : 'border-slate-300 bg-slate-100 text-slate-600'}`}>
            {e.activo ? 'Activo' : 'Desactivado'}
          </span>
        </td>
        {puedeEditar && (
          <td className="border-t border-slate-200 px-3 py-2.5">
            <div className="flex justify-end gap-2">
              <button type="button" onClick={onEditar} className={botonChico} aria-label={`Editar a ${e.nombre}`}>
                <Pencil className="h-3.5 w-3.5" aria-hidden="true" /> Editar
              </button>
              <button type="button" onClick={onCambiarEstado} disabled={ocupado} className={botonChico}
                aria-label={`${e.activo ? 'Desactivar' : 'Activar'} a ${e.nombre}`}>
                <Power className="h-3.5 w-3.5" aria-hidden="true" /> {e.activo ? 'Desactivar' : 'Activar'}
              </button>
            </div>
          </td>
        )}
      </tr>
      {aviso && (
        <tr>
          <td colSpan={columnas} className="border-t border-amber-200 bg-amber-50 px-3 py-2.5">
            <p role="alert" className="text-sm text-amber-900">{aviso}</p>
          </td>
        </tr>
      )}
    </>
  );
}
