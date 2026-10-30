// Lectura, validación y comparación de un archivo de configuración, para mostrar qué cambiaría antes de cargarlo.
// Sin dependencias a propósito: se prueba con el ejecutor de pruebas de Node.
import type { ConfiguracionERP } from './types'

export type CampoConfiguracion = Exclude<keyof ConfiguracionERP, 'ultimaActualizacion'>

type Regla =
  | { tipo: 'texto'; max: number; requerido?: boolean; patron?: RegExp }
  | { tipo: 'numero'; min: number; max: number; entero?: boolean }
  | { tipo: 'si-no' }
  | { tipo: 'opcion'; opciones: readonly string[] }

interface DescripcionDeCampo {
  etiqueta: string
  seccion: string
  regla: Regla
}

const EMPRESA = 'Datos fiscales y empresa'
const CONTABLE = 'Parámetros contables y facturación'
const SEGURIDAD = 'Seguridad y sistema'

/** Los únicos datos que se cargan de un archivo, con su etiqueta y la forma que tienen que tener. */
export const CAMPOS: Record<CampoConfiguracion, DescripcionDeCampo> = {
  razonSocial: { etiqueta: 'Razón social', seccion: EMPRESA, regla: { tipo: 'texto', max: 150, requerido: true } },
  nombreFantasia: { etiqueta: 'Nombre de fantasía', seccion: EMPRESA, regla: { tipo: 'texto', max: 150 } },
  cuit: { etiqueta: 'CUIT', seccion: EMPRESA, regla: { tipo: 'texto', max: 20, requerido: true, patron: /^[0-9][0-9-]*$/ } },
  condicionIva: { etiqueta: 'Condición frente al IVA', seccion: EMPRESA, regla: { tipo: 'opcion', opciones: ['Responsable Inscripto', 'Monotributo', 'Exento'] } },
  puntoVenta: { etiqueta: 'Punto de venta', seccion: EMPRESA, regla: { tipo: 'texto', max: 5, requerido: true, patron: /^[0-9]{1,5}$/ } },
  direccion: { etiqueta: 'Domicilio comercial', seccion: EMPRESA, regla: { tipo: 'texto', max: 200 } },
  telefono: { etiqueta: 'Teléfono', seccion: EMPRESA, regla: { tipo: 'texto', max: 40, patron: /^[0-9+()\s.-]*$/ } },
  email: { etiqueta: 'Email de administración', seccion: EMPRESA, regla: { tipo: 'texto', max: 120, patron: /^([^\s@]+@[^\s@]+)?$/ } },
  inicioActividades: { etiqueta: 'Inicio de actividades', seccion: EMPRESA, regla: { tipo: 'texto', max: 10, patron: /^([0-9]{4}-[0-9]{2}-[0-9]{2})?$/ } },
  ingresosBrutos: { etiqueta: 'Ingresos brutos', seccion: EMPRESA, regla: { tipo: 'texto', max: 120 } },

  monedaPrincipal: { etiqueta: 'Moneda principal', seccion: CONTABLE, regla: { tipo: 'texto', max: 60, requerido: true } },
  alicuotaIvaDefecto: { etiqueta: 'Alícuota de IVA por defecto (%)', seccion: CONTABLE, regla: { tipo: 'numero', min: 0, max: 100 } },
  mesCierreEjercicio: { etiqueta: 'Mes de cierre del ejercicio', seccion: CONTABLE, regla: { tipo: 'texto', max: 20, requerido: true } },
  numeracionAutomaticaAsientos: { etiqueta: 'Numeración automática de asientos', seccion: CONTABLE, regla: { tipo: 'si-no' } },
  asientosAutomaticosFacturacion: { etiqueta: 'Asientos automáticos al facturar', seccion: CONTABLE, regla: { tipo: 'si-no' } },
  metodoCosteoInventario: { etiqueta: 'Método de costeo del inventario', seccion: CONTABLE, regla: { tipo: 'opcion', opciones: ['PPP', 'FIFO'] } },

  tiempoInactividadMinutos: { etiqueta: 'Minutos de inactividad antes de cerrar la sesión', seccion: SEGURIDAD, regla: { tipo: 'numero', min: 0, max: 1440, entero: true } },
  exigirPasswordSegura: { etiqueta: 'Exigir contraseñas seguras', seccion: SEGURIDAD, regla: { tipo: 'si-no' } },
  modoDemostracion: { etiqueta: 'Modo demostración', seccion: SEGURIDAD, regla: { tipo: 'si-no' } },
}

