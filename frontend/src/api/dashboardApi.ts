export interface ImportesDelMes {
  neto: number;
  iva: number;
  total: number;
  cantidad: number;
}

export interface MesDeLaSerie {
  mes: string; // "2026-10"
  ventas: number;
  compras: number;
}

export interface FinanzasDelMes {
  ventas: ImportesDelMes;
  compras: ImportesDelMes;
  resultado: number;
  ivaAPagar: number;
  cajaSaldo: number;
  bancoSaldo: number;
  facturasAnuladas: number;
  serie: MesDeLaSerie[];
  ingresosPorOrigen: { origen: string; neto: number }[];
  topProductos: { producto: string; cantidad: number; total: number }[];
}

export interface GimnasioResumen {
  sociosConCuotaAlDia: number;
  sociosVencidos: number;
  sociosSinPlan: number;
  cuotasPorVencer: number;
  diasPorVencer: number;
  sociosSinRutina: number;
  rutinasVencidas: number;
}

export interface ResumenInicio {
  mes: string;
  hoy: string;
  /** Solo para Administrador y Contador: Recepción recibe null. */
  finanzas: FinanzasDelMes | null;
  gimnasio: GimnasioResumen;
  stockBajo: { id: number; nombre: string; stock: number }[];
  umbralStockBajo: number;
}

const pesos = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' });
export const formatearPesos = (n: number) => pesos.format(n);

/** Eje de importes: 0 / 1 K / 2,5 M, para que entre en el gráfico. */
export function compacto(n: number): string {
  const abs = Math.abs(n);
  const redondeo = (v: number) => String(Math.round(v * 10) / 10).replace('.', ',');
  if (abs >= 1_000_000) return `${redondeo(n / 1_000_000)} M`;
  if (abs >= 1_000) return `${redondeo(n / 1_000)} K`;
  return String(Math.round(n));
}

/** Techo "redondo" para el eje: 7.340 -> 8.000, 1.230.000 -> 1.500.000. */
export function techoDelEje(maximo: number): number {
  if (maximo <= 0) return 100;
  const potencia = 10 ** Math.floor(Math.log10(maximo));
  const paso = [1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10].find((m) => m * potencia >= maximo) ?? 10;
  return paso * potencia;
}

const MESES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];

/** "2026-10" -> "Octubre 2026" (o "Oct" en versión corta). */
export function nombreDelMes(mes: string, corto = false): string {
  const [anio, numero] = mes.split('-').map(Number);
  const nombre = MESES[(numero || 1) - 1] ?? mes;
  const capitalizado = nombre.charAt(0).toUpperCase() + nombre.slice(1);
  return corto ? capitalizado.slice(0, 3) : `${capitalizado} ${anio}`;
}
