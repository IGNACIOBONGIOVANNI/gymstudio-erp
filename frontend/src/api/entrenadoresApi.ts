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
  email: string
  whatsApp: string
}

/** Da de alta o edita la ficha y devuelve su id (el del alta, o el recibido en una edición): hace falta para subirle la foto. */
export async function guardarEntrenador({ id, nombre, telefono, especialidad, email, whatsApp }: DatosDeEntrenador): Promise<number> {
  const cuerpo = { nombre, telefono, especialidad, email, whatsApp };
  if (id !== undefined) {
    await pedir(`/api/entrenadores/${id}`, conJson('PUT', cuerpo), 'No se pudo guardar el entrenador.');
    return id;
  }
  const res = await pedir('/api/entrenadores', conJson('POST', cuerpo), 'No se pudo guardar el entrenador.');
  return (await res.json()).id as number;
}

/** Sube o reemplaza la foto: el JPEG ya achicado, tal cual en el cuerpo del pedido. */
export async function subirFoto(id: number, foto: Blob): Promise<void> {
  await pedir(`/api/entrenadores/${id}/foto`, { method: 'PUT', headers: { 'Content-Type': 'image/jpeg' }, body: foto }, 'No se pudo guardar la foto.');
}

/** Quita la foto; si no tenía, no pasa nada. */
export async function quitarFoto(id: number): Promise<void> {
  await pedir(`/api/entrenadores/${id}/foto`, { method: 'DELETE' }, 'No se pudo quitar la foto.');
}

/**
 * La foto del entrenador, o null si no tiene (404). Se pide con la sesión (el navegador no puede mandar el token desde un `<img>`),
 * así que se muestra a través de un enlace de objeto creado desde este Blob.
 */
export async function obtenerFoto(id: number): Promise<Blob | null> {
  let res: Response;
  try {
    res = await fetch(`/api/entrenadores/${id}/foto`);
  } catch {
    throw new Error(ERROR_DE_CONEXION);
  }
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(await mensajeDeError(res, 'No se pudo cargar la foto.'));
  return res.blob();
}

/** Desactivar falla (con el motivo) si el entrenador todavía tiene horas en el calendario. */
export async function cambiarEstadoDeEntrenador(id: number, activo: boolean): Promise<void> {
  await pedir(`/api/entrenadores/${id}/estado`, conJson('PUT', { activo }), 'No se pudo cambiar el estado del entrenador.');
}