const CLAVES = Object.keys(CAMPOS) as CampoConfiguracion[]
const TAMANO_MAXIMO = 100_000

export interface ValorInvalido {
  campo: string
  motivo: string
  /** Lo que traía el archivo, listo para mostrar: sin caracteres de control y cortado si es largo. */
  valor?: string
}

/** Un valor cualquiera del archivo, como texto seguro para mostrarlo en la vista previa. */
function describirValorCrudo(valor: unknown): string {
  if (typeof valor === 'string') {
    const limpio = [...valor].map((c) => (c.charCodeAt(0) < 32 || c.charCodeAt(0) === 127 ? ' ' : c)).join('').trim()
    return limpio.length === 0 ? '(vacío)' : limpio.length > 60 ? `${limpio.slice(0, 60)}…` : limpio
  }
  if (typeof valor === 'number' || typeof valor === 'boolean') return String(valor)
  if (valor === null) return 'null'
  return Array.isArray(valor) ? '(una lista)' : '(un objeto)'
}

export type LecturaDeArchivo =
  | { ok: true; config: Partial<ConfiguracionERP>; ignorados: string[]; invalidos: ValorInvalido[]; guardadoEl?: string }
  | { ok: false; error: string }

type Validacion = { ok: true; valor: string | number | boolean } | { ok: false; motivo: string }

function validar(campo: CampoConfiguracion, valor: unknown): Validacion {
  const { regla } = CAMPOS[campo]
  switch (regla.tipo) {
    case 'texto': {
      if (typeof valor !== 'string') return { ok: false, motivo: 'tiene que ser un texto' }
      const texto = valor.trim()
      if (regla.requerido && texto.length === 0) return { ok: false, motivo: 'no puede estar vacío' }
      if (texto.length > regla.max) return { ok: false, motivo: `tiene más de ${regla.max} caracteres` }
      if ([...texto].some((c) => c.charCodeAt(0) < 32 || c.charCodeAt(0) === 127)) return { ok: false, motivo: 'tiene caracteres no permitidos' }
      if (regla.patron && !regla.patron.test(texto)) return { ok: false, motivo: 'no tiene el formato esperado' }
      return { ok: true, valor: texto }
    }
    case 'numero': {
      if (typeof valor !== 'number' || !Number.isFinite(valor)) return { ok: false, motivo: 'tiene que ser un número' }
      if (valor < regla.min || valor > regla.max) return { ok: false, motivo: `tiene que estar entre ${regla.min} y ${regla.max}` }
      if (regla.entero && !Number.isInteger(valor)) return { ok: false, motivo: 'tiene que ser un número entero' }
      return { ok: true, valor }
    }
    case 'si-no':
      return typeof valor === 'boolean' ? { ok: true, valor } : { ok: false, motivo: 'tiene que ser sí o no' }
    case 'opcion':
      return typeof valor === 'string' && regla.opciones.includes(valor)
        ? { ok: true, valor }
        : { ok: false, motivo: `tiene que ser una de estas: ${regla.opciones.join(', ')}` }
  }
}

const tieneClave = (objeto: object, clave: string) => Object.prototype.hasOwnProperty.call(objeto, clave)

/**
 * Lee el texto de un archivo de configuración. Solo se toman los datos conocidos y con la forma esperada: lo demás
 * se ignora (si no es de la configuración) o se informa como inválido (si lo es pero no sirve), pero nunca se carga.
 */
