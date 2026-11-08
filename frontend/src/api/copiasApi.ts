// Copia de seguridad completa del sistema (solo Administrador): descargar, comparar con el sistema actual y restaurar.
import { ERROR_DE_CONEXION, mensajeDeError } from './errores';
import type { ComparacionDeCopia } from '../features/configuracion/copiasUtil';
import type { CopiaGuardada, EstadoDeCopias, Programacion } from '../features/configuracion/copiasAutomaticasUtil';

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

function formulario(archivo: File, confirmacion?: string): FormData {
  const datos = new FormData();
  datos.append('file', archivo);
  if (confirmacion !== undefined) datos.append('confirmacion', confirmacion);
  return datos;
}

/** Genera la copia completa en el servidor y la devuelve como archivo .zip. */
export async function descargarCopia(): Promise<Blob> {
  return (await pedir('/api/copias/descargar', undefined, 'No se pudo generar la copia de seguridad.')).blob();
}

/** Verifica una copia y la compara con el sistema actual. No modifica nada. */
export async function compararCopia(archivo: File): Promise<ComparacionDeCopia> {
  const res = await pedir('/api/copias/comparar', { method: 'POST', body: formulario(archivo) }, 'No se pudo comparar la copia.');
  return res.json();
}

export interface ResultadoDeRestauracion {
  tablas: number
  filas: number
  integridad: { total: number; correctos: number }
}

/** Reemplaza TODOS los datos del sistema por los de la copia. El servidor exige la confirmación escrita. */
export async function restaurarCopia(archivo: File, confirmacion: string): Promise<ResultadoDeRestauracion> {
  const res = await pedir('/api/copias/restaurar', { method: 'POST', body: formulario(archivo, confirmacion) }, 'No se pudo restaurar la copia.');
  return res.json();
}

/** La programación de las copias automáticas, cuándo es la próxima y cómo le fue a la última. */
export async function obtenerProgramacion(): Promise<EstadoDeCopias> {
  return (await pedir('/api/copias/programacion', undefined, 'No se pudo cargar la programación de las copias.')).json();
}

/** Cambia la programación: el servidor la valida entera (400 con el motivo) y devuelve cómo quedó. */
export async function guardarProgramacion(programacion: Programacion): Promise<EstadoDeCopias> {
  const res = await pedir(
    '/api/copias/programacion',
    { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(programacion) },
    'No se pudo guardar la programación.',
  );
  return res.json();
}

/** Hace una copia en la carpeta de copias automáticas en este momento. */
export async function hacerCopiaAhora(): Promise<CopiaGuardada> {
  return (await pedir('/api/copias/programacion/ahora', { method: 'POST' }, 'No se pudo hacer la copia.')).json();
}

/** Las copias guardadas en la carpeta, de la más nueva a la más vieja. */
export async function listarCopiasAutomaticas(): Promise<CopiaGuardada[]> {
  return (await pedir('/api/copias/automaticas', undefined, 'No se pudo cargar la lista de copias.')).json();
}

export async function descargarCopiaAutomatica(nombre: string): Promise<Blob> {
  return (await pedir(`/api/copias/automaticas/${encodeURIComponent(nombre)}`, undefined, 'No se pudo descargar la copia.')).blob();
}

/** Guarda un archivo en la PC (lo mismo que hace "Descargar" en el navegador). */
export function guardarArchivo(contenido: Blob, nombre: string): void {
  const url = URL.createObjectURL(contenido);
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = nombre;
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
