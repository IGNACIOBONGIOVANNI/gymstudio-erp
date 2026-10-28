import { ERROR_DE_CONEXION, mensajeDeError } from './errores';

export interface EjercicioDto {
  id: number;
  nombre: string;
  grupoMuscular: string;
  activo: boolean;
  enRutinas: number;
}

export interface RutinaResumen {
  id: number;
  nombre: string;
  objetivo: string;
  nivel: string;
  activa: boolean;
  ejercicios: number;
  dias: number;
  sociosActivos: number;
}

export interface RenglonRutina {
  ejercicioId: number;
  ejercicio?: string;
  grupoMuscular?: string;
  dia: number;
  series: number;
  repeticiones: string;
  descansoSegundos: number;
  indicaciones: string;
}

export interface RutinaDetalle {
  id: number;
  nombre: string;
  objetivo: string;
  nivel: string;
  activa: boolean;
  ejercicios: RenglonRutina[];
}

export interface AsignacionResumen {
  id: number;
  rutinaId: number;
  rutinaNombre: string;
  nivel: string;
  fechaInicio: string;
  fechaFin: string | null;
  observaciones: string;
  activa: boolean;
}

export interface AsignacionDetalle {
  id: number;
  fechaInicio: string;
  fechaFin: string | null;
  observaciones: string;
  activa: boolean;
  socio: { id: number; nombre: string; apellido: string; dni: string };
  rutina: RutinaDetalle;
}

/** Llama a la API y devuelve el JSON; ante un error lanza un Error con el motivo para mostrarlo. */
export async function pedir<T = unknown>(url: string, porDefecto: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(url, init);
  } catch {
    throw new Error(ERROR_DE_CONEXION);
  }
  if (!res.ok) throw new Error(await mensajeDeError(res, porDefecto));
  const texto = await res.text();
  return (texto ? JSON.parse(texto) : undefined) as T;
}

export const enviarJson = (metodo: 'POST' | 'PUT', cuerpo: unknown): RequestInit => ({
  method: metodo,
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(cuerpo),
});

/**
 * Una asignación está vencida si sigue activa pero su fecha de vencimiento ya pasó.
 * Las fechas llegan como "aaaa-mm-dd", así que se comparan como texto.
 */
export function estadoDeAsignacion(a: { activa: boolean; fechaFin: string | null }, hoy: string): 'Vigente' | 'Vencida' | 'Finalizada' {
  if (!a.activa) return 'Finalizada';
  return a.fechaFin && a.fechaFin < hoy ? 'Vencida' : 'Vigente';
}

export const textoDescanso = (segundos: number) =>
  segundos <= 0 ? '-' : segundos % 60 === 0 ? `${segundos / 60} min` : segundos > 60 ? `${Math.floor(segundos / 60)} min ${segundos % 60} s` : `${segundos} s`;
