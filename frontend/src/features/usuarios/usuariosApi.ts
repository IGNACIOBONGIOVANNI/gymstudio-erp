import type { Usuario, RolUsuario, EstadoUsuario } from './types';

// El backend identifica los roles por código; la UI muestra estas etiquetas.
const ETIQUETA_POR_ROL: Record<string, RolUsuario> = {
  Administrador: 'Jefe / Administrador',
  Contador: 'Contador',
  Recepcion: 'Operador / Recepción',
};

const ROL_POR_ETIQUETA: Record<RolUsuario, string> = {
  'Jefe / Administrador': 'Administrador',
  Contador: 'Contador',
  'Operador / Recepción': 'Recepcion',
};

interface UsuarioApiDto {
  id: number;
  nombre: string;
  email: string;
  rol: string;
  telefono: string | null;
  activo: boolean;
  esPrincipal: boolean;
  ultimoLogin: string | null;
  fechaCreacion: string;
}

async function verificar(res: Response): Promise<Response> {
  if (res.ok) return res;
  let mensaje = res.statusText || 'Error en la API';
  try {
    const problem = await res.json();
    mensaje = problem?.detail ?? problem?.title ?? mensaje;
  } catch {
    // respuesta sin cuerpo JSON
  }
  throw new Error(mensaje);
}

function aUsuario(dto: UsuarioApiDto): Usuario {
  return {
    id: String(dto.id),
    nombre: dto.nombre,
    email: dto.email,
    rol: ETIQUETA_POR_ROL[dto.rol] ?? 'Operador / Recepción',
    estado: dto.activo ? 'Activo' : 'Inactivo',
    esPrincipal: dto.esPrincipal,
    telefono: dto.telefono ?? '',
    ultimoAcceso: dto.ultimoLogin
      ? new Date(dto.ultimoLogin).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' })
      : 'Nunca',
    fechaCreacion: new Date(dto.fechaCreacion).toLocaleDateString('es-AR'),
  };
}

export async function obtenerUsuarios(): Promise<Usuario[]> {
  const res = await verificar(await fetch('/api/usuarios'));
  const datos: UsuarioApiDto[] = await res.json();
  return datos.map(aUsuario);
}

export async function guardarUsuario(datos: {
  id?: string;
  nombre: string;
  email: string;
  rol: RolUsuario;
  estado: EstadoUsuario;
  telefono?: string;
  password?: string;
  confirmacionPassword?: string;
}): Promise<void> {
  const body = JSON.stringify({
    nombre: datos.nombre,
    email: datos.email,
    rol: ROL_POR_ETIQUETA[datos.rol],
    telefono: datos.telefono,
    activo: datos.estado === 'Activo',
    password: datos.password || null,
    confirmacionPassword: datos.confirmacionPassword || null,
  });

  await verificar(await fetch(datos.id ? `/api/usuarios/${datos.id}` : '/api/usuarios', {
    method: datos.id ? 'PUT' : 'POST',
    headers: { 'Content-Type': 'application/json' },
    body,
  }));
}

export async function cambiarEstadoUsuario(usuario: Usuario): Promise<void> {
  await verificar(await fetch(`/api/usuarios/${usuario.id}/estado`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ activo: usuario.estado !== 'Activo' }),
  }));
}

export async function eliminarUsuario(id: string): Promise<void> {
  await verificar(await fetch(`/api/usuarios/${id}`, { method: 'DELETE' }));
}
