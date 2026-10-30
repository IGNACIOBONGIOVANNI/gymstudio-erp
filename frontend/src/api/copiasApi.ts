// Copia de seguridad completa del sistema (solo Administrador): descargar, comparar con el sistema actual y restaurar.
import { ERROR_DE_CONEXION, mensajeDeError } from './errores';
import type { ComparacionDeCopia } from '../features/configuracion/copiasUtil';

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
