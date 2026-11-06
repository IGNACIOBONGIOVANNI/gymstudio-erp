// Calendario semanal de encargados: lo ven todos los roles y lo guardan el Administrador y el Contador.
import { ERROR_DE_CONEXION, mensajeDeError } from './errores';
import type { Turno } from '../features/calendario/calendarioUtil';

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

/** Los turnos cargados (solo las celdas con una persona). */
export async function obtenerCalendario(): Promise<Turno[]> {
  return (await pedir('/api/calendario', undefined, 'No se pudo cargar el calendario.')).json();
}

/** Reemplaza el calendario: el servidor guarda solo las diferencias y devuelve cómo quedó. */
export async function guardarCalendario(turnos: Turno[]): Promise<Turno[]> {
  const res = await pedir(
    '/api/calendario',
    { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ turnos }) },
    'No se pudo guardar el calendario.',
  );
  return res.json();
}
