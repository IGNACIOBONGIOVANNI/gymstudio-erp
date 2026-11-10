import { useState, useEffect } from 'react';
import { X, User, Mail, Shield, Lock, CheckCircle2, XCircle } from 'lucide-react';
import type { Usuario, RolUsuario, EstadoUsuario } from './types';
import { guardarUsuario } from './usuariosApi';
import toast from 'react-hot-toast';
import { REQUISITOS, claveValida } from './reglasDeClave';
import { CampoDeTelefono } from '../../components/CampoDeTelefono';
import { errorDeNombreDePersona, limpiarNombreDePersona, soloLetrasDelNombre } from '../../utils/nombrePersona';
import { errorDeTelefono, normalizarTelefono } from '../../utils/telefono';

interface ModalUsuarioProps {
  usuarioAEditar?: Usuario | null;
  onClose: () => void;
  onGuardado: () => void;
}

export function ModalUsuario({ usuarioAEditar, onClose, onGuardado }: ModalUsuarioProps) {
  const esEdicion = Boolean(usuarioAEditar);

  const [nombre, setNombre] = useState('');
  const [email, setEmail] = useState('');
  const [rol, setRol] = useState<RolUsuario>('Operador / Recepción');
  const [estado, setEstado] = useState<EstadoUsuario>('Activo');
  const [password, setPassword] = useState('');
  const [telefono, setTelefono] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  // Cada error se muestra junto a su campo, no como aviso flotante.
  const [errores, setErrores] = useState<{ nombre?: string; email?: string; telefono?: string; password?: string }>({});

  const limpiarError = (campo: 'nombre' | 'email' | 'telefono' | 'password') =>
    setErrores((previos) => (previos[campo] ? { ...previos, [campo]: undefined } : previos));

  const claseDelCampo = (error?: string) =>
    `w-full pl-9 pr-3 py-2 text-sm border rounded-lg focus:ring-2 outline-none transition-all ${
      error
        ? 'border-red-400 focus:ring-red-400 focus:border-red-400'
        : 'border-slate-300 focus:ring-indigo-500 focus:border-indigo-500'
    }`;

  useEffect(() => {
    if (usuarioAEditar) {
      setNombre(usuarioAEditar.nombre);
      setEmail(usuarioAEditar.email);
      setRol(usuarioAEditar.rol);
      setEstado(usuarioAEditar.estado);
      setTelefono(normalizarTelefono(usuarioAEditar.telefono));
      setPassword('');
    } else {
      setNombre('');
      setEmail('');
      setRol('Operador / Recepción');
      setEstado('Activo');
      setPassword('');
      setTelefono('');
    }
  }, [usuarioAEditar]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const nuevos: { nombre?: string; email?: string; telefono?: string; password?: string } = {};
    if (!nombre.trim()) nuevos.nombre = 'Ingresá el nombre completo.';
    else nuevos.nombre = errorDeNombreDePersona(nombre, 'El nombre') ?? undefined;
    nuevos.telefono = errorDeTelefono(telefono, 'El teléfono') ?? undefined;
    if (!email.trim() || !email.includes('@')) nuevos.email = 'Ingresá un email válido, por ejemplo nombre@gymstudio.com.';
    // Política de contraseñas (la exige también el servidor): 8+ caracteres, mayúscula, número, especial y sin números consecutivos.
    const clave = password.trim();
    if (!esEdicion && !clave) nuevos.password = 'Ingresá una contraseña inicial.';
    else if (clave && !claveValida(clave)) nuevos.password = 'La contraseña no cumple los requisitos de la lista.';
    setErrores(nuevos);
    const primero = (['nombre', 'email', 'telefono', 'password'] as const).find((c) => nuevos[c]);
    if (primero) {
      document.getElementById(`usuario-${primero}`)?.focus();
      return;
    }

    try {
      setIsSubmitting(true);
      await guardarUsuario({
        id: usuarioAEditar?.id,
        nombre: limpiarNombreDePersona(nombre),
        email,
        rol,
        estado,
        telefono,
        password: password.trim() ? password.trim() : undefined,
      });

      toast.success(
        esEdicion
          ? `Usuario "${nombre}" actualizado correctamente`
          : `Usuario "${nombre}" creado exitosamente`
      );
      onGuardado();
      onClose();
    } catch (error: any) {
      // Si el servidor rechaza un dato, el mensaje va junto a ese campo; lo demás (conexión, permisos) sí es un aviso.
      const mensaje: string = error?.message || 'Error al guardar el usuario';
      if (/contraseña/i.test(mensaje)) setErrores({ password: mensaje });
      else if (/email|correo/i.test(mensaje)) setErrores({ email: mensaje });
      else if (/tel[eé]fono/i.test(mensaje)) setErrores({ telefono: mensaje });
      else if (/nombre/i.test(mensaje)) setErrores({ nombre: mensaje });
      else toast.error(mensaje);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm transition-opacity">
      <div className="w-[95%] max-w-lg mx-auto rounded-2xl bg-white shadow-2xl border border-slate-200 overflow-hidden animate-in fade-in zoom-in-95 duration-150">

        {/* Encabezado */}
        <div className="bg-slate-900 px-6 py-4 flex items-center justify-between text-white">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-slate-800 rounded-lg text-indigo-400">
              <User className="h-5 w-5" />
            </div>
            <div>
              <h3 className="font-bold text-base sm:text-lg">
                {esEdicion ? 'Editar Usuario' : 'Nuevo Usuario'}
              </h3>
              <p className="text-xs text-slate-400">
                {esEdicion
                  ? 'Modificá los permisos y datos del usuario'
                  : 'Completá los datos para registrar un nuevo acceso'}
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="text-slate-400 hover:text-white rounded-lg p-1.5 hover:bg-slate-800 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Formulario */}
        <form onSubmit={handleSubmit} noValidate className="p-6 space-y-4">

          {/* Nombre */}
          <div>
            <label htmlFor="usuario-nombre" className="block text-xs font-semibold text-slate-600 mb-1">
              Nombre Completo *
            </label>
            <div className="relative">
              <User className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
              <input
                id="usuario-nombre"
                type="text"
                value={nombre}
                maxLength={150}
                autoComplete="off"
                onChange={(e) => { setNombre(soloLetrasDelNombre(e.target.value)); limpiarError('nombre'); }}
                placeholder="Ej: Lic. Marcos Benítez"
                aria-invalid={Boolean(errores.nombre)}
                aria-describedby={errores.nombre ? 'error-nombre' : undefined}
                className={claseDelCampo(errores.nombre)}
              />
            </div>
            {errores.nombre
              ? <p id="error-nombre" role="alert" className="mt-1 text-xs text-red-600">{errores.nombre}</p>
              : <p className="mt-1 text-xs text-slate-500">Solo letras (también espacios, puntos, apóstrofos y guiones).</p>}
          </div>

          {/* Email / Usuario */}
          <div>
            <label className="block text-xs font-semibold text-slate-600 mb-1">
              Usuario / Correo Electrónico *
            </label>
            <div className="relative">
              <Mail className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
              <input
                id="usuario-email"
                type="email"
                value={email}
                onChange={(e) => { setEmail(e.target.value); limpiarError('email'); }}
                placeholder="ejemplo@gymstudio.com"
                aria-invalid={Boolean(errores.email)}
                aria-describedby={errores.email ? 'error-email' : undefined}
                className={claseDelCampo(errores.email)}
              />
            </div>
            {errores.email && <p id="error-email" role="alert" className="mt-1 text-xs text-red-600">{errores.email}</p>}
          </div>

          {/* Rol y Teléfono */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-slate-600 mb-1">
                Rol en el Sistema *
              </label>
              <div className="relative">
                <Shield className="absolute left-3 top-2.5 h-4 w-4 text-slate-400 pointer-events-none" />
                <select
                  value={rol}
                  onChange={(e) => setRol(e.target.value as RolUsuario)}
                  className="w-full pl-9 pr-3 py-2 text-sm border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none bg-white transition-all appearance-none"
                >
                  <option value="Jefe / Administrador">Jefe / Administrador</option>
                  <option value="Contador">Contador</option>
                  <option value="Operador / Recepción">Operador / Recepción</option>
                </select>
              </div>
            </div>

            <div className="min-w-0">
              <label htmlFor="usuario-telefono" className="block text-xs font-semibold text-slate-600 mb-1">
                Teléfono de Contacto
              </label>
              <CampoDeTelefono id="usuario-telefono" dato="teléfono" value={telefono} invalido={Boolean(errores.telefono)}
                describedBy={errores.telefono ? 'error-telefono' : undefined}
                onChange={(valor) => { setTelefono(valor); limpiarError('telefono'); }} />
              {errores.telefono && <p id="error-telefono" role="alert" className="mt-1 text-xs text-red-600">{errores.telefono}</p>}
            </div>
          </div>

          {/* Contraseña */}
          <div>
            <label className="block text-xs font-semibold text-slate-600 mb-1">
              {esEdicion ? 'Contraseña (opcional)' : 'Contraseña Inicial *'}
            </label>
            <div className="relative">
              <Lock className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
              <input
                id="usuario-password"
                type="password"
                value={password}
                onChange={(e) => { setPassword(e.target.value); limpiarError('password'); }}
                placeholder={esEdicion ? 'Dejar en blanco para no modificar' : '••••••••'}
                className={claseDelCampo(errores.password)}
                autoComplete="new-password"
                aria-invalid={Boolean(errores.password)}
                aria-describedby={errores.password ? 'error-password requisitos-clave' : 'requisitos-clave'}
              />
            </div>
            {errores.password && <p id="error-password" role="alert" className="mt-1 text-xs font-medium text-red-600">{errores.password}</p>}
            {(!esEdicion || password.length > 0) && (
              <ul id="requisitos-clave" className="mt-2 grid grid-cols-1 gap-x-4 gap-y-0.5 text-xs sm:grid-cols-2" aria-label="Requisitos de la contraseña">
                {REQUISITOS.map((r) => {
                  const ok = password.trim().length > 0 && r.cumple(password.trim());
                  return (
                    <li key={r.id} className={`flex items-center gap-1.5 ${ok ? 'text-emerald-700' : errores.password ? 'font-medium text-red-600' : 'text-slate-500'}`}>
                      {!ok && errores.password
                        ? <XCircle className="h-3.5 w-3.5 shrink-0 text-red-500" aria-hidden="true" />
                        : <CheckCircle2 className={`h-3.5 w-3.5 shrink-0 ${ok ? 'text-emerald-600' : 'text-slate-300'}`} aria-hidden="true" />}
                      <span>{r.texto}<span className="sr-only">{ok ? ': cumple' : ': falta'}</span></span>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>

          {/* Estado */}
          <div>
            <label className="block text-xs font-semibold text-slate-600 mb-2">
              Estado de la Cuenta
            </label>
            <div className="flex gap-4">
              <label
                className={`flex-1 flex items-center justify-center gap-2 p-2.5 rounded-lg border cursor-pointer text-sm font-medium transition-all ${
                  estado === 'Activo'
                    ? 'border-emerald-500 bg-emerald-50 text-emerald-800 ring-2 ring-emerald-500/20'
                    : 'border-slate-200 bg-slate-50 text-slate-600 hover:bg-slate-100'
                }`}
              >
                <input
                  type="radio"
                  name="estado"
                  value="Activo"
                  checked={estado === 'Activo'}
                  onChange={() => setEstado('Activo')}
                  className="sr-only"
                />
                <CheckCircle2 className={`h-4 w-4 ${estado === 'Activo' ? 'text-emerald-600' : 'text-slate-400'}`} />
                <span>Activo</span>
              </label>

              <label
                className={`flex-1 flex items-center justify-center gap-2 p-2.5 rounded-lg border cursor-pointer text-sm font-medium transition-all ${
                  estado === 'Inactivo'
                    ? 'border-red-400 bg-red-50 text-red-800 ring-2 ring-red-400/20'
                    : 'border-slate-200 bg-slate-50 text-slate-600 hover:bg-slate-100'
                }`}
              >
                <input
                  type="radio"
                  name="estado"
                  value="Inactivo"
                  checked={estado === 'Inactivo'}
                  onChange={() => setEstado('Inactivo')}
                  className="sr-only"
                />
                <span className={`h-2 w-2 rounded-full ${estado === 'Inactivo' ? 'bg-red-500' : 'bg-slate-400'}`} />
                <span>Inactivo</span>
              </label>
            </div>
            {usuarioAEditar?.esPrincipal && estado === 'Inactivo' && (
              <p className="mt-1 text-xs text-amber-600 font-medium">
                ⚠️ El Administrador Principal no puede desactivarse para no bloquear la sesión.
              </p>
            )}
          </div>

          {/* Botones de acción */}
          <div className="pt-4 border-t border-slate-100 flex items-center justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm font-medium text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 transition-colors"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className="inline-flex items-center gap-2 px-5 py-2 text-sm font-semibold text-white bg-marca-600 rounded-lg hover:bg-marca-700 shadow-sm transition-all active:scale-95 disabled:opacity-50"
            >
              {esEdicion ? 'Guardar Cambios' : 'Crear Usuario'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
