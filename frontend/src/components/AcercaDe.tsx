import { useEffect, useRef } from 'react'
import { BookOpen, X } from 'lucide-react'
import { LogoGym } from './LogoGym'
import { abrirManual, AUTORES, FECHA_VERSION, INSTITUCION, MATERIAS, NOMBRE_SISTEMA, VERSION } from '../acercaDe'

interface Props {
  /** Quién tiene la sesión abierta, por ejemplo "Ana Pérez (Administrador)". */
  usuario: string
  onClose: () => void
}

const INCLUYE =
  'Socios, planes y cuotas; ingreso por DNI y rutinas; facturación de ventas y compras; Caja y Banco; stock; libros de IVA, Diario, Mayor y Balance; y un asistente de consultas.'

export function AcercaDe({ usuario, onClose }: Props) {
  const cerrar = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    cerrar.current?.focus()
    const tecla = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', tecla)
    return () => document.removeEventListener('keydown', tecla)
  }, [onClose])

  const filas: [string, string][] = [
    ['Incluye', INCLUYE],
    ['Tecnología', '.NET 10 y PostgreSQL 15 en el servidor, React 19 en pantalla, todo dentro de Docker.'],
    ['Proyecto final de', MATERIAS.join(', ')],
  ]
  if (AUTORES.length > 0) filas.push(['Desarrollado por', AUTORES.join(', ')])
  if (INSTITUCION) filas.push(['Institución', INSTITUCION])
  filas.push(['Sesión', usuario])

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) onClose()
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="acerca-titulo"
        className="max-h-[calc(100vh-2rem)] w-[95%] max-w-lg overflow-y-auto rounded-xl bg-white shadow-2xl"
      >
        <div className="flex items-center justify-between bg-slate-900 px-6 py-4">
          <div className="flex items-center gap-3">
            <LogoGym className="h-9 w-9 shrink-0" />
            <div>
              <h2 id="acerca-titulo" className="text-lg font-semibold leading-tight text-white">
                Acerca de {NOMBRE_SISTEMA}
              </h2>
              <p className="text-sm text-slate-400">
                Versión {VERSION} ({FECHA_VERSION})
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Cerrar"
            className="text-slate-400 transition-colors hover:text-white"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="space-y-5 p-6">
          <p className="text-[15px] leading-6 text-slate-700">
            Sistema de gestión para gimnasios que lleva en un solo lugar a los socios y la contabilidad del negocio: lo que se
            carga en una pantalla se refleja en las demás.
          </p>

          <dl className="space-y-3 text-sm">
            {filas.map(([titulo, valor]) => (
              <div key={titulo} className="grid gap-x-4 gap-y-0.5 sm:grid-cols-[8.5rem_1fr]">
                <dt className="font-medium text-slate-500">{titulo}</dt>
                <dd className="text-slate-800">{valor}</dd>
              </div>
            ))}
          </dl>

          <p className="border-t border-slate-100 pt-4 text-sm text-slate-500">
            Funciona sin internet una vez instalado. Los datos quedan en la base de datos de esta computadora.
          </p>

          <div className="flex flex-wrap justify-end gap-3">
            <button
              type="button"
              ref={cerrar}
              onClick={onClose}
              className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50"
            >
              Cerrar
            </button>
            <button
              type="button"
              onClick={() => abrirManual()}
              className="inline-flex items-center gap-2 rounded-lg bg-marca-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-marca-700"
            >
              <BookOpen className="h-4 w-4" aria-hidden="true" />
              Abrir el manual de usuario
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
