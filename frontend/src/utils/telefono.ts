// Cómo se escribe, se guarda y se lee un teléfono (o WhatsApp): país + número de solo dígitos. Se guarda como «+54 1155551234»:
// «+», el código del país, un espacio y el número sin espacios ni guiones. La misma regla que aplica el servidor.
// Sin dependencias de React ni del navegador a propósito: así se prueba con el ejecutor de pruebas de Node.
import { PAIS_POR_DEFECTO, paisDelPrefijo, paisPorCodigo, prefijoAlComienzo } from './paises.ts'

/** El número (sin el código del país) tiene de 4 a 14 dígitos; entre el código y el número, hasta 15. */
export const LARGO_MINIMO_DEL_NUMERO = 4
export const LARGO_MAXIMO_DEL_NUMERO = 14
export const DIGITOS_MAXIMOS_DEL_TELEFONO = 15

export interface PartesDelTelefono {
  /** Código ISO del país elegido: «AR». */
  pais: string
  /** El número sin el código del país, solo dígitos. Vacío si no se escribió nada. */
  numero: string
}

/** Solo los dígitos 0-9 de un texto (los demás signos y los dígitos de otros alfabetos se descartan). */
export function soloDigitos(texto: string): string {
  return texto.replace(/[^0-9]/g, '')
}

const prefijoDe = (pais: string) => paisPorCodigo(pais)?.prefijo ?? ''

/** Cuántos dígitos puede tener el número de ese país: 15 entre código y número, y nunca más de 14. */
export function maximoDeDigitos(pais: string): number {
  return Math.min(LARGO_MAXIMO_DEL_NUMERO, DIGITOS_MAXIMOS_DEL_TELEFONO - prefijoDe(pais).length)
}

const recortar = (digitos: string, pais: string) => digitos.slice(0, maximoDeDigitos(pais))

/** El teléfono como se guarda: «+54 1155551234», o vacío si no hay número. */
export function armarTelefono(pais: string, numero: string): string {
  const digitos = recortar(soloDigitos(numero), pais)
  return digitos ? `+${prefijoDe(pais)} ${digitos}` : ''
}

/**
 * Lo que se muestra en el selector y en el campo a partir de lo guardado. Lo guardado con el formato de siempre (sin «+»,
 * con espacios y guiones: «11 5555-1234») se lee como un número del país por defecto, con solo sus dígitos; al guardar queda en el formato nuevo.
 */
export function leerTelefono(guardado: string | null | undefined, paisPorDefecto: string = PAIS_POR_DEFECTO): PartesDelTelefono {
  const texto = (guardado ?? '').trim()
  if (!texto) return { pais: paisPorDefecto, numero: '' }
  // Con letras u otros signos («4444-5555 / 4444-6666», «interno 12») no hay un solo número que leer: juntar los dígitos armaría uno inventado.
  if (!/^[0-9+\-().\s]+$/.test(texto)) return { pais: paisPorDefecto, numero: '' }

  if (texto.startsWith('+')) {
    // «+54 9 11 5555-1234»: el código es lo que está antes del primer espacio.
    const separado = /^\+([0-9]{1,3})\s+(.*)$/s.exec(texto)
    if (separado) {
      const pais = paisDelPrefijo(separado[1])
      if (pais) return { pais: pais.codigo, numero: recortar(soloDigitos(separado[2]), pais.codigo) }
    }
    // «+5491155551234»: sin espacio; el código es el que coincide al comienzo.
    const digitos = soloDigitos(texto)
    const prefijo = prefijoAlComienzo(digitos)
    const pais = prefijo ? paisDelPrefijo(prefijo) : undefined
    if (pais && prefijo) return { pais: pais.codigo, numero: recortar(digitos.slice(prefijo.length), pais.codigo) }
  }
  return { pais: paisPorDefecto, numero: recortar(soloDigitos(texto), paisPorDefecto) }
}

/** Lo guardado con el formato de siempre, llevado al formato nuevo («11 5555-1234» → «+54 1155551234»); vacío si no se puede leer. */
export function normalizarTelefono(guardado: string | null | undefined): string {
  const partes = leerTelefono(guardado)
  return armarTelefono(partes.pais, partes.numero)
}

/** Lo que pasa cuando se escribe en el campo del número: solo quedan los dígitos y el país elegido no cambia. */
export function interpretarEscrito(texto: string, paisActual: string): PartesDelTelefono {
  return { pais: paisActual, numero: recortar(soloDigitos(texto), paisActual) }
}

/** Lo que pasa cuando se pega algo: si es un número internacional completo («+54 9 11 5555-1234») cambia también el país; si no, solo los dígitos. */
export function interpretarPegado(texto: string, paisActual: string): PartesDelTelefono {
  const limpio = texto.trim()
  return limpio.startsWith('+') ? leerTelefono(limpio, paisActual) : interpretarEscrito(limpio, paisActual)
}

const FORMA_GUARDADA = /^\+([0-9]{1,3}) ([0-9]{4,14})$/

/** Por qué el teléfono no sirve, o null si sirve. Vacío vale: es opcional. `campo` es cómo se llama en el mensaje («El teléfono», «El WhatsApp»). */
export function errorDeTelefono(valor: string, campo: string): string | null {
  const texto = valor.trim()
  if (!texto) return null
  // Un número a medias (el código del país y de 1 a 3 dígitos) es lo que queda cuando se deja de escribir antes de tiempo.
  if (/^\+[0-9]{1,3} [0-9]{1,3}$/.test(texto)) return `${campo} tiene que tener al menos ${LARGO_MINIMO_DEL_NUMERO} dígitos después del código del país.`
  const forma = FORMA_GUARDADA.exec(texto)
  if (!forma) return `${campo} tiene que llevar el código del país y solo números, por ejemplo +54 1155551234.`
  if (forma[1].length + forma[2].length > DIGITOS_MAXIMOS_DEL_TELEFONO) return `${campo} es demasiado largo: entre el código y el número puede haber hasta ${DIGITOS_MAXIMOS_DEL_TELEFONO} dígitos.`
  return null
}
