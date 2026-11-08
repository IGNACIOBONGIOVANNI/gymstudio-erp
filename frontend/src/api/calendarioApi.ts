// Calendario semanal de entrenadores a cargo: lo ven todos los roles y lo guardan el Administrador y el Contador.
import { ERROR_DE_CONEXION, mensajeDeError } from './errores';
import { turnoDesdeServidor, type Turno, type TurnoDelServidor, type TurnoParaGuardar } from '../features/calendario/calendarioUtil';

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
  const turnos: TurnoDelServidor[] = await (await pedir('/api/calendario', undefined, 'No se pudo cargar el calendario.')).json();
  return turnos.map(turnoDesdeServidor);
}

/** Reemplaza el calendario: el servidor guarda solo las diferencias y devuelve cómo quedó. */
export async function guardarCalendario(turnos: TurnoParaGuardar[]): Promise<Turno[]> {
  const res = await pedir(
    '/api/calendario',
    { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ turnos }) },
    'No se pudo guardar el calendario.',
  );
  const guardados: TurnoDelServidor[] = await res.json();
  return guardados.map(turnoDesdeServidor);
}
