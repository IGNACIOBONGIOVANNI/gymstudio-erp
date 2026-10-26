import { useState, useMemo, useEffect } from 'react';
import {
  Users, Search, Plus, Edit2, Trash2, ShieldCheck,
  CheckCircle2, Clock, AlertTriangle
} from 'lucide-react';
import type { Usuario, RolUsuario } from './types';
import { obtenerUsuarios, cambiarEstadoUsuario, eliminarUsuario } from './usuariosApi';
import { ModalUsuario } from './ModalUsuario';
import toast from 'react-hot-toast';

export default function ModuloUsuarios() {
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [busqueda, setBusqueda] = useState('');
  const [filtroRol, setFiltroRol] = useState<string>('todos');
  const [filtroEstado, setFiltroEstado] = useState<string>('todos');

  // Modales
  const [modalAbierto, setModalAbierto] = useState(false);
  const [usuarioEditando, setUsuarioEditando] = useState<Usuario | null>(null);
  const [usuarioAEliminar, setUsuarioAEliminar] = useState<Usuario | null>(null);

  const cargarUsuarios = async () => {
    try {
      setUsuarios(await obtenerUsuarios());
    } catch (error: any) {
      toast.error(error?.message || 'No se pudieron cargar los usuarios');
    }
  };

  useEffect(() => {
    cargarUsuarios();
  }, []);

  // Filtrado reactivo
  const usuariosFiltrados = useMemo(() => {
    return usuarios.filter((usr) => {
      const matchTexto =
        usr.nombre.toLowerCase().includes(busqueda.toLowerCase()) ||
        usr.email.toLowerCase().includes(busqueda.toLowerCase());

      const matchRol =
        filtroRol === 'todos' || usr.rol === filtroRol;

      const matchEstado =
        filtroEstado === 'todos' || usr.estado === filtroEstado;

      return matchTexto && matchRol && matchEstado;
    });
  }, [usuarios, busqueda, filtroRol, filtroEstado]);

  // Contadores para KPIs
  const stats = useMemo(() => {
    const total = usuarios.length;
    const activos = usuarios.filter((u) => u.estado === 'Activo').length;
    const jefes = usuarios.filter((u) => u.rol.includes('Jefe')).length;
    const contadores = usuarios.filter((u) => u.rol === 'Contador').length;
    return { total, activos, jefes, contadores };
  }, [usuarios]);

  const handleToggleEstado = async (usuario: Usuario) => {
    if (usuario.esPrincipal && usuario.estado === 'Activo') {
      toast.error('No se puede desactivar al Administrador Principal');
      return;
    }

    try {
      await cambiarEstadoUsuario(usuario);
      await cargarUsuarios();
      const nuevoEstado = usuario.estado === 'Activo' ? 'Inactivo' : 'Activo';
      toast.success(`Usuario ${usuario.nombre} ahora está ${nuevoEstado}`);
    } catch (error: any) {
      toast.error(error?.message || 'Error al modificar estado');
    }
  };

  const confirmarEliminar = async () => {
    if (!usuarioAEliminar) return;

    try {
      await eliminarUsuario(usuarioAEliminar.id);
      toast.success('Usuario eliminado exitosamente');
      await cargarUsuarios();
      setUsuarioAEliminar(null);
    } catch (error: any) {
      toast.error(error?.message || 'Error al eliminar el usuario');
    }
  };

  const getRolBadgeClass = (rol: RolUsuario) => {
    switch (rol) {
      case 'Jefe / Administrador':
        return 'bg-purple-50 text-purple-700 border-purple-200 ring-purple-600/20';
      case 'Contador':
        return 'bg-blue-50 text-blue-700 border-blue-200 ring-blue-600/20';
      case 'Operador / Recepción':
        return 'bg-emerald-50 text-emerald-700 border-emerald-200 ring-emerald-600/20';
      default:
        return 'bg-slate-50 text-slate-700 border-slate-200 ring-slate-600/20';
    }
  };

  return (
    <div className="space-y-6">

      {/* Encabezado de la Sección */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-200 pb-5">
        <div>
          <div className="flex items-center gap-2.5">
            <div className="p-2 bg-indigo-50 text-indigo-600 rounded-lg">
              <Users className="h-6 w-6" />
            </div>
            <div>
              
              <p className="text-xs sm:text-sm text-slate-500">
                Quién puede entrar al sistema y con qué rol.
              </p>
            </div>
          </div>
        </div>

        <div className="flex items-center gap-2.5 shrink-0">
          <button
            onClick={() => {
              setUsuarioEditando(null);
              setModalAbierto(true);
            }}
            className="inline-flex items-center gap-2 rounded-lg bg-marca-600 px-4 py-2 min-h-[40px] text-sm font-semibold text-white shadow-sm transition-all hover:bg-marca-700 active:scale-95 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2"
          >
            <Plus className="h-4 w-4" />
            <span>Nuevo Usuario</span>
          </button>
        </div>
      </div>

      {/* Tarjetas KPI de Resumen */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-white rounded-xl border border-slate-200/80 p-4 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500 ">Total Usuarios</p>
            <h4 className="text-2xl font-bold text-slate-900 tabular-nums mt-1">{stats.total}</h4>
          </div>
          <div className="h-10 w-10 bg-slate-100 rounded-lg flex items-center justify-center text-slate-600">
            <Users className="h-5 w-5" />
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200/80 p-4 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500 ">Activos</p>
            <h4 className="text-2xl font-bold text-emerald-600 tabular-nums mt-1">{stats.activos}</h4>
          </div>
          <div className="h-10 w-10 bg-emerald-50 rounded-lg flex items-center justify-center text-emerald-600">
            <CheckCircle2 className="h-5 w-5" />
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200/80 p-4 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500 ">Administradores</p>
            <h4 className="text-2xl font-bold text-purple-600 tabular-nums mt-1">{stats.jefes}</h4>
          </div>
          <div className="h-10 w-10 bg-purple-50 rounded-lg flex items-center justify-center text-purple-600">
            <ShieldCheck className="h-5 w-5" />
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200/80 p-4 shadow-sm flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500 ">Contadores</p>
            <h4 className="text-2xl font-bold text-blue-600 tabular-nums mt-1">{stats.contadores}</h4>
          </div>
          <div className="h-10 w-10 bg-blue-50 rounded-lg flex items-center justify-center text-blue-600">
            <ShieldCheck className="h-5 w-5" />
          </div>
        </div>
      </div>

      {/* Barra de Búsqueda y Filtros */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 bg-white p-4 rounded-xl border border-slate-200/80 shadow-sm">
        <div className="flex-1 relative">
          <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
          <input
            type="text"
            value={busqueda}
            onChange={(e) => setBusqueda(e.target.value)}
            placeholder="Buscar por nombre, email o cargo..."
            className="w-full pl-9 pr-3 py-1.5 text-sm border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
          />
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          <select
            value={filtroRol}
            onChange={(e) => setFiltroRol(e.target.value)}
            className="text-xs sm:text-sm border border-slate-300 rounded-lg px-3 py-1.5 bg-white text-slate-700 focus:outline-none focus:ring-2 focus:ring-indigo-500 transition-colors"
          >
            <option value="todos">Todos los Roles</option>
            <option value="Jefe / Administrador">Jefe / Administrador</option>
            <option value="Contador">Contador</option>
            <option value="Operador / Recepción">Operador / Recepción</option>
          </select>

          <select
            value={filtroEstado}
            onChange={(e) => setFiltroEstado(e.target.value)}
            className="text-xs sm:text-sm border border-slate-300 rounded-lg px-3 py-1.5 bg-white text-slate-700 focus:outline-none focus:ring-2 focus:ring-indigo-500 transition-colors"
          >
            <option value="todos">Todos los Estados</option>
            <option value="Activo">Solo Activos</option>
            <option value="Inactivo">Solo Inactivos</option>
          </select>

          {(busqueda || filtroRol !== 'todos' || filtroEstado !== 'todos') && (
            <button
              onClick={() => {
                setBusqueda('');
                setFiltroRol('todos');
                setFiltroEstado('todos');
              }}
              className="text-xs font-semibold px-2.5 py-1.5 rounded-md bg-slate-100 hover:bg-slate-200 text-slate-600 transition-colors"
            >
              Limpiar
            </button>
          )}
        </div>
      </div>

      {/* Tabla Responsiva de Usuarios */}
      <div className="rounded-xl border border-slate-200/80 bg-white shadow-sm overflow-hidden">
        <div className="overflow-x-auto w-full">
          <table className="w-full text-sm">
            <thead className="bg-slate-50/80 border-b border-slate-200">
              <tr>
                <th className="h-10 px-4 text-left font-semibold text-slate-600 text-xs ">
                  Usuario
                </th>
                <th className="h-10 px-4 text-left font-semibold text-slate-600 text-xs ">
                  Rol Asignado
                </th>
                <th className="h-10 px-4 text-center font-semibold text-slate-600 text-xs ">
                  Estado
                </th>
                <th className="h-10 px-4 text-left font-semibold text-slate-600 text-xs ">
                  Último Acceso
                </th>
                <th className="h-10 px-4 text-right font-semibold text-slate-600 text-xs ">
                  Acciones
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {usuariosFiltrados.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-12 text-center text-slate-500">
                    <Users className="mx-auto h-8 w-8 text-slate-300 mb-2" />
                    <p className="font-medium text-slate-700">No se encontraron usuarios</p>
                    <p className="text-xs text-slate-400 mt-1">
                      Probá ajustando la búsqueda o los filtros aplicados.
                    </p>
                  </td>
                </tr>
              ) : (
                usuariosFiltrados.map((usr) => (
                  <tr
                    key={usr.id}
                    className="hover:bg-slate-50/70 transition-colors duration-150"
                  >
                    {/* Usuario (Nombre + Email + Avatar) */}
                    <td className="p-3.5 px-4">
                      <div className="flex items-center gap-3">
                        <div className="h-9 w-9 rounded-full bg-slate-900 text-white font-bold text-xs flex items-center justify-center shrink-0 shadow-sm">
                          {usr.nombre
                            .split(' ')
                            .map((p) => p[0])
                            .slice(0, 2)
                            .join('')}
                        </div>
                        <div>
                          <div className="flex items-center gap-2">
                            <span className="font-semibold text-slate-900">
                              {usr.nombre}
                            </span>
                            {usr.esPrincipal && (
                              <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-indigo-50 text-indigo-700 border border-indigo-200">
                                Principal
                              </span>
                            )}
                          </div>
                          <span className="text-xs text-slate-500 font-mono">
                            {usr.email}
                          </span>
                        </div>
                      </div>
                    </td>

                    {/* Rol */}
                    <td className="p-3.5 px-4">
                      <span
                        className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold border shadow-xs ring-1 ring-inset ${getRolBadgeClass(
                          usr.rol
                        )}`}
                      >
                        {usr.rol}
                      </span>
                    </td>

                    {/* Estado */}
                    <td className="p-3.5 px-4 text-center">
                      <button
                        onClick={() => handleToggleEstado(usr)}
                        title={
                          usr.esPrincipal
                            ? 'El Administrador Principal no se puede desactivar'
                            : 'Click para cambiar estado'
                        }
                        className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold border transition-all ${
                          usr.estado === 'Activo'
                            ? 'bg-emerald-50 text-emerald-700 border-emerald-200 hover:bg-emerald-100'
                            : 'bg-slate-100 text-slate-600 border-slate-200 hover:bg-slate-200'
                        }`}
                      >
                        {usr.estado === 'Activo' ? (
                          <>
                            <span className="h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse" />
                            <span>Activo</span>
                          </>
                        ) : (
                          <>
                            <span className="h-1.5 w-1.5 rounded-full bg-slate-400" />
                            <span>Inactivo</span>
                          </>
                        )}
                      </button>
                    </td>

                    {/* Último Acceso */}
                    <td className="p-3.5 px-4 text-slate-600 text-xs tabular-nums">
                      <div className="flex items-center gap-1.5">
                        <Clock className="h-3.5 w-3.5 text-slate-400" />
                        <span>{usr.ultimoAcceso}</span>
                      </div>
                    </td>

                    {/* Acciones */}
                    <td className="p-3.5 px-4 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          onClick={() => {
                            setUsuarioEditando(usr);
                            setModalAbierto(true);
                          }}
                          className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-blue-600 transition-colors"
                          title="Editar usuario"
                        >
                          <Edit2 className="h-4 w-4" />
                        </button>

                        <button
                          onClick={() => setUsuarioAEliminar(usr)}
                          disabled={usr.esPrincipal}
                          className={`rounded-lg p-1.5 transition-colors ${
                            usr.esPrincipal
                              ? 'text-slate-200 cursor-not-allowed'
                              : 'text-slate-400 hover:bg-red-50 hover:text-red-600'
                          }`}
                          title={
                            usr.esPrincipal
                              ? 'El Administrador Principal está protegido'
                              : 'Eliminar usuario'
                          }
                        >
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal para Crear / Editar */}
      {modalAbierto && (
        <ModalUsuario
          usuarioAEditar={usuarioEditando}
          onClose={() => {
            setModalAbierto(false);
            setUsuarioEditando(null);
          }}
          onGuardado={cargarUsuarios}
        />
      )}

      {/* Modal Confirmar Eliminación */}
      {usuarioAEliminar && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm transition-opacity">
          <div className="w-[95%] max-w-md mx-auto rounded-2xl bg-white p-6 shadow-2xl border border-slate-200 animate-in fade-in zoom-in-95">
            <div className="flex items-center justify-center mb-4 h-12 w-12 rounded-full bg-red-100 mx-auto text-red-600">
              <AlertTriangle className="h-6 w-6" />
            </div>

            <h3 className="text-center text-lg font-bold text-slate-900">
              ¿Eliminar usuario?
            </h3>

            <p className="mt-2 text-center text-sm text-slate-600">
              Estás por eliminar la cuenta de{' '}
              <strong className="text-slate-900">{usuarioAEliminar.nombre}</strong> (
              <span className="font-mono text-xs">{usuarioAEliminar.email}</span>).
            </p>

            <div className="mt-4 p-3 bg-amber-50 rounded-lg border border-amber-200 text-xs text-amber-800">
              Esta acción revocará de inmediato cualquier acceso al ERP. Los registros históricos o comprobantes asociados se preservarán.
            </div>

            <div className="mt-6 flex justify-center gap-3">
              <button
                type="button"
                onClick={() => setUsuarioAEliminar(null)}
                className="px-4 py-2 text-sm font-medium text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 transition-colors"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={confirmarEliminar}
                className="px-4 py-2 text-sm font-semibold text-white bg-red-600 rounded-lg hover:bg-red-700 shadow-sm transition-all"
              >
                Sí, eliminar usuario
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}
