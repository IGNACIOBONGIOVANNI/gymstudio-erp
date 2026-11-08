import { useEffect, useRef } from 'react';

interface Props {
  /** Qué quedaría sin guardar, dicho para completar «Tenés … sin guardar». Por ejemplo: «cambios del calendario». */
  descripcion: string;
  onSeguir: () => void;
  onSalir: () => void;
}

/**
 * Pregunta antes de cambiar de pantalla cuando hay algo sin guardar. «Seguir editando» es lo que se enfoca y lo que hace Escape:
 * salir y perder lo escrito tiene que ser una decisión, no un descuido.
 */
export function ConfirmarSalida({ descripcion, onSeguir, onSalir }: Props) {
  const seguirRef = useRef<HTMLButtonElement>(null);
  const alSeguir = useRef(onSeguir);

  useEffect(() => { alSeguir.current = onSeguir; }, [onSeguir]);

  useEffect(() => {
    seguirRef.current?.focus();
    const tecla = (e: KeyboardEvent) => { if (e.key === 'Escape') alSeguir.current(); };
    document.addEventListener('keydown', tecla);
    return () => document.removeEventListener('keydown', tecla);
  }, []);

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
      onMouseDown={(e) => { if (e.target === e.currentTarget) onSeguir(); }}>
      <div role="alertdialog" aria-modal="true" aria-labelledby="salida-titulo" aria-describedby="salida-texto"
        className="w-full max-w-sm overflow-hidden rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl">
        <h2 id="salida-titulo" className="text-base font-bold text-slate-900">Hay cambios sin guardar</h2>
        <p id="salida-texto" className="mt-2 text-sm text-slate-600">
          Tenés {descripcion} sin guardar. Si salís ahora, se pierden.
        </p>
        <div className="mt-5 flex justify-end gap-2">
          <button ref={seguirRef} type="button" onClick={onSeguir}
            className="rounded-lg bg-marca-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-marca-700">
            Seguir editando
          </button>
          <button type="button" onClick={onSalir}
            className="rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-medium text-red-700 transition-colors hover:bg-red-50">
            Salir sin guardar
          </button>
        </div>
      </div>
    </div>
  );
}
