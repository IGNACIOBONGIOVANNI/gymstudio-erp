import { mensajeDeError } from "./errores";

export interface ProductoDto {
  id: number;
  nombre: string;
  precioUnitario: number;
  precioVenta: number;
  stockActual: number;
  marca?: string;
  sabor?: string;
  calorias?: number;
  proteinas?: number;
  carbohidratos?: number;
  grasas?: number;
}

export interface CrearProductoDto {
  nombre: string;
  precioUnitario: number;
  precioVenta: number;
  stockInicial: number;
  marca?: string;
  sabor?: string;
  calorias?: number;
  proteinas?: number;
  carbohidratos?: number;
  grasas?: number;
}

export interface ActualizarProductoDto {
  id: number;
  nombre: string;
  precioUnitario: number;
  precioVenta: number;
  marca?: string;
  sabor?: string;
  calorias?: number;
  proteinas?: number;
  carbohidratos?: number;
  grasas?: number;
}

export const obtenerProductos = async (): Promise<ProductoDto[]> => {
  const res = await fetch("/api/productos", {
    method: "GET",
    headers: { "Content-Type": "application/json" },
  });
  if (!res.ok) {
    throw new Error(await mensajeDeError(res, "Error al obtener productos"));
  }
  return res.json() as Promise<ProductoDto[]>;
};

export const crearProducto = async (data: CrearProductoDto): Promise<number> => {
  const res = await fetch("/api/productos", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data)
  });
  if (!res.ok) throw new Error(await mensajeDeError(res, "Error al crear producto"));
  return res.json();
};

export const actualizarProducto = async (data: ActualizarProductoDto): Promise<void> => {
  const res = await fetch(`/api/productos/${data.id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data)
  });
  if (!res.ok) throw new Error(await mensajeDeError(res, "Error al actualizar producto"));
};

export const eliminarProducto = async (id: number): Promise<void> => {
  const res = await fetch(`/api/productos/${id}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error(await mensajeDeError(res, "Error al eliminar producto"));
};
