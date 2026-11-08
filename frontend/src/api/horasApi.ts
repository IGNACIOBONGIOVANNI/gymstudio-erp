// Horas cumplidas por los entrenadores y presupuesto de pago. Es dinero: solo lo usan el Administrador y el Contador (a Recepción el servidor
// le contesta 403).
import { ERROR_DE_CONEXION, mensajeDeError } from './errores';
import type { CambioDeHoras, HorasDelPeriodo, Presupuesto } from '../features/entrenadores/horasUtil';

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

const periodo = (desde: string, hasta: string) => `desde=${encodeURIComponent(desde)}&hasta=${encodeURIComponent(hasta)}`;

/** Los entrenadores del período y un renglón por cada día con horas programadas o ya cargado (desde y hasta, los dos incluidos; hasta 62 días). */
export async function obtenerHoras(desde: string, hasta: string): Promise<HorasDelPeriodo> {
  return (await pedir(`/api/entrenadores/horas?${periodo(desde, hasta)}`, undefined, 'No se pudieron cargar las horas.')).json();
}

/** Guarda solo lo modificado. Todo o nada: si un renglón es inválido, el servidor no guarda ninguno y dice por qué. */
export async function guardarHoras(cambios: CambioDeHoras[]): Promise<void> {
  await pedir('/api/entrenadores/horas', conJson('PUT', { items: cambios }), 'No se pudieron guardar las horas.');
}

/** Carga, hasta hoy, las horas que el calendario le asigna a cada entrenador con valor de hora, sin pisar lo ya cargado. */
export async function precargarHoras(desde: string, hasta: string): Promise<{ creados: number; sinValor: string[] }> {
  return (await pedir('/api/entrenadores/horas/precargar', conJson('POST', { desde, hasta }), 'No se pudieron precargar las horas.')).json();
}

export async function obtenerPresupuesto(desde: string, hasta: string): Promise<Presupuesto> {
  return (await pedir(`/api/entrenadores/presupuesto?${periodo(desde, hasta)}`, undefined, 'No se pudo armar el presupuesto.')).json();
}
