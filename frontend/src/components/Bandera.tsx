import type { ComponentType, SVGProps } from 'react';
import * as Banderas from 'country-flag-icons/react/3x2';

// Las banderas son imágenes SVG que vienen con la aplicación (no se bajan de internet) y se dibujan igual en todos los sistemas:
// el emoji de bandera no se ve en Windows, donde aparece como las dos letras del país.
const TODAS = Banderas as unknown as Record<string, ComponentType<SVGProps<SVGSVGElement>>>;

/** La bandera de un país por su código ISO («AR»). Es decorativa: el nombre del país siempre está escrito al lado. */
export function Bandera({ codigo, className = 'h-4 w-6' }: { codigo: string; className?: string }) {
  const Dibujo = TODAS[codigo];
  const base = `${className} shrink-0 rounded-[2px] shadow-[0_0_0_1px_rgba(15,23,42,0.15)]`;
  return Dibujo
    ? <Dibujo aria-hidden="true" focusable="false" className={base} />
    : <span aria-hidden="true" className={`${base} inline-block bg-slate-200`} />;
}
