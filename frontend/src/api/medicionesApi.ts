export interface Medicion {
  id: number;
  fecha: string; // "aaaa-mm-dd"
  pesoKg: number;
  grasaCorporalPct: number | null;
  cinturaCm: number | null;
  caderaCm: number | null;
  pechoCm: number | null;
  brazoCm: number | null;
  musloCm: number | null;
  notas: string;
}

export type ClaveMetrica = 'pesoKg' | 'grasaCorporalPct' | 'cinturaCm' | 'caderaCm' | 'pechoCm' | 'brazoCm' | 'musloCm';

export const METRICAS: { clave: ClaveMetrica; etiqueta: string; unidad: string; obligatoria?: boolean }[] = [
  { clave: 'pesoKg', etiqueta: 'Peso', unidad: 'kg', obligatoria: true },
  { clave: 'grasaCorporalPct', etiqueta: 'Grasa corporal', unidad: '%' },
  { clave: 'cinturaCm', etiqueta: 'Cintura', unidad: 'cm' },
  { clave: 'caderaCm', etiqueta: 'Cadera', unidad: 'cm' },
  { clave: 'pechoCm', etiqueta: 'Pecho', unidad: 'cm' },
  { clave: 'brazoCm', etiqueta: 'Brazo', unidad: 'cm' },
  { clave: 'musloCm', etiqueta: 'Muslo', unidad: 'cm' },
];

const un_decimal = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
export const formatearMedida = (v: number | null | undefined) => (v === null || v === undefined ? '-' : un_decimal.format(v));

/** Puntos (fecha, valor) de una métrica, solo de las mediciones donde se midió. */
export function puntosDe(mediciones: Medicion[], clave: ClaveMetrica): { fecha: string; valor: number }[] {
  return mediciones
    .filter((m) => m[clave] !== null && m[clave] !== undefined)
    .map((m) => ({ fecha: m.fecha, valor: m[clave] as number }))
    .sort((a, b) => a.fecha.localeCompare(b.fecha));
}

/** Variación entre la primera y la última medición de una métrica; null si hay menos de dos. */
export function variacion(mediciones: Medicion[], clave: ClaveMetrica): { desde: string; inicial: number; actual: number; diferencia: number } | null {
  const p = puntosDe(mediciones, clave);
  if (p.length < 2) return null;
  const inicial = p[0].valor, actual = p[p.length - 1].valor;
  return { desde: p[0].fecha, inicial, actual, diferencia: Math.round((actual - inicial) * 10) / 10 };
}

/** Marcas "redondas" para el eje vertical que cubren [minimo, maximo]. */
export function marcasDelEje(minimo: number, maximo: number, cantidad = 4): number[] {
  if (maximo - minimo < 0.5) { const c = (maximo + minimo) / 2; minimo = c - 1; maximo = c + 1; }
  const bruto = (maximo - minimo) / cantidad;
  const potencia = 10 ** Math.floor(Math.log10(bruto));
  const paso = [1, 2, 2.5, 5, 10].map((m) => m * potencia).find((p) => p >= bruto) ?? 10 * potencia;
  const desde = Math.floor(minimo / paso) * paso;
  const hasta = Math.ceil(maximo / paso) * paso;
  const marcas: number[] = [];
  for (let v = desde; v <= hasta + paso / 2; v += paso) marcas.push(Math.round(v * 100) / 100);
  return marcas;
}
