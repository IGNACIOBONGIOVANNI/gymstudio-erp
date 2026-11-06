// Calendario semanal de encargados del gimnasio: lunes a viernes, en turnos de una hora de 06:00 a 22:00.
// La lógica de la grilla está acá, sin dependencias, para probarla con el ejecutor de Node. Las reglas deben coincidir con
// las del servidor (backend/src/EstudioContable.Domain/ReglasDeCalendario.cs).

export const DIAS = [
  { numero: 1, nombre: 'Lunes' },
  { numero: 2, nombre: 'Martes' },
  { numero: 3, nombre: 'Miércoles' },
  { numero: 4, nombre: 'Jueves' },
  { numero: 5, nombre: 'Viernes' },
] as const

/** El turno de 06:00 a 07:00 se guarda como 6. */
export const PRIMERA_HORA = 6
/** El turno de 21:00 a 22:00 se guarda como 21: el gimnasio cierra a las 22. */
export const ULTIMA_HORA = 21
export const LARGO_MAXIMO_DEL_NOMBRE = 80

/** Las horas en que empieza cada turno: 6, 7, ... 21. */
export const HORAS: readonly number[] = Array.from({ length: ULTIMA_HORA - PRIMERA_HORA + 1 }, (_, i) => PRIMERA_HORA + i)

export interface Turno {
  dia: number
  hora: number
  encargado: string
}

/** Quién está de encargado en cada celda, por clave "día-hora". Las celdas vacías no figuran. */
export type Grilla = Record<string, string>

export const clave = (dia: number, hora: number) => `${dia}-${hora}`

const dos = (n: number) => String(n).padStart(2, '0')

/** 6 -> "06:00"; el cierre del último turno se escribe 22 -> "22:00". */
export const textoDeHora = (hora: number) => `${dos(hora)}:00`

/** 6 -> "06:00 a 07:00" */
export const textoDelTurno = (hora: number) => `${textoDeHora(hora)} a ${textoDeHora(hora + 1)}`

export const nombreDelDia = (dia: number) => DIAS.find((d) => d.numero === dia)?.nombre ?? ''

/** El nombre como se guarda: sin caracteres de control, sin espacios repetidos ni sobrantes y de hasta 80 letras. */
export function limpiarNombre(texto: string | null | undefined): string {
  // eslint-disable-next-line no-control-regex
  return (texto ?? '').replace(/[\u0000-\u001f\u007f-\u009f]/g, ' ').replace(/\s+/g, ' ').trim().slice(0, LARGO_MAXIMO_DEL_NOMBRE)
}

export function esCeldaValida(dia: number, hora: number): boolean {
  return Number.isInteger(dia) && dia >= DIAS[0].numero && dia <= DIAS[DIAS.length - 1].numero
    && Number.isInteger(hora) && hora >= PRIMERA_HORA && hora <= ULTIMA_HORA
}

/** La grilla de lo que devuelve la API. Lo que no tiene forma de celda o de nombre se ignora. */
export function grillaDesdeTurnos(turnos: readonly Turno[]): Grilla {
  const grilla: Grilla = {}
  for (const t of turnos) {
    const nombre = limpiarNombre(t.encargado)
    if (nombre && esCeldaValida(t.dia, t.hora)) grilla[clave(t.dia, t.hora)] = nombre
  }
  return grilla
}

/** Lo que se envía a la API: solo las celdas con una persona, ordenadas por día y hora. */
export function turnosDesdeGrilla(grilla: Grilla): Turno[] {
  const turnos: Turno[] = []
  for (const dia of DIAS) {
    for (const hora of HORAS) {
      const encargado = limpiarNombre(grilla[clave(dia.numero, hora)])
      if (encargado) turnos.push({ dia: dia.numero, hora, encargado })
    }
  }
  return turnos
}

/** ¿Son iguales las dos grillas, celda por celda? (los nombres se comparan ya limpios) */
export function grillasIguales(a: Grilla, b: Grilla): boolean {
  const x = turnosDesdeGrilla(a)
  const y = turnosDesdeGrilla(b)
  return x.length === y.length && x.every((t, i) => t.dia === y[i].dia && t.hora === y[i].hora && t.encargado === y[i].encargado)
}

export function ponerEncargado(grilla: Grilla, dia: number, hora: number, texto: string): Grilla {
  const nueva = { ...grilla }
  // Mientras se escribe se respetan los espacios; se limpian al guardar (turnosDesdeGrilla).
  // eslint-disable-next-line no-control-regex
  const crudo = texto.replace(/[\u0000-\u001f\u007f-\u009f]/g, '').slice(0, LARGO_MAXIMO_DEL_NOMBRE)
  if (crudo.trim()) nueva[clave(dia, hora)] = crudo
  else delete nueva[clave(dia, hora)]
  return nueva
}

