import { enviarJson, pedir } from './rutinasApi';

export type EstadoCuota = 'SinPlan' | 'AlDia' | 'PorVencer' | 'Vencida';

export interface CuotaDelSocio {
  estado: EstadoCuota;
  /** "aaaa-mm-dd": null si todavía no pagó ninguna cuota. */
  vence: string | null;
  diasParaVencer: number | null;
}

export interface EjercicioDelDia {
  ejercicio: string;
  grupoMuscular: string;
  series: number;
  repeticiones: string;
  descansoSegundos: number;
  indicaciones: string;
}

export interface RutinaDelDia {
  asignacionId: number;
  nombre: string;
  objetivo: string;
  nivel: string;
  vencida: boolean;
  hasta: string | null;
  dia: number | null;
  diasTotales: number;
  ejercicios: EjercicioDelDia[];
}

export interface SocioDelIngreso {
  id: number;
  nombre: string;
  apellido: string;
  dni: string;
}

export interface ResultadoIngreso {
  id: number;
  yaRegistrado: boolean;
  registradaEn: string;
  socio: SocioDelIngreso;
  cuota: CuotaDelSocio;
  visitasDelMes: number;
  ultimaVisitaAnterior: string | null;
  rutina: RutinaDelDia | null;
}

export interface IngresoDeHoy {
  id: number;
  registradaEn: string;
  registradaPor: string;
  socio: SocioDelIngreso;
  estado: EstadoCuota;
}

export interface SocioEnRiesgo {
  id: number;
  nombre: string;
  apellido: string;
  dni: string;
  telefono: string;
  ultimaVisita: string | null;
  diasSinVenir: number;
  venceCuota: string;
}

export const registrarIngreso = (dni: string) =>
  pedir<ResultadoIngreso>('/api/asistencias/ingreso', 'No se pudo registrar el ingreso', enviarJson('POST', { dni }));

export const ingresosDeHoy = () =>
  pedir<IngresoDeHoy[]>('/api/asistencias/hoy', 'No se pudieron cargar los ingresos de hoy');

export const sociosEnRiesgo = (dias: number) =>
  pedir<SocioEnRiesgo[]>(`/api/asistencias/en-riesgo?dias=${dias}`, 'No se pudo cargar la lista de socios en riesgo');

export const deshacerIngreso = (id: number) =>
  pedir<void>(`/api/asistencias/${id}`, 'No se pudo deshacer el ingreso', { method: 'DELETE' });
