// Lógica pura de la pantalla de entrenadores: cómo se achica la foto, las iniciales que la reemplazan y los enlaces de contacto.
// Sin dependencias de React ni del navegador a propósito: así se prueba con el ejecutor de pruebas de Node.
import { enlaceWhatsApp } from '../../utils/whatsapp.ts'

/** La foto se guarda cuadrada, de este lado en píxeles (el servidor acepta de 16 a 512). */
export const LADO_DE_LA_FOTO = 256

/** Lo máximo que pesa la foto ya achicada: lo mismo que acepta el servidor. */
export const BYTES_MAXIMOS_DE_LA_FOTO = 150_000

/** Calidades de JPEG que se prueban, de la mejor a la peor. */
const CALIDADES = [0.85, 0.75, 0.65, 0.55, 0.45, 0.35]

/**
 * El cuadrado central de una imagen: de ahí sale la foto, para que una foto apaisada o vertical no quede deformada. Todo en enteros
 * (con una diferencia impar, el cuadrado queda un píxel más cerca del borde izquierdo o de arriba).
 */
export function recorteCuadrado(ancho: number, alto: number): { x: number; y: number; lado: number } {
  if (!(ancho > 0) || !(alto > 0)) return { x: 0, y: 0, lado: 0 }
  const lado = Math.floor(Math.min(ancho, alto))
  return { x: Math.floor((ancho - lado) / 2), y: Math.floor((alto - lado) / 2), lado }
}

/**
 * Codifica la foto con la mejor calidad que entre en el tope de peso: prueba 0,85, 0,75, … 0,35 y devuelve el primer resultado
 * que pesa hasta `BYTES_MAXIMOS_DE_LA_FOTO`. Si ninguna entra, lanza un error que se puede mostrar tal cual. Un error del
 * codificador (la imagen no se pudo leer) sigue de largo.
 */
export async function codificarHastaQueEntre<T extends { size: number }>(codificar: (calidad: number) => Promise<T>): Promise<T> {
  for (const calidad of CALIDADES) {
    const resultado = await codificar(calidad)
    if (resultado.size <= BYTES_MAXIMOS_DE_LA_FOTO) return resultado
  }
  throw new Error('No se pudo achicar la foto lo suficiente. Probá con otra imagen.')
}

/** Las iniciales de las dos primeras palabras del nombre, en mayúscula: lo que se muestra mientras no hay foto. */
export function iniciales(nombre: string): string {
  return nombre
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((palabra) => Array.from(palabra)[0].toLocaleUpperCase('es'))
    .join('')
}

const FORMA_DE_EMAIL = /^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$/

/**
 * `mailto:` solo si tiene forma de email. Cada parte se codifica para que un email raro no pueda agregar destinatarios ni un asunto
 * al enlace (por ejemplo con un «?bcc=…»).
 */
export function enlaceDeEmail(email: string | null | undefined): string | null {
  const limpio = (email ?? '').trim()
  if (!FORMA_DE_EMAIL.test(limpio)) return null
  const arroba = limpio.indexOf('@')
  return `mailto:${encodeURIComponent(limpio.slice(0, arroba))}@${encodeURIComponent(limpio.slice(arroba + 1))}`
}

/** El enlace que abre el chat de WhatsApp con el entrenador (sin mensaje armado), o null si el número no se puede interpretar. */
export function enlaceDeWhatsAppDeEntrenador(whatsApp: string | null | undefined): string | null {
  return enlaceWhatsApp(whatsApp)
}
