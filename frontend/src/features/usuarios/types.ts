export type RolUsuario = 'Jefe / Administrador' | 'Contador' | 'Operador / Recepción';
export type EstadoUsuario = 'Activo' | 'Inactivo';

export interface Usuario {
  id: string;
  nombre: string;
  email: string;
  rol: RolUsuario;
  ultimoAcceso: string;
  estado: EstadoUsuario;
  fechaCreacion: string;
  esPrincipal?: boolean; // Bloquea la eliminación accidental del admin
  telefono?: string;
  password?: string;
}
