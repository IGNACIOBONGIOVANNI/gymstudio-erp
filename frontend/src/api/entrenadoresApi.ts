// Entrenadores del gimnasio: los ven todos los roles; los cargan, editan y desactivan el Administrador y el Contador.
import { ERROR_DE_CONEXION, mensajeDeError } from './errores';
import type { Entrenador } from '../features/calendario/calendarioUtil';

async function pedir(url: string, init: RequestInit | undefined, porDefecto: string): Promise<Response> {
  let res: Response;
  try {
    res = await fetch(url, init);
  } catch {
    throw new Error(ERROR_DE_CONEXION);
  }
  if (!res.ok) throw new Error(await mensajeDeError(res, porDefecto));
  return res;
}

const conJson = (metodo: string, cuerpo: unknown): RequestInit => ({
  method: metodo,
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(cuerpo),
});

/** La lista por nombre, con las horas semanales de cada uno. */
export async function obtenerEntrenadores(soloActivos = false): Promise<Entrenador[]> {
  return (await pedir(`/api/entrenadores?soloActivos=${soloActivos}`, undefined, 'No se pudo cargar la lista de entrenadores.')).json();
}

export interface DatosDeEntrenador {
  /** Sin `id` es un alta; con `id`, una edición. */
  id?: number
  nombre: string
  telefono: string
  especialidad: string
}

export async function guardarEntrenador({ id, nombre, telefono, especialidad }: DatosDeEntrenador): Promise<void> {
  const cuerpo = { nombre, telefono, especialidad };
  if (id === undefined) await pedir('/api/entrenadores', conJson('POST', cuerpo), 'No se pudo guardar el entrenador.');
  else await pedir(`/api/entrenadores/${id}`, conJson('PUT', cuerpo), 'No se pudo guardar el entrenador.');
}

/** Desactivar falla (con el motivo) si el entrenador todavía tiene horas en el calendario. */
export async function cambiarEstadoDeEntrenador(id: number, activo: boolean): Promise<void> {
  await pedir(`/api/entrenadores/${id}/estado`, conJson('PUT', { activo }), 'No se pudo cambiar el estado del entrenador.');
}
