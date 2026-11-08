import { useEffect, useState } from 'react';
import { obtenerFoto } from '../../api/entrenadoresApi';
import type { Entrenador } from '../calendario/calendarioUtil';
import { iniciales } from './entrenadoresUtil';

// Las fotos ya bajadas, por entrenador y por fecha de la foto: al volver a mostrar la lista no se piden de nuevo, y al reemplazar una
// foto la fecha cambia, así que la vieja no se vuelve a usar. Se guarda el Blob (no el enlace), porque cada avatar crea y libera su
// propio enlace de objeto. El tope evita que crezca sin límite en una sesión larga.
const FOTOS = new Map<string, Blob>();
const MAXIMO_EN_MEMORIA = 60;

function recordar(clave: string, foto: Blob) {
  FOTOS.delete(clave);
  FOTOS.set(clave, foto);
  while (FOTOS.size > MAXIMO_EN_MEMORIA) FOTOS.delete(FOTOS.keys().next().value as string);
}

interface CirculoProps {
  nombre: string;
  tamano: number;
}

/** El círculo con las iniciales: lo que se ve mientras no hay foto, mientras carga o si falla. */
export function CirculoDeIniciales({ nombre, tamano }: CirculoProps) {
  return (
    <span aria-hidden="true" style={{ width: tamano, height: tamano, fontSize: Math.max(11, Math.round(tamano * 0.38)) }}
      className="inline-flex shrink-0 select-none items-center justify-center rounded-full bg-slate-200 font-semibold text-slate-600">
      {iniciales(nombre)}
    </span>
  );
}

interface Props {
  entrenador: Pick<Entrenador, 'id' | 'nombre' | 'tieneFoto' | 'fotoActualizada'>;
  /** Lado en píxeles. */
  tamano?: number;
}

/**
 * La foto del entrenador en un círculo. Se pide con la sesión (un `<img>` no puede mandar el token), así que se baja como archivo y
 * se muestra por un enlace de objeto que se libera al desmontar. Sin foto, con error o mientras carga, se ven las iniciales: una foto
 * que falla nunca rompe la lista. La imagen es decorativa: el nombre ya está escrito al lado.
 */
export function AvatarDeEntrenador({ entrenador, tamano = 40 }: Props) {
  const { id, nombre, tieneFoto, fotoActualizada } = entrenador;
  const clave = tieneFoto ? `${id}|${fotoActualizada ?? ''}` : null;
  const [lista, setLista] = useState<{ clave: string; url: string } | null>(null);
  const [rota, setRota] = useState<string | null>(null);

  useEffect(() => {
    if (!clave) return;
    let cancelado = false;
    let url: string | null = null;
    const guardada = FOTOS.get(clave);
    (guardada ? Promise.resolve(guardada) : obtenerFoto(id))
      .then((foto) => {
        if (cancelado || !foto) return;
        recordar(clave, foto);
        url = URL.createObjectURL(foto);
        setLista({ clave, url });
      })
      .catch(() => { /* se queda con las iniciales */ });
    return () => {
      cancelado = true;
      if (url) URL.revokeObjectURL(url);
    };
  }, [clave, id]);

  const url = clave && lista?.clave === clave ? lista.url : null;
  if (!url || rota === url) return <CirculoDeIniciales nombre={nombre} tamano={tamano} />;
  return (
    <img src={url} alt="" aria-hidden="true" width={tamano} height={tamano} onError={() => setRota(url)}
      style={{ width: tamano, height: tamano }} className="shrink-0 rounded-full border border-slate-200 object-cover" />
  );
}
