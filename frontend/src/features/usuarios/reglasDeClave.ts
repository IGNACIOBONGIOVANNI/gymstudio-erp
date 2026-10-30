// Política de contraseñas, igual a la del servidor (backend/src/EstudioContable.Domain/ReglasDeClave.cs): el servidor es el que
// manda; esto solo le muestra a la persona qué le falta antes de enviar. Sin dependencias: se prueba con el ejecutor de Node.

export const LARGO_MINIMO = 8
export const LARGO_MAXIMO = 128

export interface Requisito {
  id: string
  texto: string
  cumple: (clave: string) => boolean
}

const esNumero = (c: string) => c >= '0' && c <= '9'

/** ¿Hay dos números pegados que sean consecutivos (12, 45, 98)? */
export function tieneNumerosConsecutivos(clave: string): boolean {
  for (let i = 1; i < clave.length; i++) {
    if (esNumero(clave[i - 1]) && esNumero(clave[i]) && Math.abs(clave.charCodeAt(i) - clave.charCodeAt(i - 1)) === 1) return true
  }
  return false
}

const esMayuscula = (c: string) => c !== c.toLowerCase() && c === c.toUpperCase()
const esEspecial = (c: string) => !/[\p{L}\p{N}\s]/u.test(c) && !/\p{Cc}/u.test(c)

export const REQUISITOS: readonly Requisito[] = [
  { id: 'largo', texto: `Al menos ${LARGO_MINIMO} caracteres`, cumple: (c) => c.length >= LARGO_MINIMO && c.length <= LARGO_MAXIMO },
  { id: 'mayuscula', texto: 'Una letra mayúscula', cumple: (c) => [...c].some(esMayuscula) },
  { id: 'numero', texto: 'Un número', cumple: (c) => [...c].some(esNumero) },
  { id: 'especial', texto: 'Un carácter especial (! ? # $ % & * - _ .)', cumple: (c) => [...c].some(esEspecial) },
  { id: 'consecutivos', texto: 'Sin números consecutivos (como 12, 45 o 98)', cumple: (c) => !tieneNumerosConsecutivos(c) },
]

/** Los requisitos que la clave todavía no cumple. Vacío si cumple la política. */
export function requisitosQueFaltan(clave: string): Requisito[] {
  return REQUISITOS.filter((r) => !r.cumple(clave))
}

export const claveValida = (clave: string): boolean => requisitosQueFaltan(clave).length === 0
