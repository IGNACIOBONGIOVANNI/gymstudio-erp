// El nombre de una persona (entrenador, socio, usuario) lleva solo letras, espacios, puntos (Lic., Dr.), apóstrofos (D'Angelo) y
// guiones (Pérez-Gómez). Los nombres de empresa no pasan por acá: llevan números y puntos. La misma regla que aplica el servidor.
// Sin dependencias de React ni del navegador a propósito: así se prueba con el ejecutor de pruebas de Node.

export const MENSAJE_DEL_NOMBRE = 'solo puede tener letras, espacios, puntos, apóstrofos y guiones.'

/** Empieza con una letra y sigue con letras, marcas combinadas (la tilde suelta), espacios, puntos, apóstrofos y guiones. */
const FORMA_DEL_NOMBRE = /^\p{L}[\p{L}\p{M} '’.-]*$/u

/**
 * Lo que queda de lo escrito o pegado en el campo del nombre: los números y símbolos no aparecen, los espacios raros pasan a un
 * espacio común (sin repetirse) y el primer carácter es una letra. No recorta los espacios del final: hay que poder escribir
 * «Ana » antes de «Ana López».
 */
export function soloLetrasDelNombre(texto: string): string {
  return texto
    .replace(/\s+/g, ' ')
    .replace(/[^\p{L}\p{M} '’.-]/gu, '')
    .replace(/^[^\p{L}]+/u, '')
}

/** El nombre como se guarda: lo mismo, sin espacios en los extremos. */
export function limpiarNombreDePersona(texto: string): string {
  return soloLetrasDelNombre(texto).trim()
}

/** Por qué el nombre no sirve, o null si sirve (o está vacío: que sea obligatorio lo controla cada formulario). `campo` es «El nombre», «El apellido»... */
export function errorDeNombreDePersona(texto: string, campo = 'El nombre'): string | null {
  const limpio = texto.replace(/\s+/g, ' ').trim()
  if (!limpio) return null
  return FORMA_DEL_NOMBRE.test(limpio) ? null : `${campo} ${MENSAJE_DEL_NOMBRE}`
}
