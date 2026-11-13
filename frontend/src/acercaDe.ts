// Datos que muestran "Acerca de" y el manual de usuario. Se cambian acá, en un solo lugar.

export const NOMBRE_SISTEMA = 'GymStudio ERP'
export const VERSION = '1.0.0'
export const FECHA_VERSION = 'octubre de 2026'

/** Quiénes lo desarrollaron. Si está vacío, "Acerca de" no muestra esa línea. Ej.: ['Nombre Apellido'] */
export const AUTORES: string[] = ['Ignacio Facundo Bongiovanni']

/** Institución o cátedra del proyecto. Si está vacío, no se muestra. */
export const INSTITUCION = ''

export const MATERIAS = ['Práctica Profesionalizante II', 'Seguridad de los Sistemas', 'Desarrollo de sistemas Web', 'Base de datos']

/** Dirección del manual (se abre en una ventana aparte). */
export const RUTA_MANUAL = '/manual'
export const VENTANA_MANUAL = 'manual-gymstudio'

/** Abre el manual en su propia ventana; si el navegador la bloquea, en una pestaña común. */
export function abrirManual(seccion?: string): void {
  const url = seccion ? `${RUTA_MANUAL}#${seccion}` : RUTA_MANUAL
  const ventana = window.open(url, VENTANA_MANUAL, 'popup=yes,width=1100,height=800')
  if (ventana) ventana.focus()
  else window.open(url, '_blank')
}
