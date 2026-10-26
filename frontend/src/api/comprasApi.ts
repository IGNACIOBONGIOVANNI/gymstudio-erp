export interface FacturaCompraDto {
  id: number;
  fecha: string;
  tipoFactura: string;
  numeroFactura: string;
  proveedorNombre: string;
  cuit: string;
  neto: number;
  iva: number;
  total: number;
  condicionPago: string;
  proveedor?: any;
  cliente?: any;
}

export class ApiError extends Error {
  problem: any;
  constructor(problem: any) {
    super(problem.detail ?? problem.title ?? "Error en la API");
    this.name = "ApiError";
    this.problem = problem;
  }
}

async function manejarRespuesta<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const problem = await res.json();
    throw new ApiError(problem);
  }
  return res.json() as Promise<T>;
}

/** Obtiene el listado de facturas de compra (Libro Compra) */
export async function obtenerLibroCompra(clienteId?: string): Promise<FacturaCompraDto[]> {
  const url = clienteId ? `/api/facturas?clienteId=${clienteId}` : `/api/facturas`;
  const res = await fetch(url, {
    method: "GET",
    headers: { "Content-Type": "application/json" },
  });
  
  const data: any[] = await manejarRespuesta<any[]>(res);
  console.log('Respuesta Compras (Raw de /api/facturas):', data);

  // Mapeamos el modelo genérico de Facturas al formato esperado por el PDF y la UI
  return data
    .filter(f => String(f.tipoOperacion).toLowerCase() === 'compra')
    .map(f => ({
      id: f.id,
      fecha: f.fecha,
      tipoFactura: f.tipoComprobante || "A",
      numeroFactura: f.id.toString().padStart(4, "0") + "-00",
      proveedorNombre: f.proveedor ? f.proveedor.razonSocial : (f.cliente || "Proveedor"),
      cuit: f.proveedor ? f.proveedor.cuit : "XX-XXXXXXXX-X",
      neto: f.subtotal || 0,
      iva: f.iva || 0,
      total: f.total || 0,
      condicionPago: f.condicionPago || "",
      proveedor: f.proveedor, // 🔥 Pasamos el objeto completo para el PDF!
      cliente: f.clienteObj || f.cliente // Pasamos cliente también por las dudas
    }));
}

export interface CrearFacturaCompraCommand {
  clienteId: number;
  fecha: string;
  tipoFactura: string;
  numeroFactura: string;
  proveedorNombre: string;
  cuit: string;
  condicionPago: number;
  items: { productoId: number, cantidad: number }[];
}

/** Crea una factura de compra manual */
export async function crearFacturaCompra(command: CrearFacturaCompraCommand): Promise<{ id: number }> {
  const url = `/api/compras`;
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(command)
  });
  return manejarRespuesta<{ id: number }>(res);
}