export interface RangoDeTurno {
  /** Nombre de la persona; vacío para dejar libres las celdas. */
  encargado: string
  /** Números de día (1 = lunes ... 5 = viernes). */
  dias: readonly number[]
  /** Hora en que empieza el rango (6 a 21). */
  desde: number
  /** Hora en que termina el rango (7 a 22): el turno de la última hora termina a esa hora. */
  hasta: number
}

/** Las celdas que abarca el rango: cada día elegido, cada hora desde `desde` hasta antes de `hasta`. */
export function celdasDelRango(rango: RangoDeTurno): { dia: number; hora: number }[] {
  const celdas: { dia: number; hora: number }[] = []
  for (const dia of [...new Set(rango.dias)].sort((a, b) => a - b)) {
    for (let hora = rango.desde; hora < rango.hasta; hora++) {
      if (esCeldaValida(dia, hora)) celdas.push({ dia, hora })
    }
  }
  return celdas
}

/** Mensaje de lo que impide aplicar el rango, o null si se puede. */
export function problemaDelRango(rango: RangoDeTurno, permitirVacio = false): string | null {
  if (!permitirVacio && !limpiarNombre(rango.encargado)) return 'Escribí el nombre del encargado.'
  if (rango.dias.length === 0) return 'Elegí al menos un día.'
  if (rango.hasta <= rango.desde) return 'La hora de fin tiene que ser posterior a la de inicio.'
  if (celdasDelRango(rango).length === 0) return 'El rango no abarca ninguna hora del calendario.'
  return null
}

/** Carga a la persona en todas las celdas del rango (o las deja libres si el nombre está vacío). */
export function aplicarRango(grilla: Grilla, rango: RangoDeTurno): Grilla {
  const nombre = limpiarNombre(rango.encargado)
  const nueva = { ...grilla }
  for (const { dia, hora } of celdasDelRango(rango)) {
    if (nombre) nueva[clave(dia, hora)] = nombre
    else delete nueva[clave(dia, hora)]
  }
  return nueva
}

/** Cuántos turnos ya cargados, de otra persona, se pisarían al aplicar el rango. */
export function turnosQueSePisan(grilla: Grilla, rango: RangoDeTurno): number {
  const nombre = limpiarNombre(rango.encargado)
  return celdasDelRango(rango).filter(({ dia, hora }) => {
    const actual = limpiarNombre(grilla[clave(dia, hora)])
    return actual !== '' && actual !== nombre
  }).length
}

export interface CeldaDeLectura {
  /** Texto de la celda ("" si está libre). */
  texto: string
  /** Cuántas horas seguidas ocupa (la misma persona en turnos consecutivos se muestra como un solo bloque). */
  filas: number
}

/**
 * La grilla lista para mostrar o imprimir, fila por fila (una por hora). Cada fila trae una entrada por día, o `null` si esa
 * celda ya la cubre el bloque de arriba (rowSpan): la misma persona en horas seguidas se ve como un solo bloque.
 */
export function filasParaMostrar(grilla: Grilla): { hora: number; celdas: (CeldaDeLectura | null)[] }[] {
  const nombreEn = (dia: number, hora: number) => limpiarNombre(grilla[clave(dia, hora)])
  return HORAS.map((hora) => ({
    hora,
    celdas: DIAS.map((dia): CeldaDeLectura | null => {
      const nombre = nombreEn(dia.numero, hora)
      if (!nombre) return { texto: '', filas: 1 }
      // Una celda del medio de un bloque la cubre el bloque de arriba.
      if (hora > PRIMERA_HORA && nombreEn(dia.numero, hora - 1) === nombre) return null
      let filas = 1
      while (hora + filas <= ULTIMA_HORA && nombreEn(dia.numero, hora + filas) === nombre) filas++
      return { texto: nombre, filas }
    }),
  }))
}

/** Los nombres distintos del calendario, en orden alfabético (para asignarle a cada uno su color). */
export function encargadosDistintos(grilla: Grilla): string[] {
  return [...new Set(turnosDesdeGrilla(grilla).map((t) => t.encargado))].sort((a, b) => a.localeCompare(b, 'es'))
}

/** Cuántas horas semanales tiene cada persona. */
export function horasPorEncargado(grilla: Grilla): { encargado: string; horas: number }[] {
  const cuenta = new Map<string, number>()
  for (const t of turnosDesdeGrilla(grilla)) cuenta.set(t.encargado, (cuenta.get(t.encargado) ?? 0) + 1)
  return [...cuenta].map(([encargado, horas]) => ({ encargado, horas })).sort((a, b) => a.encargado.localeCompare(b.encargado, 'es'))
}
