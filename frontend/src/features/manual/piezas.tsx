import type { ReactNode } from 'react'
import { Lightbulb, TriangleAlert } from 'lucide-react'

/** Nombre de un botón, menú o campo tal como se lee en la pantalla. */
export function B({ children }: { children: ReactNode }) {
  return (
    <b className="whitespace-nowrap rounded border border-slate-200 bg-slate-100 px-1.5 py-px text-[13px] font-medium text-slate-800 print:border-slate-400 print:bg-white">
      {children}
    </b>
  )
}

/** Pasos que se hacen en orden. */
export function Pasos({ children }: { children: ReactNode }) {
  return <ol className="list-decimal space-y-2 pl-6 marker:font-semibold marker:text-marca-700 print:marker:text-black">{children}</ol>
}

export function Lista({ children }: { children: ReactNode }) {
  return <ul className="list-disc space-y-1.5 pl-6 marker:text-slate-400">{children}</ul>
}

export function Sub({ children }: { children: ReactNode }) {
  return <h3 className="pt-2 text-base font-semibold text-slate-900">{children}</h3>
}

/** Aclaración que conviene no pasar por alto. "tip" ayuda; "ojo" evita un error. */
export function Nota({ tipo = 'tip', children }: { tipo?: 'tip' | 'ojo'; children: ReactNode }) {
  const ojo = tipo === 'ojo'
  const Icono = ojo ? TriangleAlert : Lightbulb
  return (
    <aside
      className={`flex gap-3 rounded-r-lg border-l-4 px-4 py-3 text-[14px] leading-6 print:break-inside-avoid print:border-l-2 print:border-black print:bg-white print:text-black ${
        ojo ? 'border-amber-500 bg-amber-50 text-amber-950' : 'border-marca-500 bg-marca-50 text-marca-950'
      }`}
    >
      <Icono className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
      <div className="space-y-1.5">
        <p className="font-semibold">{ojo ? 'Ojo' : 'Tip'}</p>
        <div>{children}</div>
      </div>
    </aside>
  )
}

const QUIEN = {
  todos: 'Todos los usuarios',
  gestion: 'Administrador y Contador',
  admin: 'Solo el Administrador',
} as const

/** Quién ve y usa esa parte del sistema. */
export function Quien({ quien, extra }: { quien: keyof typeof QUIEN; extra?: string }) {
  return (
    <p className="text-sm text-slate-500">
      <span className="font-medium text-slate-600">Quién la usa:</span> {QUIEN[quien]}
      {extra ? `. ${extra}` : ''}
    </p>
  )
}

export function Tabla({ columnas, filas }: { columnas: string[]; filas: ReactNode[][] }) {
  return (
    <div className="overflow-x-auto rounded-lg ring-1 ring-slate-200 print:ring-0">
      <table className="w-full min-w-[34rem] border-collapse text-left text-[14px]">
        <thead>
          <tr className="bg-slate-100 text-slate-700 print:bg-white">
            {columnas.map((c, i) => (
              <th key={i} className="border-b border-slate-200 px-3 py-2 font-semibold print:border-black">{c}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {filas.map((fila, i) => (
            <tr key={i} className="border-b border-slate-100 last:border-0 print:break-inside-avoid print:border-slate-300">
              {fila.map((celda, j) => (
                <td key={j} className="px-3 py-2 align-top">{celda}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