export function leerArchivoDeConfiguracion(texto: string): LecturaDeArchivo {
  if (texto.length > TAMANO_MAXIMO) return { ok: false, error: 'El archivo es demasiado grande para ser una configuración.' }

  let datos: unknown
  try {
    datos = JSON.parse(texto)
  } catch {
    return { ok: false, error: 'El archivo no es un JSON válido.' }
  }
  if (datos === null || typeof datos !== 'object' || Array.isArray(datos)) {
    return { ok: false, error: 'El archivo no tiene el formato de una configuración de GymStudio.' }
  }

  const origen = datos as Record<string, unknown>
  const config: Record<string, string | number | boolean> = {}
  const ignorados: string[] = []
  const invalidos: ValorInvalido[] = []

  for (const clave of Object.keys(origen)) {
    if (clave === 'ultimaActualizacion') continue
    if (!tieneClave(CAMPOS, clave)) {
      ignorados.push(clave.slice(0, 40))
      continue
    }
    const resultado = validar(clave as CampoConfiguracion, origen[clave])
    if (resultado.ok) config[clave] = resultado.valor
    else invalidos.push({ campo: clave, motivo: resultado.motivo, valor: describirValorCrudo(origen[clave]) })
  }

  if (!tieneClave(config, 'razonSocial') || !tieneClave(config, 'cuit')) {
    return { ok: false, error: 'El archivo no parece una configuración de GymStudio: falta la razón social o el CUIT, o no son válidos.' }
  }

  const fecha = origen.ultimaActualizacion
  const guardadoEl = typeof fecha === 'string' && fecha.trim().length > 0 ? fecha.trim().slice(0, 40) : undefined
  return { ok: true, config: config as unknown as Partial<ConfiguracionERP>, ignorados, invalidos, guardadoEl }
}

/** Cómo se muestra un valor: sí/no, "(vacío)" cuando no hay nada y el texto tal cual en los demás casos. */
export function mostrarValor(valor: unknown): string {
  if (typeof valor === 'boolean') return valor ? 'Sí' : 'No'
  if (valor === undefined || valor === null || valor === '') return '(vacío)'
  return String(valor)
}

export interface Cambio {
  campo: CampoConfiguracion
  etiqueta: string
  seccion: string
  actual: string
  nuevo: string
}

/** Cómo queda cada dato al cargar el archivo: cambia, ya vale lo mismo, el archivo no lo trae o lo trae inválido. */
export type EstadoDeFila = 'cambia' | 'igual' | 'sin-dato' | 'invalido'

export interface Fila {
  campo: CampoConfiguracion
  etiqueta: string
  seccion: string
  /** Lo que vale ahora. */
  actual: string
  /** Lo que trae el archivo (null si no lo trae). */
  archivo: string | null
  estado: EstadoDeFila
  /** Por qué no se carga, cuando el estado es "invalido". */
  motivo?: string
}

export interface Comparacion {
  /** Todos los datos de la configuración, en el orden de las pantallas, con su estado. */
  filas: Fila[]
  /** Los que cambiarían si se cargara el archivo. */
  cambios: Cambio[]
  /** Datos del archivo que ya valen lo mismo que ahora. */
  iguales: number
  /** Datos de la configuración actual que el archivo no trae (quedan como están). */
  sinDato: number
}

/**
 * Compara, dato por dato, la configuración actual con la del archivo. Devuelve una fila por cada dato (cambie o no),
 * para poder mostrar la comparación completa, y aparte los que cambian.
 */
export function compararConfiguracion(actual: ConfiguracionERP, nueva: Partial<ConfiguracionERP>, invalidos: ValorInvalido[] = []): Comparacion {
  const filas: Fila[] = []
  const cambios: Cambio[] = []
  let iguales = 0
  let sinDato = 0
  for (const campo of CLAVES) {
    const { etiqueta, seccion } = CAMPOS[campo]
    const ahora = mostrarValor(actual[campo])
    if (tieneClave(nueva, campo)) {
      const despues = mostrarValor(nueva[campo])
      if (actual[campo] === nueva[campo]) {
        iguales++
        filas.push({ campo, etiqueta, seccion, actual: ahora, archivo: despues, estado: 'igual' })
      } else {
        cambios.push({ campo, etiqueta, seccion, actual: ahora, nuevo: despues })
        filas.push({ campo, etiqueta, seccion, actual: ahora, archivo: despues, estado: 'cambia' })
      }
      continue
    }
    const invalido = invalidos.find((v) => v.campo === campo)
    if (invalido) {
      filas.push({ campo, etiqueta, seccion, actual: ahora, archivo: invalido.valor ?? '(sin valor)', estado: 'invalido', motivo: invalido.motivo })
      continue
    }
    sinDato++
    filas.push({ campo, etiqueta, seccion, actual: ahora, archivo: null, estado: 'sin-dato' })
  }
  return { filas, cambios, iguales, sinDato }
}
