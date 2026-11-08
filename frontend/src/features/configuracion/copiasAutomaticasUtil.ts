// Copias de seguridad automáticas: la lógica de la pantalla, sin dependencias, para probarla con el ejecutor de Node.
// Las reglas deben coincidir con las del servidor (backend/src/EstudioContable.Domain/Entities/ConfiguracionDeCopias.cs).

export type Frecuencia = 'CadaSeisHoras' | 'CadaDoceHoras' | 'Diaria' | 'Semanal' | 'Mensual'

/** Lo que se programa. Todas las horas son de Argentina, como en el servidor. */
export interface Programacion {
  activa: boolean
  frecuencia: Frecuencia
  hora: number
  minuto: number
  /** 1 = lunes ... 7 = domingo. Solo cuenta en la frecuencia semanal. */
  diaDeLaSemana: number | null
  /** 1 a 28. Solo cuenta en la frecuencia mensual. */
  diaDelMes: number | null
  conservar: number
}

/** La programación vigente y cómo le fue a la última copia (lo que devuelve /api/copias/programacion). */
export interface EstadoDeCopias extends Programacion {
  proximaCopia: string | null
  ultimaCopia: string | null
  ultimoIntento: string | null
  ultimoError: string | null
}

/** Una copia guardada en la carpeta de copias automáticas. */
export interface CopiaGuardada {
  nombre: string
  bytes: number
  fecha: string
}

export const FRECUENCIAS: readonly { valor: Frecuencia; texto: string }[] = [
  { valor: 'CadaSeisHoras', texto: 'Cada 6 horas' },
  { valor: 'CadaDoceHoras', texto: 'Cada 12 horas' },
  { valor: 'Diaria', texto: 'Todos los días' },
  { valor: 'Semanal', texto: 'Una vez por semana' },
  { valor: 'Mensual', texto: 'Una vez por mes' },
]

export const DIAS_DE_LA_SEMANA: readonly { numero: number; nombre: string }[] = [
  { numero: 1, nombre: 'Lunes' },
  { numero: 2, nombre: 'Martes' },
  { numero: 3, nombre: 'Miércoles' },
  { numero: 4, nombre: 'Jueves' },
  { numero: 5, nombre: 'Viernes' },
  { numero: 6, nombre: 'Sábado' },
  { numero: 7, nombre: 'Domingo' },
]

// «Los lunes», «Los sábados»: el plural de cada día.
const PLURAL_DEL_DIA = ['lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábados', 'domingos']

const enteroEntre = (valor: number, minimo: number, maximo: number) => Number.isInteger(valor) && valor >= minimo && valor <= maximo

export type ErroresDeProgramacion = Partial<Record<'hora' | 'minuto' | 'diaDeLaSemana' | 'diaDelMes' | 'conservar', string>>

/** Lo que impide guardar la programación, campo por campo (el servidor lo vuelve a controlar). Vacío si está bien. */
export function validarProgramacion(p: Programacion): ErroresDeProgramacion {
  const errores: ErroresDeProgramacion = {}
  if (!enteroEntre(p.hora, 0, 23)) errores.hora = 'La hora va de 0 a 23.'
  if (!enteroEntre(p.minuto, 0, 59)) errores.minuto = 'Los minutos van de 0 a 59.'
  if (!enteroEntre(p.conservar, 1, 60)) errores.conservar = 'Las copias a conservar van de 1 a 60.'
  if (p.frecuencia === 'Semanal') {
    if (p.diaDeLaSemana === null) errores.diaDeLaSemana = 'Elegí el día de la semana.'
    else if (!enteroEntre(p.diaDeLaSemana, 1, 7)) errores.diaDeLaSemana = 'El día de la semana va de 1 (lunes) a 7 (domingo).'
  }
  if (p.frecuencia === 'Mensual') {
    if (p.diaDelMes === null) errores.diaDelMes = 'Elegí el día del mes.'
    else if (!enteroEntre(p.diaDelMes, 1, 28)) errores.diaDelMes = 'El día del mes va de 1 a 28.'
  }
  return errores
}

