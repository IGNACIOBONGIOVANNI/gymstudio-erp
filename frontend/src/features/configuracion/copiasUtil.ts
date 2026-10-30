// Funciones puras de la pantalla de copias de seguridad: agrupar las tablas por módulo, resumir una comparación y nombrar
// el archivo. Sin dependencias a propósito: se prueban con el ejecutor de pruebas de Node.

/** Lo que hay que escribir para confirmar que se reemplazan TODOS los datos (lo exige también el servidor). */
export const CONFIRMACION_DE_RESTAURACION = 'RESTAURAR'

export type EstadoDeTabla = 'igual' | 'cambia' | 'solo-copia' | 'solo-ahora'

export interface TablaComparada {
  tabla: string
  etiqueta: string
  grupo: string
  orden: number
  filasCopia: number | null
  filasAhora: number | null
  estado: EstadoDeTabla
  esAuditoria: boolean
}

export interface EstadoDeSistema {
  version: string
  migracion: string
  migraciones: number
}

export interface ComparacionDeCopia {
  fecha: string | null
  copia: EstadoDeSistema
  actual: EstadoDeSistema
  mismoEsquema: boolean
  tablas: TablaComparada[]
  veredicto: 'identica' | 'anterior' | 'otra-version'
  puedeRestaurar: boolean
  motivoSiNoPuede: string | null
}

export interface ResumenDeComparacion {
  /** Tablas de datos (sin la auditoría) que no coinciden con las del sistema actual. */
  cambian: number
  iguales: number
  soloEnLaCopia: number
  soloAhora: number
}

/** Cuenta las tablas por resultado. La auditoría no cuenta: crece sola con cada acción, hasta con descargar la copia. */
export function resumirComparacion(tablas: readonly TablaComparada[]): ResumenDeComparacion {
  const datos = tablas.filter((t) => !t.esAuditoria)
  return {
    cambian: datos.filter((t) => t.estado === 'cambia').length,
    iguales: datos.filter((t) => t.estado === 'igual').length,
    soloEnLaCopia: datos.filter((t) => t.estado === 'solo-copia').length,
    soloAhora: datos.filter((t) => t.estado === 'solo-ahora').length,
  }
}

export interface GrupoDeTablas {
  grupo: string
  tablas: TablaComparada[]
}

/** Las tablas agrupadas por módulo, en el orden en que se muestran (ventas y compras primero). */
export function agruparPorModulo(tablas: readonly TablaComparada[]): GrupoDeTablas[] {
  const ordenadas = [...tablas].sort((a, b) => a.orden - b.orden || a.tabla.localeCompare(b.tabla))
  const grupos: GrupoDeTablas[] = []
  for (const tabla of ordenadas) {
    const ultimo = grupos[grupos.length - 1]
    if (ultimo && ultimo.grupo === tabla.grupo) ultimo.tablas.push(tabla)
    else grupos.push({ grupo: tabla.grupo, tablas: [tabla] })
  }
  return grupos
}

/** "1.234" para una cantidad de filas, o "—" cuando la tabla no existe de ese lado. */
export function textoDeFilas(filas: number | null): string {
  return filas === null ? '—' : filas.toLocaleString('es-AR')
}

/** La diferencia de filas con su signo ("+2", "-1"), o null si no cambió la cantidad. */
export function diferenciaDeFilas(t: Pick<TablaComparada, 'filasCopia' | 'filasAhora'>): string | null {
  if (t.filasCopia === null || t.filasAhora === null || t.filasCopia === t.filasAhora) return null
  const d = t.filasAhora - t.filasCopia
  return d > 0 ? `+${d}` : `${d}`
}

/** gymstudio_copia_20261029_221530.zip, con la hora de la PC (la que ve la persona). */
export function nombreDeArchivoDeCopia(fecha: Date, prefijo = 'gymstudio_copia'): string {
  const dos = (n: number) => String(n).padStart(2, '0')
  return `${prefijo}_${fecha.getFullYear()}${dos(fecha.getMonth() + 1)}${dos(fecha.getDate())}_${dos(fecha.getHours())}${dos(fecha.getMinutes())}${dos(fecha.getSeconds())}.zip`
}

/** ¿Escribió la confirmación tal cual? (mayúsculas incluidas: es una frase para frenar un clic apurado). */
export function confirmacionValida(texto: string): boolean {
  return texto.trim() === CONFIRMACION_DE_RESTAURACION
}
