// Lógica pura de las horas cumplidas y del presupuesto de pago de los entrenadores: qué se escribe, qué se envía al guardar, los períodos y
// el formato argentino de horas e importes. Sin dependencias de React ni del navegador a propósito: así se prueba con el ejecutor de Node.

export interface DiaDeHoras { entrenadorId: number; fecha: string; programadas: number; cumplidas: number | null; valorHora: number | null; nota: string | null }
export interface HorasDelPeriodo { entrenadores: { id: number; nombre: string; valorHora: number | null; activo: boolean }[]; dias: DiaDeHoras[] }
export interface CambioDeHoras { entrenadorId: number; fecha: string; horas: number | null; nota: string | null }
/** Lo que está escrito en la pantalla, por día (clave = `claveDeDia`): las horas como se tipearon ('' = sin cargar) y la nota. */
export type Borrador = Record<string, { horas: string; nota: string }>
export interface FilaDePresupuesto { entrenadorId: number; nombre: string; dias: number; horas: number; valorHora: number | null; monto: number }
export interface Presupuesto { desde: string; hasta: string; filas: FilaDePresupuesto[]; totalHoras: number; totalMonto: number }

export const DIAS_MAXIMOS_DEL_PERIODO = 62
const MAXIMO_DEL_VALOR_DE_LA_HORA = 999_999_999_999
export const MENSAJE_DE_HORAS = 'Las horas van de 0 a 24, de a cuarto de hora.'
const MENSAJE_DEL_VALOR = 'El valor de la hora tiene que ser un importe de 0 en adelante, con hasta 2 decimales (por ejemplo 1500,50).'

export const claveDeDia = (entrenadorId: number, fecha: string): string => `${entrenadorId}|${fecha}`

// ---------- horas ----------

/** 7.5 → «7,5»; 8 → «8»: la coma de los números argentinos y sin ceros de más. */
export function formatoDeHoras(horas: number): string {
  return String(horas).replace('.', ',')
}

/**
 * Lo que se escribió en «Cumplió»: vacío es «sin cargar» (null); «7,5» y «7.5» valen; 0 es «faltó». Más de 24, negativo, que no sea de a
 * cuarto de hora o cualquier otra cosa es 'invalida'.
 */
export function parsearHoras(texto: string): number | null | 'invalida' {
  const limpio = texto.trim()
  if (limpio === '') return null
  if (!/^\d+([.,]\d+)?$/.test(limpio)) return 'invalida'
  const horas = Number(limpio.replace(',', '.'))
  if (horas > 24 || Math.abs(horas * 4 - Math.round(horas * 4)) > 1e-9) return 'invalida'
  return horas
}

/** El error para mostrar junto al campo de horas, o null si está bien (o vacío). */
export function mensajeDeHoras(texto: string): string | null {
  return parsearHoras(texto) === 'invalida' ? MENSAJE_DE_HORAS : null
}

// ---------- el borrador y lo que se envía ----------

/** El borrador de partida: lo cargado como se escribe y lo que todavía no se cargó, vacío. */
export function borradorDesde(dias: DiaDeHoras[]): Borrador {
  const borrador: Borrador = {}
  for (const d of dias) {
    borrador[claveDeDia(d.entrenadorId, d.fecha)] = { horas: d.cumplidas === null ? '' : formatoDeHoras(d.cumplidas), nota: d.nota ?? '' }
  }
  return borrador
}

/**
 * Lo que hay que enviar al guardar: solo los días modificados. Horas vaciadas en un día cargado → `horas: null` (se borra); una nota vacía o de
 * espacios es null; un texto de horas inválido no se envía (la pantalla ya lo marca junto al campo); un día sin cargar que sigue sin horas no
 * se envía aunque tenga una nota escrita (no hay nada que guardar).
 */
export function cambiosDelBorrador(dias: DiaDeHoras[], borrador: Borrador): CambioDeHoras[] {
  const cambios: CambioDeHoras[] = []
  for (const d of dias) {
    const escrito = borrador[claveDeDia(d.entrenadorId, d.fecha)]
    if (!escrito) continue
    const horas = parsearHoras(escrito.horas)
    if (horas === 'invalida') continue
    if (horas === null && d.cumplidas === null) continue
    const nota = escrito.nota.trim() === '' ? null : escrito.nota.trim()
    const notaOriginal = d.nota === null || d.nota.trim() === '' ? null : d.nota.trim()
    if (horas === d.cumplidas && nota === notaOriginal) continue
    cambios.push({ entrenadorId: d.entrenadorId, fecha: d.fecha, horas, nota })
  }
  return cambios
}

