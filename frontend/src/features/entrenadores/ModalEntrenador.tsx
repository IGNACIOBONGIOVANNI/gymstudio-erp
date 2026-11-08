import { useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import toast from 'react-hot-toast';
import { guardarEntrenador, quitarFoto as quitarFotoDelServidor, subirFoto } from '../../api/entrenadoresApi';
import { LARGO_MAXIMO_DEL_NOMBRE, limpiarNombre, type Entrenador } from '../calendario/calendarioUtil';
import { AvatarDeEntrenador, CirculoDeIniciales } from './AvatarDeEntrenador';
import { LARGO_MAXIMO_DEL_EMAIL, LARGO_MAXIMO_DEL_WHATSAPP, errorDeEmail, errorDeWhatsApp, limpiarWhatsApp } from './entrenadoresUtil';
import { procesarFoto } from './procesarFoto';

const LARGO_MAXIMO_DEL_TELEFONO = 30;
const LARGO_MAXIMO_DE_LA_ESPECIALIDAD = 60;

interface Props {
  /** Sin entrenador es un alta; con uno, su edición. */
  entrenador?: Entrenador | null;
  onCerrar: () => void;
  onGuardado: () => void;
}

type Campo = 'nombre' | 'telefono' | 'whatsApp' | 'email' | 'especialidad' | 'foto';
type Errores = Partial<Record<Campo, string>>;
const CAMPOS_CON_TEXTO = ['nombre', 'telefono', 'whatsApp', 'email', 'especialidad'] as const;

const campo = 'w-full rounded-lg border px-3 py-2 text-sm focus:outline-none focus:ring-2';
const claseDelCampo = (error?: string) =>
  `${campo} ${error ? 'border-red-400 focus:border-red-400 focus:ring-red-400' : 'border-slate-300 focus:border-marca-500 focus:ring-marca-500'}`;
const botonChico = 'inline-flex items-center rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 transition-colors hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50';

/** A qué campo corresponde un mensaje del servidor. El orden importa: el aviso del email dice «nombre@dominio.com». */
function campoDelMensaje(mensaje: string): Campo | null {
  if (/tel[eé]fono/i.test(mensaje)) return 'telefono';
  if (/whatsapp/i.test(mensaje)) return 'whatsApp';
  if (/e-?mail/i.test(mensaje)) return 'email';
  if (/especialidad/i.test(mensaje)) return 'especialidad';
  if (/nombre/i.test(mensaje)) return 'nombre';
  return null;
}

/** Alta y edición de un entrenador: foto, nombre, teléfono, WhatsApp, email y especialidad (no tiene acceso al sistema). */
export function ModalEntrenador({ entrenador, onCerrar, onGuardado }: Props) {
  // Si los datos se guardaron pero la foto falló, el diálogo sigue abierto y pasa a editar esa ficha: reintentar no la duplica.
  const [idGuardado, setIdGuardado] = useState<number | null>(null);
  const idActual = entrenador?.id ?? idGuardado ?? undefined;
  const esEdicion = idActual !== undefined;

  const [nombre, setNombre] = useState(entrenador?.nombre ?? '');
  const [telefono, setTelefono] = useState(entrenador?.telefono ?? '');
  const [whatsApp, setWhatsApp] = useState(entrenador?.whatsApp ?? '');
  const [email, setEmail] = useState(entrenador?.email ?? '');
  const [especialidad, setEspecialidad] = useState(entrenador?.especialidad ?? '');
  // La foto elegida (ya recortada y achicada) con su enlace de vista previa, o la orden de quitar la que tenía.
  const [fotoNueva, setFotoNueva] = useState<{ blob: Blob; url: string } | null>(null);
  const [quitar, setQuitar] = useState(false);
  const [procesando, setProcesando] = useState(false);
  const [errores, setErrores] = useState<Errores>({});
  const [guardando, setGuardando] = useState(false);
  const nombreRef = useRef<HTMLInputElement>(null);
  const archivoRef = useRef<HTMLInputElement>(null);
  const alCerrar = useRef(onCerrar);
  const urlDeLaVistaPrevia = useRef<string | null>(null);
  const guardandoAhora = useRef(false);
  const montado = useRef(true);

  useEffect(() => { alCerrar.current = onCerrar; }, [onCerrar]);
  useEffect(() => { guardandoAhora.current = guardando; }, [guardando]);

  useEffect(() => {
    nombreRef.current?.focus();
    montado.current = true;
    // Mientras se guarda no se cierra: si la foto falla después de guardar los datos, el motivo tiene que poder leerse.
    const tecla = (e: KeyboardEvent) => { if (e.key === 'Escape' && !guardandoAhora.current) alCerrar.current(); };
    document.addEventListener('keydown', tecla);
    return () => {
      montado.current = false;
      document.removeEventListener('keydown', tecla);
      if (urlDeLaVistaPrevia.current) URL.revokeObjectURL(urlDeLaVistaPrevia.current);
    };
  }, []);

  const tieneFotoGuardada = entrenador?.tieneFoto ?? false;
  const hayFoto = fotoNueva !== null || (tieneFotoGuardada && !quitar);

  const limpiarError = (que: Campo) => setErrores((previos) => (previos[que] ? { ...previos, [que]: undefined } : previos));

  const enfocarPrimero = (nuevos: Errores) => {
    const primero = CAMPOS_CON_TEXTO.find((c) => nuevos[c]);
    if (primero) document.getElementById(`entrenador-${primero.toLowerCase()}`)?.focus();
  };

  const soltarVistaPrevia = () => {
    if (urlDeLaVistaPrevia.current) URL.revokeObjectURL(urlDeLaVistaPrevia.current);
    urlDeLaVistaPrevia.current = null;
  };

  const elegirFoto = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const archivo = e.target.files?.[0];
    e.target.value = '';   // para poder elegir de nuevo el mismo archivo
    if (!archivo) return;
    setProcesando(true);
    limpiarError('foto');
    try {
      const blob = await procesarFoto(archivo);
      if (!montado.current) return;   // se cerró el diálogo mientras se preparaba: no crear un enlace que nadie va a liberar
      soltarVistaPrevia();
      const url = URL.createObjectURL(blob);
      urlDeLaVistaPrevia.current = url;
      setFotoNueva({ blob, url });
      setQuitar(false);
    } catch (error) {
      setErrores((previos) => ({ ...previos, foto: error instanceof Error ? error.message : 'No se pudo preparar la foto.' }));
    } finally {
      setProcesando(false);
    }
  };

  const quitarLaFoto = () => {
    soltarVistaPrevia();
    setFotoNueva(null);
    setQuitar(tieneFotoGuardada);
    limpiarError('foto');
  };

  const enviar = async (e: React.FormEvent) => {
    e.preventDefault();
    const nuevos: Errores = {};
    if (!limpiarNombre(nombre)) nuevos.nombre = 'Ingresá el nombre completo.';
    if (telefono.trim().length > LARGO_MAXIMO_DEL_TELEFONO) nuevos.telefono = `El teléfono no puede tener más de ${LARGO_MAXIMO_DEL_TELEFONO} caracteres.`;
    const errorWhats = errorDeWhatsApp(whatsApp);
    if (errorWhats) nuevos.whatsApp = errorWhats;
    const errorMail = errorDeEmail(email);
    if (errorMail) nuevos.email = errorMail;
    if (especialidad.trim().length > LARGO_MAXIMO_DE_LA_ESPECIALIDAD) nuevos.especialidad = `La especialidad no puede tener más de ${LARGO_MAXIMO_DE_LA_ESPECIALIDAD} caracteres.`;
    setErrores((previos) => ({ ...nuevos, foto: previos.foto }));
    if (Object.keys(nuevos).length > 0) { enfocarPrimero(nuevos); return; }

    setGuardando(true);
    let id: number;
    try {
      id = await guardarEntrenador({
        id: idActual, nombre: limpiarNombre(nombre), telefono: telefono.trim(), especialidad: especialidad.trim(),
        email: email.trim(), whatsApp: limpiarWhatsApp(whatsApp),
        // El valor de la hora todavía no se edita en este diálogo: se manda el que ya tenía la ficha para no borrarlo.
        valorHora: entrenador?.valorHora == null ? '' : String(entrenador.valorHora),
      });
    } catch (error) {
      // Si el servidor rechaza un dato, el mensaje va junto a ese campo; lo demás (conexión, permisos) es un aviso.
      const mensaje = error instanceof Error ? error.message : 'No se pudo guardar el entrenador.';
      const deCampo = campoDelMensaje(mensaje);
      if (deCampo) {
        const conMensaje: Errores = { [deCampo]: mensaje };
        setErrores(conMensaje);
        enfocarPrimero(conMensaje);
      } else toast.error(mensaje);
      setGuardando(false);
      return;
    }

    // Los datos ya están guardados. Si falla la foto, el diálogo sigue abierto para reintentar sin crear la ficha de nuevo.
    setIdGuardado(id);
    try {
      if (fotoNueva) await subirFoto(id, fotoNueva.blob);
      else if (quitar && tieneFotoGuardada) await quitarFotoDelServidor(id);
    } catch (error) {
      const motivo = error instanceof Error ? error.message : 'error desconocido';
      setErrores({ foto: `Se guardaron los datos, pero la foto no se pudo subir: ${motivo}` });
      onGuardado();
      setGuardando(false);
      return;
    }

    toast.success(esEdicion ? `Entrenador «${limpiarNombre(nombre)}» actualizado.` : `Entrenador «${limpiarNombre(nombre)}» cargado.`);
    onGuardado();
    onCerrar();
  };

  const campoDeTexto = (que: Exclude<Campo, 'foto'>) => ({
    id: `entrenador-${que.toLowerCase()}`,
    'aria-invalid': Boolean(errores[que]),
    'aria-describedby': errores[que] ? `error-entrenador-${que.toLowerCase()}` : undefined,
    className: claseDelCampo(errores[que]),
  });
  const mensajeDe = (que: Exclude<Campo, 'foto'>) => errores[que]
    ? <p id={`error-entrenador-${que.toLowerCase()}`} role="alert" className="mt-1 text-xs text-red-600">{errores[que]}</p>
    : null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
      onMouseDown={(e) => { if (e.target === e.currentTarget && !guardando) onCerrar(); }}>
      <div role="dialog" aria-modal="true" aria-labelledby="entrenador-titulo"
        className="flex max-h-[92vh] w-full max-w-lg flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl">
        <div className="flex items-start justify-between gap-4 bg-slate-900 px-6 py-4 text-white">
          <div>
            <h2 id="entrenador-titulo" className="text-base font-bold">{esEdicion ? 'Editar entrenador' : 'Nuevo entrenador'}</h2>
            <p className="mt-0.5 text-xs text-slate-400">Es una ficha para el calendario: no tiene usuario ni contraseña.</p>
          </div>
          <button type="button" onClick={onCerrar} disabled={guardando} aria-label="Cerrar"
            className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-800 hover:text-white disabled:cursor-not-allowed disabled:opacity-50">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form className="space-y-4 overflow-y-auto p-6" noValidate onSubmit={(e) => void enviar(e)}>
          <div>
            <p className="mb-1 block text-xs font-semibold text-slate-600">Foto</p>
            <div className="flex items-center gap-4">
              {fotoNueva
                ? <img src={fotoNueva.url} alt="Vista previa de la foto elegida" width={80} height={80}
                    className="h-20 w-20 shrink-0 rounded-full border border-slate-200 object-cover" />
                : tieneFotoGuardada && !quitar && entrenador
                  ? <AvatarDeEntrenador entrenador={entrenador} tamano={80} />
                  : <CirculoDeIniciales nombre={nombre} tamano={80} />}
              <div className="space-y-2">
                <div className="flex flex-wrap gap-2">
                  <button type="button" onClick={() => archivoRef.current?.click()} disabled={procesando || guardando} className={botonChico}>
                    {procesando ? 'Preparando la foto...' : hayFoto ? 'Cambiar foto' : 'Elegir foto'}
                  </button>
                  {hayFoto && (
                    <button type="button" onClick={quitarLaFoto} disabled={procesando || guardando} className={botonChico}>Quitar foto</button>
                  )}
                </div>
                <p className="text-xs text-slate-500">Se recorta al centro y se achica sola. Sirve una foto JPG, PNG o WebP.</p>
              </div>
            </div>
            <input ref={archivoRef} type="file" accept="image/*" className="hidden" tabIndex={-1} aria-label="Elegir la foto del entrenador"
              onChange={(e) => void elegirFoto(e)} />
            {errores.foto && <p role="alert" className="mt-2 text-xs text-red-600">{errores.foto}</p>}
          </div>

          <div>
            <label htmlFor="entrenador-nombre" className="mb-1 block text-xs font-semibold text-slate-600">Nombre completo *</label>
            <input {...campoDeTexto('nombre')} ref={nombreRef} value={nombre} maxLength={LARGO_MAXIMO_DEL_NOMBRE} autoComplete="off"
              onChange={(e) => { setNombre(e.target.value); limpiarError('nombre'); }} placeholder="Ej: Ana López" />
            {mensajeDe('nombre')}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label htmlFor="entrenador-telefono" className="mb-1 block text-xs font-semibold text-slate-600">Teléfono</label>
              <input {...campoDeTexto('telefono')} value={telefono} maxLength={LARGO_MAXIMO_DEL_TELEFONO + 10} autoComplete="off" inputMode="tel"
                onChange={(e) => { setTelefono(e.target.value); limpiarError('telefono'); }} placeholder="+54 11 1234-5678" />
              {mensajeDe('telefono')}
            </div>
            <div>
              <label htmlFor="entrenador-whatsapp" className="mb-1 block text-xs font-semibold text-slate-600">WhatsApp</label>
              <input {...campoDeTexto('whatsApp')} value={whatsApp} maxLength={LARGO_MAXIMO_DEL_WHATSAPP + 10} autoComplete="off" inputMode="tel"
                onChange={(e) => { setWhatsApp(e.target.value); limpiarError('whatsApp'); }} placeholder="+54 9 11 5555-1234" />
              {mensajeDe('whatsApp')}
            </div>
          </div>

          <div>
            <label htmlFor="entrenador-email" className="mb-1 block text-xs font-semibold text-slate-600">Email</label>
            <input {...campoDeTexto('email')} type="email" value={email} maxLength={LARGO_MAXIMO_DEL_EMAIL + 10} autoComplete="off"
              onChange={(e) => { setEmail(e.target.value); limpiarError('email'); }} placeholder="ana@gimnasio.com" />
            {mensajeDe('email')}
          </div>

          <div>
            <label htmlFor="entrenador-especialidad" className="mb-1 block text-xs font-semibold text-slate-600">Especialidad</label>
            <input {...campoDeTexto('especialidad')} value={especialidad} maxLength={LARGO_MAXIMO_DE_LA_ESPECIALIDAD + 10} autoComplete="off"
              onChange={(e) => { setEspecialidad(e.target.value); limpiarError('especialidad'); }} placeholder="Ej: Musculación, Funcional, Yoga" />
            {mensajeDe('especialidad')}
          </div>

          <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
            <button type="button" onClick={onCerrar} disabled={guardando}
              className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50">Cancelar</button>
            <button type="submit" disabled={guardando || procesando}
              className="rounded-lg bg-marca-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-marca-700 disabled:opacity-50">
              {guardando ? 'Guardando...' : esEdicion ? 'Guardar cambios' : 'Cargar entrenador'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
