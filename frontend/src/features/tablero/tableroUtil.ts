// Tipos y cálculos puros del Tablero de Gestión: el simulador «¿qué pasaría si…?», cómo se muestran los valores y
// las hojas del Excel. Sin React ni red, para poder probarlos solos.
import type { Hoja } from '../../utils/xlsx';

export type Semaforo = 'verde' | 'amarillo' | 'rojo' | 'sin-meta' | 'sin-datos';

export interface Indicador {
  clave: string;
  nombre: string;
  perspectiva: string;
  sentido: 'mas' | 'menos';
  esPorcentaje: boolean;
  admiteMeta: boolean;
  valor: number | null;
  anterior: number | null;
  variacion: number | null;
  meta: number | null;
  semaforo: Semaforo;
  detalle: string | null;
}

export interface IndicadoresDelMes {
  mes: string;
  mesAnterior: string;
  cierre: string;
  indicadores: Indicador[];
}

export interface PuntoDeEvolucion { periodo: string; ventas: number; compras: number; cuotas: number; resultado: number }
export interface IngresoPorPlan { plan: string; neto: number; cobros: number }
export interface ProductoVendido { producto: string; cantidad: number; total: number }
export interface CompraPorProveedor { proveedor: string; neto: number }

export interface Analisis {
  granularidad: 'mes' | 'dia';
  evolucion: PuntoDeEvolucion[];
  ingresosPorPlan: IngresoPorPlan[];
  /** [lunes..domingo][franjas de 06 a 22 h]: cantidad de ingresos. */
  asistencia: number[][];
  topProductos: ProductoVendido[];
  comprasPorProveedor: CompraPorProveedor[];
}

export interface SocioParaAccionar {
  id: number; nombre: string; apellido: string; telefono: string; plan: string | null;
  motivo: 'no-viene' | 'por-vencer'; dias: number; accion: string;
}

export interface PlanParaSimular {
  id: number; nombre: string; precioMensual: number; duracionMeses: number; sociosVigentes: number; recaudacionMensual: number;
}

export interface Decisiones { socios: SocioParaAccionar[]; planes: PlanParaSimular[] }

export interface Meta { clave: string; valor: number; actualizadoPor: string; actualizadoEn: string }

export const PERSPECTIVAS = ['Financiera', 'Socios', 'Procesos', 'Personas'] as const;
export const DIAS_DE_LA_SEMANA = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
export const PRIMERA_HORA = 6;
export const FRANJAS = 17;   // de 06 a 22 h

/** Indicadores que son importes: se muestran en pesos. */
const DE_PESOS = new Set(['ingresos', 'resultado', 'liquidez', 'costo-entrenadores']);

const redondear = (n: number) => Math.round(n * 100) / 100;
const acotar = (n: number, min: number, max: number) => (Number.isFinite(n) ? Math.min(max, Math.max(min, n)) : 0);
const numero = new Intl.NumberFormat('es-AR', { maximumFractionDigits: 2 });
const pesos = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 });

/**
 * Recaudación mensual de hoy y la proyectada: cada plan con su % de aumento (de -100 a 1000) y una pérdida estimada
 * de socios (0 a 100 %) que se aplica a todos. Mensual = precio / duración en meses × socios vigentes.
 */
export function simular(planes: PlanParaSimular[], aumentos: Record<number, number>, perdida: number): { actual: number; proyectada: number; diferencia: number } {
  const quedan = 1 - acotar(perdida, 0, 100) / 100;
  let actual = 0;
  let proyectada = 0;
  for (const p of planes) {
    const meses = Math.max(1, p.duracionMeses);
    const base = (p.precioMensual / meses) * p.sociosVigentes;
    actual += base;
    proyectada += base * (1 + acotar(aumentos[p.id] ?? 0, -100, 1000) / 100) * quedan;
  }
  actual = redondear(actual);
  proyectada = redondear(proyectada);
  return { actual, proyectada, diferencia: redondear(proyectada - actual) };
}

/** «▲ 12 %», «▼ 3 pts», «= 0 %» o «—» si no hay con qué comparar. */
export function textoDeVariacion(variacion: number | null, esPorcentaje: boolean): string {
  if (variacion === null || !Number.isFinite(variacion)) return '—';
  const flecha = variacion > 0 ? '▲' : variacion < 0 ? '▼' : '=';
  return `${flecha} ${numero.format(Math.abs(variacion))} ${esPorcentaje ? 'pts' : '%'}`;
}

/** El valor de un indicador como se lee: en pesos, en % o como número; sin datos, una raya. */
export function textoDeValor(valor: number | null, esPorcentaje: boolean, clave: string): string {
  if (valor === null || !Number.isFinite(valor)) return '—';
  if (esPorcentaje) return `${numero.format(valor)} %`;
  if (DE_PESOS.has(clave)) return pesos.format(valor);
  return numero.format(valor);
}

export const TEXTO_DEL_SEMAFORO: Record<Semaforo, string> = {
  verde: 'Cumple la meta',
  amarillo: 'Cerca de la meta',
  rojo: 'Lejos de la meta',
  'sin-meta': 'Sin meta',
  'sin-datos': 'Sin datos',
};

/** Las seis hojas del Excel del tablero (se abre también en Power BI). */
export function hojasDelTablero(ind: IndicadoresDelMes, an: Analisis): Hoja[] {
  const importe = (v: number) => ({ v, tipo: 'importe' as const });
  const enc = (titulos: string[]) => titulos.map((t) => ({ v: t, negrita: true }));
  return [
    {
      nombre: 'Indicadores',
      filas: [
        enc(['Perspectiva', 'Indicador', `Valor ${ind.mes}`, `Valor ${ind.mesAnterior}`, 'Variación', 'Meta', 'Semáforo', 'Detalle']),
        ...ind.indicadores.map((i) => [
          i.perspectiva, i.nombre, i.valor, i.anterior, i.variacion === null ? null : textoDeVariacion(i.variacion, i.esPorcentaje),
          i.meta, TEXTO_DEL_SEMAFORO[i.semaforo], i.detalle ?? '',
        ]),
      ],
    },
    {
      nombre: 'Evolución',
      filas: [enc([an.granularidad === 'dia' ? 'Día' : 'Mes', 'Ventas', 'Compras', 'Cuotas', 'Resultado']),
        ...an.evolucion.map((p) => [p.periodo, importe(p.ventas), importe(p.compras), importe(p.cuotas), importe(p.resultado)])],
    },
    {
      nombre: 'Ingresos por plan',
      filas: [enc(['Plan', 'Neto', 'Cobros']), ...an.ingresosPorPlan.map((p) => [p.plan, importe(p.neto), { v: p.cobros, tipo: 'entero' as const }])],
    },
    {
      nombre: 'Asistencia',
      filas: [enc(['Día', ...Array.from({ length: FRANJAS }, (_, k) => `${String(PRIMERA_HORA + k).padStart(2, '0')} h`)]),
        ...an.asistencia.map((fila, d) => [DIAS_DE_LA_SEMANA[d], ...fila.map((n) => ({ v: n, tipo: 'entero' as const }))])],
    },
    {
      nombre: 'Productos',
      filas: [enc(['Producto', 'Cantidad', 'Total']), ...an.topProductos.map((p) => [p.producto, { v: p.cantidad, tipo: 'entero' as const }, importe(p.total)])],
    },
    {
      nombre: 'Compras por proveedor',
      filas: [enc(['Proveedor', 'Neto']), ...an.comprasPorProveedor.map((p) => [p.proveedor, importe(p.neto)])],
    },
  ];
}
