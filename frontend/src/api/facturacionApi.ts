// src/api/facturacionApi.ts

export interface CrearFacturaDto {
  fecha: string;
  tipoOperacion: string;
  tipoComprobante: string;
  cliente: string;
  clienteId?: number | null;
  proveedorId?: number | null;
  condicionPago?: string;
  puntoVenta?: number | null;
  numeroComprobante?: number | null;
  subtotal: number;
  iva: number;
  total: number;
  items: CrearFacturaItemDto[];
}

export interface CrearFacturaItemDto {
  productoId: number;
  agregarAlInventario?: boolean;
  descripcion: string;
  cantidad: number;
  precioUnitario: number;
  subtotal: number;
}

export interface FacturaItemDto {
  id?: number;
  productoId?: number | null;
  descripcion: string;
  cantidad: number;
  precioUnitario: number;
  subtotal: number;
}

export interface FacturaDto {
  id: number;
  fecha: string;
  tipoOperacion: string;
  tipoComprobante: string;
  cliente: string;
  subtotal: number;
  iva: number;
  total: number;
  puntoVenta?: number | null;
  numeroComprobante?: number | null;
  numeroFactura?: string | null;
  cuit?: string | null;
  condicionPago?: string;
  anulada?: boolean;
  fechaAnulacion?: string | null;
  items?: FacturaItemDto[];
}

export class ApiError extends Error {
  problem: any;
  constructor(problem: any) {
    super(problem?.detail ?? problem?.title ?? "Error en la API");
    this.name = "ApiError";
    this.problem = problem;
  }
}

// 🔥 EL BLINDAJE: Ahora soporta respuestas vacías sin crashear
async function manejarRespuesta<T>(res: Response): Promise<T> {
  const text = await res.text(); // Leemos el texto crudo primero
  
  if (!res.ok) {
    let problem = { detail: res.statusText };
    if (text) {
      try {
        problem = JSON.parse(text);
      } catch (e) {
        problem = { detail: text };
      }
    }
    throw new ApiError(problem);
  }

  // Si es 204 No Content, devolvemos un objeto vacío en lugar de intentar parsear
  if (res.status === 204) {
    return {} as T;
  }

  // Parseamos solo si realmente hay texto
  return text ? (JSON.parse(text) as T) : ({} as T);
}

/** Crea una factura en el sistema */
export async function crearFactura(payload: CrearFacturaDto): Promise<{ id: number }> {
  const res = await fetch("/api/facturas", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  return manejarRespuesta<{ id: number }>(res);
}

/** Obtiene todas las facturas del sistema */
export async function obtenerFacturas(): Promise<FacturaDto[]> {
  const res = await fetch("/api/facturas", {
    method: "GET",
    headers: { "Content-Type": "application/json" },
  });
  return manejarRespuesta<FacturaDto[]>(res);
}

/** Edita una factura existente */
export async function editarFactura(id: number, payload: CrearFacturaDto): Promise<void> {
  const res = await fetch(`/api/facturas/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  return manejarRespuesta<void>(res);
}

/**
 * Anula una factura. No se borra: el backend la marca como anulada y genera
 * la anulación de su asiento, movimiento inverso de caja/banco y devolución de stock.
 */
export async function anularFactura(id: number): Promise<void> {
  const res = await fetch(`/api/facturas/${id}/anular`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
  });
  return manejarRespuesta<void>(res);
}