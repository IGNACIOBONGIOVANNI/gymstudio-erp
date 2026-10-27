import { useState, useEffect } from 'react';
import { X, User, Mail, Shield, Lock, Phone, CheckCircle2 } from 'lucide-react';
import type { Usuario, RolUsuario, EstadoUsuario } from './types';
import { guardarUsuario } from './usuariosApi';
import toast from 'react-hot-toast';
import { obtenerConfiguracion } from '../configuracion/configuracionStorage';

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

  useEffect(() => {
    if (usuarioAEditar) {
      setNombre(usuarioAEditar.nombre);
      setEmail(usuarioAEditar.email);
      setRol(usuarioAEditar.rol);
      setEstado(usuarioAEditar.estado);
      setTelefono(usuarioAEditar.telefono || '');
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

    if (!nombre.trim()) {
      toast.error('Por favor ingresá el nombre completo');
      return;
    }

    if (!email.trim() || !email.includes('@')) {
      toast.error('Por favor ingresá un email válido');
      return;
    }

    if (!esEdicion && !password.trim()) {
      toast.error('Ingresá una contraseña inicial para el usuario');
      return;
    }
    // Configuración > Seguridad: contraseñas seguras (8+ caracteres con letras y números).
    const clave = password.trim();
    if (clave && obtenerConfiguracion().exigirPasswordSegura && !(clave.length >= 8 && /[A-Za-zÁÉÍÓÚáéíóúÑñ]/.test(clave) && /\d/.test(clave))) {
      toast.error('La contraseña debe tener al menos 8 caracteres, con letras y números (se exige en Configuración > Seguridad)');
      return;
    }

    try {
      setIsSubmitting(true);
      await guardarUsuario({
        id: usuarioAEditar?.id,
        nombre,
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
      toast.error(error?.message || 'Error al guardar el usuario');
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
        <form onSubmit={handleSubmit} className="p-6 space-y-4">

          {/* Nombre */}
          <div>
            <label className="block text-xs font-semibold text-slate-600 mb-1">
              Nombre Completo *
            </label>
            <div className="relative">
              <User className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
              <input
                type="text"
                required
                value={nombre}
                onChange={(e) => setNombre(e.target.value)}
                placeholder="Ej: Lic. Marcos Benítez"
                className="w-full pl-9 pr-3 py-2 text-sm border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
              />
            </div>
          </div>

          {/* Email / Usuario */}
          <div>
            <label className="block text-xs font-semibold text-slate-600 mb-1">
              Usuario / Correo Electrónico *
            </label>
            <div className="relative">
              <Mail className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="ejemplo@gymstudio.com"
                className="w-full pl-9 pr-3 py-2 text-sm border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
              />
            </div>
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

            <div>
              <label className="block text-xs font-semibold text-slate-600 mb-1">
                Teléfono de Contacto
              </label>
              <div className="relative">
                <Phone className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
                <input
                  type="text"
                  value={telefono}
                  onChange={(e) => setTelefono(e.target.value)}
                  placeholder="+54 11 1234-5678"
                  className="w-full pl-9 pr-3 py-2 text-sm border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
                />
              </div>
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
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder={esEdicion ? 'Dejar en blanco para no modificar' : '••••••••'}
                className="w-full pl-9 pr-3 py-2 text-sm border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
              />
            </div>
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
