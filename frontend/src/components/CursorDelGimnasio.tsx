import { useEffect, useRef, useState } from 'react';
import { esAccionable, modoDelCursor, ocultaLaFlecha, pedidosALaApi, posicionDelDibujo, type ModoDelCursor } from '../utils/cursorDelGimnasio';

/**
 * El cursor animado del gimnasio: los navegadores no animan los cursores, así que cuando se puede hacer una acción la flecha
 * se oculta y el cursor pasa a ser un brazo haciendo curl (el clic cae en el codo, que no se mueve), y mientras el sistema
 * carga, un disco de pesas girando (el clic cae en su centro). En lo demás queda la flecha verde. Solo con mouse; con
 * «reducir movimiento» queda quieto (ver index.css).
 */
export function CursorDelGimnasio() {
  const capa = useRef<HTMLDivElement>(null);
  const [modo, setModo] = useState<ModoDelCursor>('normal');
  const [conMouse] = useState(() => typeof window !== 'undefined' && window.matchMedia?.('(pointer: fine)').matches);

  useEffect(() => {
    if (!conMouse) return;
    let x = -100, y = -100, accionable = false, dentro = false, cuadro = 0;
    let modoActual: ModoDelCursor = 'normal';
    let temporizador: number | undefined;

    const dibujar = () => {
      cuadro = 0;
      const p = posicionDelDibujo(modoActual, x, y);
      if (capa.current) capa.current.style.transform = `translate3d(${p.x}px, ${p.y}px, 0)`;
    };
    const actualizarModo = () => {
      const nuevo = dentro ? modoDelCursor({ accionable, cargandoDesde: pedidosALaApi.desde(), ahora: Date.now() }) : 'normal';
      if (nuevo === modoActual) return;
      modoActual = nuevo;
      // La flecha nativa se oculta mientras el brazo o el disco hacen de cursor.
      if (ocultaLaFlecha(nuevo)) document.documentElement.dataset.cursorGimnasio = nuevo;
      else delete document.documentElement.dataset.cursorGimnasio;
      dibujar();
      setModo(nuevo);
    };
    const alMover = (e: PointerEvent) => {
      if (e.pointerType !== 'mouse') return;
      x = e.clientX; y = e.clientY; dentro = true;
      accionable = esAccionable(e.target instanceof Element ? e.target : null);
      actualizarModo();
      if (!cuadro) cuadro = requestAnimationFrame(dibujar);
    };
    const alSalir = (e: MouseEvent) => { if (!e.relatedTarget) { dentro = false; actualizarModo(); } };
    const alPerderFoco = () => { dentro = false; actualizarModo(); };
    // Cuando empieza una carga, el disco aparece a los 200 ms aunque el mouse no se mueva.
    const dejarDeEscuchar = pedidosALaApi.suscribir(() => {
      window.clearTimeout(temporizador);
      actualizarModo();
      if (pedidosALaApi.enCurso() > 0) temporizador = window.setTimeout(actualizarModo, 210);
    });

    window.addEventListener('pointermove', alMover, { passive: true });
    document.addEventListener('mouseout', alSalir);
    window.addEventListener('blur', alPerderFoco);
    return () => {
      window.removeEventListener('pointermove', alMover);
      document.removeEventListener('mouseout', alSalir);
      window.removeEventListener('blur', alPerderFoco);
      dejarDeEscuchar();
      window.clearTimeout(temporizador);
      delete document.documentElement.dataset.cursorGimnasio;
      if (cuadro) cancelAnimationFrame(cuadro);
    };
  }, [conMouse]);

  if (!conMouse) return null;
  return (
    <div ref={capa} aria-hidden="true" className="cursor-gimnasio" data-modo={modo}>
      {modo === 'accion' && <BrazoHaciendoCurl />}
      {modo === 'carga' && <DiscoGirando />}
    </div>
  );
}

function BrazoHaciendoCurl() {
  // Verde claro con borde oscuro (y un halo blanco desde index.css): se distingue sobre fondo blanco, el menú oscuro y los botones verdes.
  return (
    <svg width="44" height="31" viewBox="-14 -2 47 33">
      <g className="curl-biceps">
        <path d="M4.6 29 C3 26 3.6 22.4 7.2 21.8 C10.4 15.6 20.6 13 24.8 17.6 C26.6 19.4 28.6 20 31.6 20.2 L31.6 29.2 Z"
          fill="#47a27c" stroke="#0a231b" strokeWidth="1.3" strokeLinejoin="round" />
        <path d="M14.4 21.4 C16.8 17.8 20.8 17.4 22.8 19.8" fill="none" stroke="#d6ede0" strokeWidth="1.1" strokeLinecap="round" />
      </g>
      <g className="curl-antebrazo">
        <path d="M4.9 25 L5.7 11.6 C5.3 10.6 5.4 9.4 5.6 8.4 C5.6 4.6 12.6 4.6 12.6 8.4 L11.9 11.2 L11.2 25 Z"
          fill="#47a27c" stroke="#0a231b" strokeWidth="1.3" strokeLinejoin="round" />
        <path d="M7.4 9.6 L11.2 9.4" stroke="#0a231b" strokeWidth="0.9" strokeLinecap="round" />
        <rect x="1.2" y="5" width="16" height="1.8" rx="0.9" fill="#d6ede0" stroke="#0a231b" strokeWidth="0.8" />
        <rect x="0.7" y="2.3" width="2.6" height="7.2" rx="0.8" fill="#0a231b" stroke="#fff" strokeWidth="0.8" />
        <rect x="15.1" y="2.3" width="2.6" height="7.2" rx="0.8" fill="#0a231b" stroke="#fff" strokeWidth="0.8" />
      </g>
      <circle cx="8" cy="25" r="3" fill="#47a27c" />
    </svg>
  );
}

function DiscoGirando() {
  return (
    <svg width="40" height="40" viewBox="0 0 32 32" className="disco-girando">
      <circle cx="16" cy="16" r="14" fill="#1f6f54" stroke="#fff" strokeWidth="1.6" />
      <circle cx="16" cy="16" r="9.4" fill="none" stroke="#47a27c" strokeWidth="1.6" />
      <path d="M16 3.6 A12.4 12.4 0 0 1 28.4 16" fill="none" stroke="#f5f9f7" strokeWidth="2.6" strokeLinecap="round" />
      <rect x="14.6" y="22.8" width="2.8" height="4" rx="1" fill="#d6ede0" />
      <circle cx="16" cy="16" r="3.4" fill="#fff" stroke="#123a2e" strokeWidth="0.8" />
    </svg>
  );
}
