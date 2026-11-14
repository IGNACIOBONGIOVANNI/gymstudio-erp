import { CheckCircle2, XCircle } from 'lucide-react';
import { REQUISITOS } from './reglasDeClave';

interface Props {
  id: string;
  clave: string;
  /** Si hubo un intento de guardar con la clave inválida: lo que falta se marca en rojo. */
  conError: boolean;
}

/** Lista de requisitos de la contraseña, que se tildan mientras se escribe. */
export function RequisitosDeClave({ id, clave, conError }: Props) {
  const escrita = clave.trim();
  return (
    <ul id={id} className="mt-2 grid grid-cols-1 gap-x-4 gap-y-0.5 text-xs sm:grid-cols-2" aria-label="Requisitos de la contraseña">
      {REQUISITOS.map((r) => {
        const ok = escrita.length > 0 && r.cumple(escrita);
        return (
          <li key={r.id} className={`flex items-center gap-1.5 ${ok ? 'text-emerald-700' : conError ? 'font-medium text-red-600' : 'text-slate-500'}`}>
            {!ok && conError
              ? <XCircle className="h-3.5 w-3.5 shrink-0 text-red-500" aria-hidden="true" />
              : <CheckCircle2 className={`h-3.5 w-3.5 shrink-0 ${ok ? 'text-emerald-600' : 'text-slate-300'}`} aria-hidden="true" />}
            <span>{r.texto}<span className="sr-only">{ok ? ': cumple' : ': falta'}</span></span>
          </li>
        );
      })}
    </ul>
  );
}

/** Aviso bajo el campo «Repetir contraseña»: si coincide con la primera o todavía no. */
export function AvisoDeRepeticion({ clave, repetida }: { clave: string; repetida: string }) {
  if (!repetida) return null;
  const coincide = clave.trim() === repetida.trim();
  return (
    <p className={`mt-1 flex items-center gap-1.5 text-xs ${coincide ? 'text-emerald-700' : 'text-amber-700'}`} aria-live="polite">
      {coincide
        ? <CheckCircle2 className="h-3.5 w-3.5 shrink-0 text-emerald-600" aria-hidden="true" />
        : <XCircle className="h-3.5 w-3.5 shrink-0 text-amber-600" aria-hidden="true" />}
      {coincide ? 'Las dos contraseñas coinciden.' : 'Todavía no coincide con la primera.'}
    </p>
  );
}