/** «Agregar un día»: suma un renglón vacío (programadas 0) en su lugar, a menos que ese entrenador ya lo tenga. No toca lo original. */
export function conDiaNuevo(dias: DiaDeHoras[], borrador: Borrador, entrenadorId: number, fecha: string): { dias: DiaDeHoras[]; borrador: Borrador } {
  if (dias.some((d) => d.entrenadorId === entrenadorId && d.fecha === fecha)) return { dias, borrador }
  const orden = new Map<number, number>()
  dias.forEach((d, i) => { if (!orden.has(d.entrenadorId)) orden.set(d.entrenadorId, i) })
  const lugar = (id: number) => orden.get(id) ?? dias.length
  const nuevo: DiaDeHoras = { entrenadorId, fecha, programadas: 0, cumplidas: null, valorHora: null, nota: null }
  const ordenados = [...dias, nuevo].sort((a, b) => lugar(a.entrenadorId) - lugar(b.entrenadorId) || a.fecha.localeCompare(b.fecha))
  return { dias: ordenados, borrador: { ...borrador, [claveDeDia(entrenadorId, fecha)]: { horas: '', nota: '' } } }
}

/** Por entrenador (en el orden en que aparecen): las horas cargadas según lo escrito ahora y las programadas por el calendario. */
export function resumenPorEntrenador(dias: DiaDeHoras[], borrador: Borrador): { entrenadorId: number; cargadas: number; programadas: number }[] {
  const resumen = new Map<number, { entrenadorId: number; cargadas: number; programadas: number }>()
  for (const d of dias) {
    const fila = resumen.get(d.entrenadorId) ?? { entrenadorId: d.entrenadorId, cargadas: 0, programadas: 0 }
    const escrito = borrador[claveDeDia(d.entrenadorId, d.fecha)]
    const horas = escrito ? parsearHoras(escrito.horas) : d.cumplidas
    if (typeof horas === 'number') fila.cargadas += horas
    fila.programadas += d.programadas
    resumen.set(d.entrenadorId, fila)
  }
  return [...resumen.values()]
}

// ---------- fechas ----------

/** 'aaaa-mm' → el primer y el último día del mes. */
export function rangoDelMes(mes: string): { desde: string; hasta: string } {
  const [anio, numero] = mes.split('-').map(Number)
  const ultimo = new Date(Date.UTC(anio, numero, 0)).getUTCDate()
  return { desde: `${mes}-01`, hasta: `${mes}-${String(ultimo).padStart(2, '0')}` }
}

const NOMBRES_DE_LOS_DIAS = ['dom', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb']

/** 'aaaa-mm-dd' → «lun», «mar»… */
export function nombreDelDia(fecha: string): string {
  const [anio, mes, dia] = fecha.split('-').map(Number)
  return NOMBRES_DE_LOS_DIAS[new Date(Date.UTC(anio, mes - 1, dia)).getUTCDay()]
}

/** La fecha de hoy en Argentina (UTC-3), como la usa el servidor: a las 22:30 de allá todavía es el mismo día aunque en UTC ya sea el siguiente. */
export function hoyEnArgentina(ahora: Date = new Date()): string {
  return new Date(ahora.getTime() - 3 * 3_600_000).toISOString().slice(0, 10)
}

/** El error de un período (desde y hasta, los dos incluidos) para mostrar junto a los campos, o null si está bien. */
export function mensajeDePeriodo(desde: string, hasta: string): string | null {
  const formato = /^\d{4}-\d{2}-\d{2}$/
  if (!formato.test(desde) || !formato.test(hasta)) return 'Elegí las dos fechas.'
  const dia = (f: string) => { const [a, m, d] = f.split('-').map(Number); return Date.UTC(a, m - 1, d) / 86_400_000 }
  if (dia(desde) > dia(hasta)) return 'La fecha desde no puede ser posterior a la fecha hasta.'
  if (dia(hasta) - dia(desde) + 1 > DIAS_MAXIMOS_DEL_PERIODO) return `El período no puede pasar de ${DIAS_MAXIMOS_DEL_PERIODO} días.`
  return null
}

// ---------- dinero ----------

/** 1234.5 → «$ 1.234,50»: pesos argentinos con punto de miles y coma decimal. */
export function formatoDeImporte(n: number): string {
  const centavos = Math.round(Math.abs(n) * 100)
  const entero = String(Math.floor(centavos / 100)).replace(/\B(?=(\d{3})+(?!\d))/g, '.')
  return `${n < 0 ? '-' : ''}$ ${entero},${String(centavos % 100).padStart(2, '0')}`
}

/** El valor de la hora escrito en la ficha: vacío es «sin valor» (null); un importe de 0 en adelante con hasta 2 decimales; lo demás, 'invalido'. */
export function parsearValorHora(texto: string): number | null | 'invalido' {
  const limpio = texto.trim()
  if (limpio === '') return null
  if (!/^\d+([.,]\d{1,2})?$/.test(limpio)) return 'invalido'
  const valor = Number(limpio.replace(',', '.'))
  return valor > MAXIMO_DEL_VALOR_DE_LA_HORA ? 'invalido' : valor
}

/** El error del valor de la hora para mostrar junto al campo, o null si está bien (o vacío). */
export function mensajeDeValorHora(texto: string): string | null {
  return parsearValorHora(texto) === 'invalido' ? MENSAJE_DEL_VALOR : null
}
