import { useEffect, useId, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { ChevronDown, Search } from 'lucide-react';
import { buscarPaises, paisPorCodigo } from '../utils/paises';
import { Bandera } from './Bandera';

interface Props {
  /** Código ISO del país elegido: «AR». */
  pais: string;
  /** Cómo se llama el dato, para el lector de pantalla: «teléfono», «WhatsApp». */
  dato: string;
  onElegir: (codigo: string) => void;
  disabled?: boolean;
}

/** Dónde y de qué alto se abre la lista, calculado al abrirla a partir del botón. */
interface Posicion { top?: number; bottom?: number; left: number; width: number; maxHeight: number }

const ALTO_DESEADO = 320;
const MARGEN = 8;

/**
 * El país del teléfono: un botón con la bandera y el código (🇦🇷 +54) que abre una lista con buscador. Se maneja con el teclado
 * (flechas para recorrer, Enter para elegir, Escape para cerrar) y la lista se dibuja sobre toda la pantalla, así no la recorta
 * un diálogo con desplazamiento. Al desplazar la página o cambiar el tamaño de la ventana, se cierra.
 */
export function SelectorDePais({ pais, dato, onElegir, disabled }: Props) {
  const [abierto, setAbierto] = useState(false);
  const [texto, setTexto] = useState('');
  const [activo, setActivo] = useState(0);
  const [posicion, setPosicion] = useState<Posicion | null>(null);
  const botonRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const id = useId();
  const idLista = `${id}-lista`;
  const idOpcion = (n: number) => `${id}-op-${n}`;

  const elegido = paisPorCodigo(pais);
  const lista = buscarPaises(texto);
  const indice = Math.min(activo, Math.max(lista.length - 1, 0));

  const cerrar = (devolverElFoco: boolean) => {
    setAbierto(false);
    if (devolverElFoco) botonRef.current?.focus();
  };

  const abrir = () => {
    const caja = botonRef.current?.getBoundingClientRect();
    if (!caja) return;
    const ancho = Math.min(340, window.innerWidth - 2 * MARGEN);
    const abajo = window.innerHeight - caja.bottom - MARGEN;
    const arriba = caja.top - MARGEN;
    const haciaAbajo = abajo >= Math.min(ALTO_DESEADO, 220) || abajo >= arriba;
    setPosicion({
      left: Math.max(MARGEN, Math.min(caja.left, window.innerWidth - ancho - MARGEN)),
      width: ancho,
      maxHeight: Math.max(160, Math.min(ALTO_DESEADO, haciaAbajo ? abajo : arriba)),
      ...(haciaAbajo ? { top: caja.bottom + 4 } : { bottom: window.innerHeight - caja.top + 4 }),
    });
    setTexto('');
    setActivo(Math.max(0, buscarPaises('').findIndex((p) => p.codigo === pais)));
    setAbierto(true);
  };

  const elegir = (codigo: string) => {
    cerrar(false);
    onElegir(codigo);
  };

  // Mientras está abierta: se cierra al hacer clic afuera, al desplazar la página o al cambiar el tamaño de la ventana.
  useEffect(() => {
    if (!abierto) return;
    const clicAfuera = (e: MouseEvent) => {
      const objetivo = e.target as Node;
      if (!panelRef.current?.contains(objetivo) && !botonRef.current?.contains(objetivo)) setAbierto(false);
    };
    const desplazo = (e: Event) => { if (!panelRef.current?.contains(e.target as Node)) setAbierto(false); };
    // En el celular, al abrirse el teclado cambia el alto de la ventana: eso no la cierra, solo un cambio de ancho (girar el aparato).
    const anchoAlAbrir = window.innerWidth;
    const cambioElTamano = () => { if (window.innerWidth !== anchoAlAbrir) setAbierto(false); };
    document.addEventListener('mousedown', clicAfuera);
    window.addEventListener('scroll', desplazo, true);
    window.addEventListener('resize', cambioElTamano);
    return () => {
      document.removeEventListener('mousedown', clicAfuera);
      window.removeEventListener('scroll', desplazo, true);
      window.removeEventListener('resize', cambioElTamano);
    };
  }, [abierto]);

  // La opción activa siempre a la vista, también al recorrer con las flechas.
  useEffect(() => {
    if (abierto) document.getElementById(`${id}-op-${indice}`)?.scrollIntoView({ block: 'nearest' });
  }, [abierto, indice, id]);

  const teclaEnElBuscador = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); cerrar(true); return; }   // no cierra también el diálogo
    if (e.key === 'Tab') { cerrar(true); return; }   // el foco vuelve al botón y el Tab sigue desde ahí: al número, o al campo anterior con Mayús
    if (e.key === 'ArrowDown') { e.preventDefault(); setActivo(lista.length ? (indice + 1) % lista.length : 0); return; }
    if (e.key === 'ArrowUp') { e.preventDefault(); setActivo(lista.length ? (indice - 1 + lista.length) % lista.length : 0); return; }
    if (e.key === 'Home') { e.preventDefault(); setActivo(0); return; }
    if (e.key === 'End') { e.preventDefault(); setActivo(Math.max(lista.length - 1, 0)); return; }
    if (e.key === 'Enter') { e.preventDefault(); e.stopPropagation(); if (lista[indice]) elegir(lista[indice].codigo); }
  };

  return (
    <>
      <button ref={botonRef} type="button" disabled={disabled} onClick={() => (abierto ? cerrar(false) : abrir())}
        onKeyDown={(e) => { if (e.key === 'ArrowDown' && !abierto) { e.preventDefault(); abrir(); } }}
        aria-haspopup="listbox" aria-expanded={abierto} aria-controls={abierto ? idLista : undefined}
        aria-label={`País del ${dato}: ${elegido?.nombre ?? 'sin elegir'}, código +${elegido?.prefijo ?? ''}. Cambiar`}
        className="flex shrink-0 items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-2 py-2 text-sm text-slate-700 transition-colors hover:bg-slate-50 focus:border-marca-500 focus:outline-none focus:ring-2 focus:ring-marca-500 disabled:cursor-not-allowed disabled:opacity-60">
        <Bandera codigo={pais} />
        <span className="tabular-nums">+{elegido?.prefijo}</span>
        <ChevronDown className="h-3.5 w-3.5 text-slate-400" aria-hidden="true" />
      </button>

      {abierto && posicion && createPortal(
        <div ref={panelRef} style={{ position: 'fixed', top: posicion.top, bottom: posicion.bottom, left: posicion.left, width: posicion.width, maxHeight: posicion.maxHeight }}
          className="z-[80] flex flex-col overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xl">
          <div className="flex items-center gap-2 border-b border-slate-200 px-3 py-2">
            <Search className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />
            <input autoFocus type="text" value={texto} autoComplete="off" spellCheck={false}
              onChange={(e) => { setTexto(e.target.value); setActivo(0); }} onKeyDown={teclaEnElBuscador}
              role="combobox" aria-expanded="true" aria-controls={idLista} aria-autocomplete="list" aria-activedescendant={lista.length ? idOpcion(indice) : undefined}
              aria-label={`Buscar el país del ${dato}`} placeholder="Buscar país o código"
              className="w-full bg-transparent text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none" />
          </div>
          <ul id={idLista} role="listbox" aria-label={`Países para el ${dato}`} className="min-h-0 flex-1 overflow-y-auto py-1">
            {lista.length === 0 && <li className="px-3 py-3 text-sm text-slate-500">Ningún país coincide con lo que escribiste.</li>}
            {lista.map((p, n) => (
              <li key={p.codigo} id={idOpcion(n)} role="option" aria-selected={p.codigo === pais}
                onMouseDown={(e) => e.preventDefault()} onClick={() => elegir(p.codigo)} onMouseMove={() => { if (n !== indice) setActivo(n); }}
                className={`flex cursor-pointer items-center gap-3 px-3 py-1.5 text-sm ${n === indice ? 'bg-marca-50 text-slate-900' : 'text-slate-700'} ${p.codigo === pais ? 'font-semibold' : ''}`}>
                <Bandera codigo={p.codigo} />
                <span className="min-w-0 flex-1 truncate">{p.nombre}</span>
                <span className="shrink-0 tabular-nums text-slate-500">+{p.prefijo}</span>
              </li>
            ))}
          </ul>
        </div>,
        document.body,
      )}
    </>
  );
}
