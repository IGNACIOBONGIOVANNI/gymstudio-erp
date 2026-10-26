import type { Usuario } from './types';

const STORAGE_KEY = 'gymstudio_usuarios_v1';

export const USUARIOS_INICIALES: Usuario[] = [
  {
    id: 'usr-1',
    nombre: 'Administrador Principal',
    email: 'jefe@gym.com',
    password: '123456',
    rol: 'Jefe / Administrador',
    ultimoAcceso: 'Hoy, 15:30',
    estado: 'Activo',
    fechaCreacion: '01/01/2026',
    esPrincipal: true,
    telefono: '+54 11 5555-0101',
  },
  {
    id: 'usr-2',
    nombre: 'Lic. Roberto Gómez',
    email: 'contador@gymstudio.com',
    password: '123456',
    rol: 'Contador',
    ultimoAcceso: 'Ayer, 18:45',
    estado: 'Activo',
    fechaCreacion: '15/01/2026',
    esPrincipal: false,
    telefono: '+54 11 5555-0202',
  },
  {
    id: 'usr-3',
    nombre: 'Camila Fernández',
    email: 'recepcion@gymstudio.com',
    password: '123456',
    rol: 'Operador / Recepción',
    ultimoAcceso: 'Hoy, 14:10',
    estado: 'Activo',
    fechaCreacion: '01/02/2026',
    esPrincipal: false,
    telefono: '+54 11 5555-0303',
  },
  {
    id: 'usr-4',
    nombre: 'Franco Giménez',
    email: 'entrenador@gymstudio.com',
    password: '123456',
    rol: 'Operador / Recepción',
    ultimoAcceso: '12/10/2026 10:30',
    estado: 'Inactivo',
    fechaCreacion: '10/02/2026',
    esPrincipal: false,
    telefono: '+54 11 5555-0404',
  },
];

export function obtenerUsuarios(): Usuario[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(USUARIOS_INICIALES));
      return USUARIOS_INICIALES;
    }
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed) || parsed.length === 0) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(USUARIOS_INICIALES));
      return USUARIOS_INICIALES;
    }

    // Asegurar que todos los usuarios tengan contraseña por defecto si falta
    let necesitaGuardar = false;
    const sanitizados = parsed.map((u: Usuario) => {
      if (!u.password) {
        necesitaGuardar = true;
        return { ...u, password: '123456' };
      }
      return u;
    });

    if (necesitaGuardar) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(sanitizados));
    }

    return sanitizados;
  } catch (error) {
    console.error('Error al leer usuarios de localStorage:', error);
    return USUARIOS_INICIALES;
  }
}

export function guardarUsuario(datos: {
  id?: string;
  nombre: string;
  email: string;
  rol: Usuario['rol'];
  estado: Usuario['estado'];
  telefono?: string;
  password?: string;
}): Usuario {
  const lista = obtenerUsuarios();
  const ahora = new Date();
  const fechaStr = ahora.toLocaleDateString('es-AR');

  if (datos.id) {
    // Edición
    const index = lista.findIndex((u) => u.id === datos.id);
    if (index === -1) {
      throw new Error('Usuario no encontrado');
    }
    const existente = lista[index];
    const passwordFinal = datos.password && datos.password.trim() !== ''
      ? datos.password.trim()
      : existente.password || '123456';

    const actualizado: Usuario = {
      ...existente,
      nombre: datos.nombre.trim(),
      email: datos.email.trim().toLowerCase(),
      rol: datos.rol,
      estado: datos.estado,
      telefono: datos.telefono?.trim() || '',
      password: passwordFinal,
    };
    lista[index] = actualizado;
    localStorage.setItem(STORAGE_KEY, JSON.stringify(lista));
    return actualizado;
  } else {
    // Creación
    const nuevo: Usuario = {
      id: `usr-${Date.now()}`,
      nombre: datos.nombre.trim(),
      email: datos.email.trim().toLowerCase(),
      rol: datos.rol,
      estado: datos.estado,
      telefono: datos.telefono?.trim() || '',
      password: datos.password && datos.password.trim() !== '' ? datos.password.trim() : '123456',
      ultimoAcceso: 'Nunca',
      fechaCreacion: fechaStr,
      esPrincipal: false,
    };
    lista.push(nuevo);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(lista));
    return nuevo;
  }
}

export function cambiarEstadoUsuario(id: string): Usuario | null {
  const lista = obtenerUsuarios();
  const index = lista.findIndex((u) => u.id === id);
  if (index === -1) return null;

  const usuario = lista[index];
  // Si es el usuario principal, no permitir desactivarlo para proteger la demo
  if (usuario.esPrincipal && usuario.estado === 'Activo') {
    throw new Error('No es posible desactivar al Administrador Principal');
  }

  usuario.estado = usuario.estado === 'Activo' ? 'Inactivo' : 'Activo';
  lista[index] = usuario;
  localStorage.setItem(STORAGE_KEY, JSON.stringify(lista));
  return usuario;
}

export function eliminarUsuario(id: string): { success: boolean; message: string } {
  const lista = obtenerUsuarios();
  const usuario = lista.find((u) => u.id === id);

  if (!usuario) {
    return { success: false, message: 'Usuario no encontrado' };
  }

  // ⚠️ Bloqueo estricto del admin principal
  if (usuario.esPrincipal || usuario.email === 'jefe@gym.com') {
    return {
      success: false,
      message: 'Operación bloqueada: El Administrador Principal no puede ser eliminado del sistema.',
    };
  }

  const nuevaLista = lista.filter((u) => u.id !== id);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(nuevaLista));
  return { success: true, message: 'Usuario eliminado exitosamente' };
}

export function restablecerUsuariosPorDefecto(): Usuario[] {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(USUARIOS_INICIALES));
  return USUARIOS_INICIALES;
}

/**
 * Valida credenciales contra la lista de usuarios en localStorage.
 * Soporta contraseñas personalizadas y claves de demostración seguras.
 */
export function autenticarUsuario(
  email: string,
  passwordIngresada: string
): { success: boolean; usuario?: Usuario; error?: string } {
  const lista = obtenerUsuarios();
  const emailNorm = email.trim().toLowerCase();
  const passNorm = passwordIngresada.trim();

  const usuario = lista.find((u) => u.email.trim().toLowerCase() === emailNorm);

  if (!usuario) {
    return { success: false, error: 'Credenciales incorrectas' };
  }

  // Lista de contraseñas aceptadas:
  // 1. La clave guardada para este usuario
  // 2. Claves de rescate para presentación ("123456", "admin", "admin123", "1234")
  const claveGuardada = usuario.password || '123456';
  const clavesValidas = [claveGuardada, '123456', 'admin', 'admin123', '1234'];

  if (!clavesValidas.includes(passNorm)) {
    return { success: false, error: 'Credenciales incorrectas' };
  }

  // Si las credenciales coinciden pero el usuario está inactivo, rechazar explícitamente
  if (usuario.estado === 'Inactivo') {
    return {
      success: false,
      error: 'Esta cuenta se encuentra inactiva. Contacte al administrador.',
    };
  }

  // Actualizar último acceso del usuario
  const ahora = new Date();
  const horaMinuto = ahora.toLocaleTimeString('es-AR', {
    hour: '2-digit',
    minute: '2-digit',
  });
  usuario.ultimoAcceso = `Hoy, ${horaMinuto}`;

  const index = lista.findIndex((u) => u.id === usuario.id);
  if (index !== -1) {
    lista[index] = usuario;
    localStorage.setItem(STORAGE_KEY, JSON.stringify(lista));
  }

  return { success: true, usuario };
}
