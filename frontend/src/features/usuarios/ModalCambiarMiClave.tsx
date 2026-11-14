import { useState } from 'react';
import { KeyRound, Lock, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { claveValida } from './reglasDeClave';
import { AvisoDeRepeticion, RequisitosDeClave } from './RequisitosDeClave';
import { mensajeDeError, ERROR_DE_CONEXION } from '../../api/errores';

interface Props {
  onClose: () => void;
}

type Campo = 'actual' | 'nueva' | 'repetida';

/**
 * Cualquier usuario cambia su propia contraseña: da la actual y escribe la nueva dos veces. (El Administrador puede
 * restablecer la de otro desde Usuarios sin saber la actual.)
 */
export function ModalCambiarMiClave({ onClose }: Props) {
  const [actual, setActual] = useState('');
  const [nueva, setNueva] = useState('');
  const [repetida, setRepetida] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [errores, setErrores] = useState<Partial<Record<Campo, string>>>({});

  const limpiar = (campo: Campo) => setErrores((e) => (e[campo] ? { ...e, [campo]: undefined } : e));
  const clase = (error?: string) =>
    `w-full rounded-lg border py-2 pl-9 pr-3 text-sm outline-none transition-all focus:ring-2 ${
      error ? 'border-red-400 focus:border-red-400 focus:ring-red-400' : 'border-slate-300 focus:border-marca-600 focus:ring-marca-600'
    }`;

  const guardar = async (e: React.FormEvent) => {
    e.preventDefault();
    const nuevos: Partial<Record<Campo, string>> = {};
    if (!actual) nuevos.actual = 'Escribí tu contraseña actual.';
    if (!claveValida(nueva.trim())) nuevos.nueva = 'La contraseña nueva no cumple los requisitos de la lista.';
    else if (nueva.trim() === actual) nuevos.nueva = 'La contraseña nueva tiene que ser distinta de la actual.';
    else if (repetida.trim() !== nueva.trim()) nuevos.repetida = repetida ? 'Las dos contraseñas nuevas no coinciden.' : 'Repetí la contraseña nueva para confirmarla.';
    setErrores(nuevos);
    const primero = (['actual', 'nueva', 'repetida'] as const).find((c) => nuevos[c]);
    if (primero) {
      document.getElementById(`mi-clave-${primero}`)?.focus();
      return;
    }

    setEnviando(true);
    try {
      const res = await fetch('/api/auth/mi-clave', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ claveActual: actual, claveNueva: nueva.trim(), confirmacionClave: repetida.trim() }),
      });
      if (res.ok) {
        toast.success('Tu contraseña se cambió. La próxima vez ingresá con la nueva.');
        onClose();
        return;
      }
      const mensaje = await mensajeDeError(res, 'No se pudo cambiar la contraseña.');
      if (/actual/i.test(mensaje)) setErrores({ actual: mensaje });
      else if (/coinciden/i.test(mensaje)) setErrores({ repetida: mensaje });
      else if (/pol[ií]tica|distinta/i.test(mensaje)) setErrores({ nueva: mensaje });
      else toast.error(mensaje);
    } catch {
      toast.error(ERROR_DE_CONEXION);
    } finally {
      setEnviando(false);
    }
  };

  const error = (campo: Campo) =>
    errores[campo] ? <p id={`error-mi-clave-${campo}`} role="alert" className="mt-1 text-xs font-medium text-red-600">{errores[campo]}</p> : null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 p-4 backdrop-blur-sm" role="dialog" aria-modal="true" aria-labelledby="titulo-mi-clave">
      <div className="w-[95%] max-w-md overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl">
        <div className="flex items-center justify-between bg-slate-900 px-6 py-4 text-white">
          <div className="flex items-center gap-3">
            <div className="rounded-lg bg-slate-800 p-2 text-marca-400"><KeyRound className="h-5 w-5" aria-hidden="true" /></div>
            <div>
              <h3 id="titulo-mi-clave" className="text-base font-bold sm:text-lg">Cambiar mi contraseña</h3>
              <p className="text-xs text-slate-400">Pedimos la actual para confirmar que sos vos.</p>
            </div>
          </div>
          <button onClick={onClose} className="rounded-lg p-1.5 text-slate-400 transition-colors hover:bg-slate-800 hover:text-white" aria-label="Cerrar">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={guardar} noValidate className="space-y-4 p-6">
          <div>
            <label htmlFor="mi-clave-actual" className="mb-1 block text-xs font-semibold text-slate-600">Contraseña actual *</label>
            <div className="relative">
              <Lock className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" aria-hidden="true" />
              <input id="mi-clave-actual" type="password" autoComplete="current-password" autoFocus value={actual} disabled={enviando}
                onChange={(e) => { setActual(e.target.value); limpiar('actual'); }} className={clase(errores.actual)}
                aria-invalid={Boolean(errores.actual)} aria-describedby={errores.actual ? 'error-mi-clave-actual' : undefined} />
            </div>
            {error('actual')}
          </div>

          <div>
            <label htmlFor="mi-clave-nueva" className="mb-1 block text-xs font-semibold text-slate-600">Contraseña nueva *</label>
            <div className="relative">
              <Lock className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" aria-hidden="true" />
              <input id="mi-clave-nueva" type="password" autoComplete="new-password" value={nueva} disabled={enviando}
                onChange={(e) => { setNueva(e.target.value); limpiar('nueva'); }} className={clase(errores.nueva)}
                aria-invalid={Boolean(errores.nueva)}
                aria-describedby={errores.nueva ? 'error-mi-clave-nueva requisitos-mi-clave' : 'requisitos-mi-clave'} />
            </div>
            {error('nueva')}
            <RequisitosDeClave id="requisitos-mi-clave" clave={nueva} conError={Boolean(errores.nueva)} />
          </div>

          <div>
            <label htmlFor="mi-clave-repetida" className="mb-1 block text-xs font-semibold text-slate-600">Repetir contraseña nueva *</label>
            <div className="relative">
              <Lock className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" aria-hidden="true" />
              <input id="mi-clave-repetida" type="password" autoComplete="new-password" value={repetida} disabled={enviando}
                onChange={(e) => { setRepetida(e.target.value); limpiar('repetida'); }} className={clase(errores.repetida)}
                aria-invalid={Boolean(errores.repetida)} aria-describedby={errores.repetida ? 'error-mi-clave-repetida' : undefined} />
            </div>
            {errores.repetida ? error('repetida') : <AvisoDeRepeticion clave={nueva} repetida={repetida} />}
          </div>

          <div className="flex items-center justify-end gap-3 border-t border-slate-100 pt-4">
            <button type="button" onClick={onClose} disabled={enviando}
              className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50 disabled:opacity-50">
              Cancelar
            </button>
            <button type="submit" disabled={enviando}
              className="rounded-lg bg-marca-600 px-5 py-2 text-sm font-semibold text-white shadow-sm transition-all hover:bg-marca-700 disabled:opacity-50">
              {enviando ? 'Guardando…' : 'Cambiar contraseña'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
