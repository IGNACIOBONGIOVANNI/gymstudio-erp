import { useEffect, useState } from 'react'
import { Printer } from 'lucide-react'
import { LogoGym } from '../../components/LogoGym'
import { FECHA_VERSION, NOMBRE_SISTEMA, VERSION } from '../../acercaDe'
import { SECCIONES } from './indice'
import { CONTENIDO } from './secciones'

// Impresión pensada para fotocopia: blanco y negro, sin fondos, y los títulos no quedan solos al pie de la hoja.
const ESTILOS = `
@media (prefers-reduced-motion: no-preference) { html { scroll-behavior: smooth; } }
@media print {
  @page { size: A4; margin: 16mm 14mm; }
  body { background: #fff !important; color: #000 !important; }
  a { color: inherit !important; text-decoration: none !important; }
}
`

function Indice({ activa }: { activa: string }) {
  return (
    <ul className="space-y-0.5 text-sm">
      {SECCIONES.map((s) => {
        const esta = s.id === activa
        return (
          <li key={s.id}>
            <a
              href={`#${s.id}`}
              aria-current={esta ? 'location' : undefined}
              className={`block border-l-2 py-1 pl-3 pr-2 transition-colors ${
                esta
                  ? 'border-marca-600 font-medium text-marca-700'
                  : 'border-slate-200 text-slate-600 hover:border-slate-400 hover:text-slate-900'
              }`}
            >
              {s.titulo}
            </a>
          </li>
        )
      })}
    </ul>
  )
}

export function Manual() {
  const [activa, setActiva] = useState<string>(SECCIONES[0].id)

  useEffect(() => {
    document.title = `Manual de usuario · ${NOMBRE_SISTEMA}`
    // Si se abrió con un ancla (por ejemplo /manual#facturar), ir a esa sección cuando ya esté dibujada.
    const ancla = decodeURIComponent(window.location.hash.slice(1))
    if (ancla) document.getElementById(ancla)?.scrollIntoView()
  }, [])

  // El índice resalta la sección que se está leyendo.
  useEffect(() => {
    const observador = new IntersectionObserver(
      (entradas) => {
        const visibles = entradas
          .filter((e) => e.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)
        if (visibles[0]) setActiva(visibles[0].target.id)
      },
      { rootMargin: '-10% 0px -75% 0px' },
    )
    SECCIONES.forEach((s) => {
      const el = document.getElementById(s.id)
      if (el) observador.observe(el)
    })
    return () => observador.disconnect()
  }, [])

  return (
    <div className="min-h-screen bg-slate-50 text-slate-800 print:bg-white print:text-black">
      <style>{ESTILOS}</style>

      <header className="bg-slate-900 text-white print:border-b-2 print:border-black print:bg-white print:text-black">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-6 gap-y-3 px-4 py-5 sm:px-6 lg:px-8">
          <div className="flex items-center gap-4">
            <LogoGym className="h-11 w-11 shrink-0 print:hidden" />
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">Manual de usuario</h1>
              <p className="text-sm text-slate-400 print:text-black">
                {NOMBRE_SISTEMA}, versión {VERSION} ({FECHA_VERSION})
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={() => window.print()}
            className="inline-flex items-center gap-2 rounded-lg bg-white/10 px-3.5 py-2 text-sm font-medium text-white transition-colors hover:bg-white/20 focus:outline-none focus:ring-2 focus:ring-marca-400 print:hidden"
          >
            <Printer className="h-4 w-4" aria-hidden="true" />
            Imprimir o guardar en PDF
          </button>
        </div>
      </header>

      <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:grid lg:grid-cols-[16rem_minmax(0,1fr)] lg:gap-12 lg:px-8">
        <nav aria-label="Índice del manual" className="print:hidden">
          <details className="mb-6 rounded-lg bg-white p-4 ring-1 ring-slate-200 lg:hidden">
            <summary className="cursor-pointer text-sm font-semibold text-slate-900">Índice</summary>
            <div className="mt-3">
              <Indice activa={activa} />
            </div>
          </details>
          <div className="hidden lg:sticky lg:top-6 lg:block lg:max-h-[calc(100vh-3rem)] lg:overflow-y-auto lg:pr-2">
            <p className="mb-2 text-sm font-semibold text-slate-900">Contenido</p>
            <Indice activa={activa} />
          </div>
        </nav>

        <main className="min-w-0 max-w-[46rem]">
          <p className="mb-10 text-[15px] leading-7 text-slate-600 print:text-black">
            Esta guía explica, pantalla por pantalla, cómo usar el sistema. Cada parte dice quién la usa. Para buscar una palabra
            puntual, usá <kbd className="rounded border border-slate-300 bg-white px-1.5 text-[13px] print:border-black">Ctrl</kbd>
            {' + '}
            <kbd className="rounded border border-slate-300 bg-white px-1.5 text-[13px] print:border-black">F</kbd>.
          </p>

          <div className="space-y-12">
            {SECCIONES.map((s) => (
              <section key={s.id} id={s.id} aria-labelledby={`t-${s.id}`} className="scroll-mt-6">
                <h2
                  id={`t-${s.id}`}
                  className="border-b border-slate-200 pb-2 text-xl font-semibold tracking-tight text-slate-900 print:break-after-avoid print:border-black"
                >
                  {s.titulo}
                </h2>
                <div className="mt-5 space-y-4 text-[15px] leading-7 text-slate-700 print:text-black">{CONTENIDO[s.id]}</div>
              </section>
            ))}
          </div>

          <footer className="mt-16 border-t border-slate-200 pt-6 text-sm text-slate-500 print:border-black print:text-black">
            {NOMBRE_SISTEMA}, versión {VERSION} ({FECHA_VERSION}).{' '}
            <a href="#empezar" className="font-medium text-marca-700 hover:underline print:hidden">
              Volver al principio
            </a>
          </footer>
        </main>
      </div>
    </div>
  )
}