const dos = (n: number) => String(n).padStart(2, '0')

/** 2, 5 -> "02:05" */
export const horaComoTexto = (hora: number, minuto: number) => `${dos(hora)}:${dos(minuto)}`

/** "22:30" -> { hora: 22, minuto: 30 }; null si no tiene la forma HH:MM de una hora válida. */
export function parsearHora(texto: string): { hora: number; minuto: number } | null {
  const partes = /^([01]\d|2[0-3]):([0-5]\d)$/.exec(texto)
  return partes ? { hora: Number(partes[1]), minuto: Number(partes[2]) } : null
}

// Los momentos del día de las frecuencias de 6 y 12 horas: la hora elegida más cada intervalo, ordenados.
function momentosDelDia(p: Programacion, cadaHoras: number): string {
  const inicio = p.hora * 60 + p.minuto
  const momentos = Array.from({ length: 24 / cadaHoras }, (_, k) => (inicio + k * cadaHoras * 60) % 1440).sort((a, b) => a - b)
    .map((m) => horaComoTexto(Math.floor(m / 60), m % 60))
  return momentos.length < 2 ? momentos.join('') : `${momentos.slice(0, -1).join(', ')} y ${momentos[momentos.length - 1]}`
}

/** La programación en una frase: «Todos los días a las 22:00», «Los lunes a las 08:30», «El día 5 de cada mes a las 22:00»... */
export function resumenDeProgramacion(p: Programacion): string {
  const hora = horaComoTexto(p.hora, p.minuto)
  switch (p.frecuencia) {
    case 'CadaSeisHoras': return `Cada 6 horas desde las ${hora} (${momentosDelDia(p, 6)})`
    case 'CadaDoceHoras': return `Cada 12 horas desde las ${hora} (${momentosDelDia(p, 12)})`
    case 'Diaria': return `Todos los días a las ${hora}`
    case 'Semanal': return `Los ${PLURAL_DEL_DIA[(p.diaDeLaSemana ?? 1) - 1] ?? 'lunes'} a las ${hora}`
    case 'Mensual': return `El día ${p.diaDelMes ?? 1} de cada mes a las ${hora}`
  }
}

/** ¿Son iguales en lo que importa para su frecuencia? (el día de la semana o del mes solo cuenta en la semanal o la mensual) */
export function programacionesIguales(a: Programacion, b: Programacion): boolean {
  if (a.activa !== b.activa || a.frecuencia !== b.frecuencia || a.hora !== b.hora || a.minuto !== b.minuto || a.conservar !== b.conservar) return false
  if (a.frecuencia === 'Semanal') return a.diaDeLaSemana === b.diaDeLaSemana
  if (a.frecuencia === 'Mensual') return a.diaDelMes === b.diaDelMes
  return true
}

const FORMATO_DE_FECHA = new Intl.DateTimeFormat('es-AR', {
  timeZone: 'America/Argentina/Buenos_Aires', weekday: 'short', day: 'numeric', month: 'numeric', year: 'numeric',
  hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
})

/** La fecha y hora de Argentina, como «sáb, 7/11/2026, 22:00»; «—» si no hay fecha. */
export function textoDeFecha(iso: string | null): string {
  if (!iso) return '—'
  const fecha = new Date(iso)
  return Number.isNaN(fecha.getTime()) ? '—' : FORMATO_DE_FECHA.format(fecha)
}

/** 532 -> "532 B"; 12595 -> "12,3 KB"; 4300000 -> "4,1 MB" */
export function tamanoLegible(bytes: number): string {
  const con1Decimal = (n: number) => n.toLocaleString('es-AR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${con1Decimal(bytes / 1024)} KB`
  return `${con1Decimal(bytes / (1024 * 1024))} MB`
}
