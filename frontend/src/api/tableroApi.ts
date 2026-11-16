// Tablero de Gestión: indicadores, metas, análisis y decisiones. Son números del negocio: solo Administrador y Contador
// (a Recepción el servidor le contesta 403). Las metas las guarda solo el Administrador.
import { ERROR_DE_CONEXION, mensajeDeError } from './errores';
import type { Analisis, Decisiones, IndicadoresDelMes, Meta } from '../features/tablero/tableroUtil';

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

export async function obtenerIndicadores(mes: string, hoy: string): Promise<IndicadoresDelMes> {
  const url = `/api/tablero/indicadores?mes=${encodeURIComponent(mes)}&hoy=${encodeURIComponent(hoy)}`;
  return (await pedir(url, undefined, 'No se pudieron calcular los indicadores.')).json();
}

export async function obtenerMetas(): Promise<Meta[]> {
  return (await pedir('/api/tablero/metas', undefined, 'No se pudieron cargar las metas.')).json();
}

/** Fija la meta de un indicador; con null la borra. */
export async function guardarMeta(clave: string, valor: number | null): Promise<void> {
  await pedir(`/api/tablero/metas/${encodeURIComponent(clave)}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ valor }),
  }, 'No se pudo guardar la meta.');
}

/** Análisis entre dos meses (hasta 24); con un solo mes la evolución viene por día. */
export async function obtenerAnalisis(desde: string, hasta: string, planId: number | null): Promise<Analisis> {
  const plan = planId === null ? '' : `&planId=${planId}`;
  const url = `/api/tablero/analisis?desde=${encodeURIComponent(desde)}&hasta=${encodeURIComponent(hasta)}${plan}`;
  return (await pedir(url, undefined, 'No se pudo armar el análisis.')).json();
}

export async function obtenerDecisiones(hoy: string): Promise<Decisiones> {
  return (await pedir(`/api/tablero/decisiones?hoy=${encodeURIComponent(hoy)}`, undefined, 'No se pudieron cargar las decisiones.')).json();
}

export interface PlanDelFiltro { id: number; nombre: string }

export async function obtenerPlanes(): Promise<PlanDelFiltro[]> {
  const planes: { id: number; nombre: string }[] = await (await pedir('/api/planes', undefined, 'No se pudieron cargar los planes.')).json();
  return planes.map((p) => ({ id: p.id, nombre: p.nombre }));
}
