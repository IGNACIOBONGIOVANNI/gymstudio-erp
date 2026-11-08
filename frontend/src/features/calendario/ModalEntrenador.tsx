import { useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import toast from 'react-hot-toast';
import { guardarEntrenador } from '../../api/entrenadoresApi';
import { LARGO_MAXIMO_DEL_NOMBRE, limpiarNombre, type Entrenador } from './calendarioUtil';

const LARGO_MAXIMO_DEL_TELEFONO = 30;
const LARGO_MAXIMO_DE_LA_ESPECIALIDAD = 60;

interface Props {
  /** Sin entrenador es un alta; con uno, su edición. */
  entrenador?: Entrenador | null;
  onCerrar: () => void;
  onGuardado: () => void;
}

type Campo = 'nombre' | 'telefono' | 'especialidad';
type Errores = Partial<Record<Campo, string>>;

const campo = 'w-full rounded-lg border px-3 py-2 text-sm focus:outline-none focus:ring-2';
const claseDelCampo = (error?: string) =>
  `${campo} ${error ? 'border-red-400 focus:border-red-400 focus:ring-red-400' : 'border-slate-300 focus:border-marca-500 focus:ring-marca-500'}`;

/** Alta y edición de un entrenador: una ficha con nombre, teléfono y especialidad (no tiene acceso al sistema). */
export function ModalEntrenador({ entrenador, onCerrar, onGuardado }: Props) {
  const esEdicion = Boolean(entrenador);
  const [nombre, setNombre] = useState(entrenador?.nombre ?? '');
  const [telefono, setTelefono] = useState(entrenador?.telefono ?? '');
  const [especialidad, setEspecialidad] = useState(entrenador?.especialidad ?? '');
  const [errores, setErrores] = useState<Errores>({});
  const [guardando, setGuardando] = useState(false);
  const nombreRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    nombreRef.current?.focus();
    const tecla = (e: KeyboardEvent) => { if (e.key === 'Escape') onCerrar(); };
    document.addEventListener('keydown', tecla);
    return () => document.removeEventListener('keydown', tecla);
  }, [onCerrar]);

  const limpiarError = (que: Campo) => setErrores((previos) => (previos[que] ? { ...previos, [que]: undefined } : previos));

  const enfocarPrimero = (nuevos: Errores) => {
    const primero = (['nombre', 'telefono', 'especialidad'] as const).find((c) => nuevos[c]);
    if (primero) document.getElementById(`entrenador-${primero}`)?.focus();
  };

  const enviar = async (e: React.FormEvent) => {
    e.preventDefault();
    const nuevos: Errores = {};
    if (!limpiarNombre(nombre)) nuevos.nombre = 'Ingresá el nombre completo.';
    if (telefono.trim().length > LARGO_MAXIMO_DEL_TELEFONO) nuevos.telefono = `El teléfono no puede tener más de ${LARGO_MAXIMO_DEL_TELEFONO} caracteres.`;
    if (especialidad.trim().length > LARGO_MAXIMO_DE_LA_ESPECIALIDAD) nuevos.especialidad = `La especialidad no puede tener más de ${LARGO_MAXIMO_DE_LA_ESPECIALIDAD} caracteres.`;
    setErrores(nuevos);
    if (Object.keys(nuevos).length > 0) { enfocarPrimero(nuevos); return; }

    setGuardando(true);
    try {
      // El email y el WhatsApp todavía no se editan en este diálogo: se mandan los que ya tenía la ficha para no borrarlos.
      await guardarEntrenador({
        id: entrenador?.id, nombre: limpiarNombre(nombre), telefono: telefono.trim(), especialidad: especialidad.trim(),
        email: entrenador?.email ?? '', whatsApp: entrenador?.whatsApp ?? '',
      });
      toast.success(esEdicion ? `Entrenador «${limpiarNombre(nombre)}» actualizado.` : `Entrenador «${limpiarNombre(nombre)}» cargado.`);
      onGuardado();
      onCerrar();
    } catch (error) {
      // Si el servidor rechaza un dato, el mensaje va junto a ese campo; lo demás (conexión, permisos) es un aviso.
      const mensaje = error instanceof Error ? error.message : 'No se pudo guardar el entrenador.';
      const deCampo: Errores = /tel[eé]fono/i.test(mensaje) ? { telefono: mensaje }
        : /especialidad/i.test(mensaje) ? { especialidad: mensaje }
        : /nombre/i.test(mensaje) ? { nombre: mensaje }
        : {};
      if (Object.keys(deCampo).length > 0) { setErrores(deCampo); enfocarPrimero(deCampo); } else toast.error(mensaje);
    } finally {
      setGuardando(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
      onMouseDown={(e) => { if (e.target === e.currentTarget) onCerrar(); }}>
      <div role="dialog" aria-modal="true" aria-labelledby="entrenador-titulo"
        className="w-full max-w-md overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl">
        <div className="flex items-start justify-between gap-4 bg-slate-900 px-6 py-4 text-white">
          <div>
            <h2 id="entrenador-titulo" className="text-base font-bold">{esEdicion ? 'Editar entrenador' : 'Nuevo entrenador'}</h2>
            <p className="mt-0.5 text-xs text-slate-400">Es una ficha para el calendario: no tiene usuario ni contraseña.</p>
          </div>
          <button type="button" onClick={onCerrar} aria-label="Cerrar" className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-800 hover:text-white">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form className="space-y-4 p-6" noValidate onSubmit={(e) => void enviar(e)}>
          <div>
            <label htmlFor="entrenador-nombre" className="mb-1 block text-xs font-semibold text-slate-600">Nombre completo *</label>
            <input id="entrenador-nombre" ref={nombreRef} value={nombre} maxLength={LARGO_MAXIMO_DEL_NOMBRE} autoComplete="off"
              onChange={(e) => { setNombre(e.target.value); limpiarError('nombre'); }} placeholder="Ej: Ana López"
              aria-invalid={Boolean(errores.nombre)} aria-describedby={errores.nombre ? 'error-entrenador-nombre' : undefined}
              className={claseDelCampo(errores.nombre)} />
            {errores.nombre && <p id="error-entrenador-nombre" role="alert" className="mt-1 text-xs text-red-600">{errores.nombre}</p>}
          </div>

          <div>
            <label htmlFor="entrenador-telefono" className="mb-1 block text-xs font-semibold text-slate-600">Teléfono</label>
            <input id="entrenador-telefono" value={telefono} maxLength={LARGO_MAXIMO_DEL_TELEFONO + 10} autoComplete="off" inputMode="tel"
              onChange={(e) => { setTelefono(e.target.value); limpiarError('telefono'); }} placeholder="+54 11 1234-5678"
              aria-invalid={Boolean(errores.telefono)} aria-describedby={errores.telefono ? 'error-entrenador-telefono' : undefined}
              className={claseDelCampo(errores.telefono)} />
            {errores.telefono && <p id="error-entrenador-telefono" role="alert" className="mt-1 text-xs text-red-600">{errores.telefono}</p>}
          </div>

          <div>
            <label htmlFor="entrenador-especialidad" className="mb-1 block text-xs font-semibold text-slate-600">Especialidad</label>
            <input id="entrenador-especialidad" value={especialidad} maxLength={LARGO_MAXIMO_DE_LA_ESPECIALIDAD + 10} autoComplete="off"
              onChange={(e) => { setEspecialidad(e.target.value); limpiarError('especialidad'); }} placeholder="Ej: Musculación, Funcional, Yoga"
              aria-invalid={Boolean(errores.especialidad)} aria-describedby={errores.especialidad ? 'error-entrenador-especialidad' : undefined}
              className={claseDelCampo(errores.especialidad)} />
            {errores.especialidad && <p id="error-entrenador-especialidad" role="alert" className="mt-1 text-xs text-red-600">{errores.especialidad}</p>}
          </div>

          <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
            <button type="button" onClick={onCerrar}
              className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50">Cancelar</button>
            <button type="submit" disabled={guardando}
              className="rounded-lg bg-marca-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-marca-700 disabled:opacity-50">
              {guardando ? 'Guardando...' : esEdicion ? 'Guardar cambios' : 'Cargar entrenador'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
