// Totales por día del Libro Diario: lo que usan el PDF, el Excel y la vista «Formato planilla» para cerrar cada día con su
// Debe y su Haber antes del total general.
// Sin dependencias de React ni del navegador a propósito: así se prueba con el ejecutor de pruebas de Node.

export interface DiaDelDiario<T> {
  /** El día como «aaaa-mm-dd». */
  dia: string
  /** Los asientos de ese día, en el orden en que llegaron. */
  asientos: T[]
  /** La suma del Debe y del Haber de esos asientos, redondeada al centavo. */
  debe: number
  haber: number
}

interface ConTotales {
  fecha?: string | null
  totalDebe?: number | null
  totalHaber?: number | null
}

const alCentavo = (valor: number) => Math.round(valor * 100) / 100

/** El día de un asiento: la parte «aaaa-mm-dd» de su fecha, sin la hora ni la zona horaria (como se muestra en el libro). */
export function claveDelDia(fecha: string | null | undefined): string {
  return (fecha ?? '').slice(0, 10)
}

/**
 * Junta los asientos seguidos que son del mismo día y suma su Debe y su Haber. La lista llega ordenada por fecha y número de
 * asiento, así que cada día queda en un solo grupo y en su orden.
 */
export function agruparPorDia<T extends ConTotales>(asientos: readonly T[]): DiaDelDiario<T>[] {
  const dias: DiaDelDiario<T>[] = []
  for (const asiento of asientos) {
    const dia = claveDelDia(asiento.fecha)
    let actual = dias[dias.length - 1]
    if (!actual || actual.dia !== dia) {
      actual = { dia, asientos: [], debe: 0, haber: 0 }
      dias.push(actual)
    }
    actual.asientos.push(asiento)
    actual.debe = alCentavo(actual.debe + (asiento.totalDebe ?? 0))
    actual.haber = alCentavo(actual.haber + (asiento.totalHaber ?? 0))
  }
  return dias
}
