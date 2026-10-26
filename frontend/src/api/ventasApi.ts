// src/api/ventasApi.ts

// Refleja el mismo patrón usado en asientosApi.ts
export interface FacturaVentaDto {
  id: number;
  fecha: string;
  tipoFactura: string;
  numeroFactura: string;
  clienteNombre: string;
  cuit: string;
  neto: number;
  iva: number;
  total: number;
  condicionPago: string;
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

/** Obtiene el Libro Venta de un cliente */
export async function obtenerLibroVenta(clienteId?: string): Promise<FacturaVentaDto[]> {
  const url = clienteId ? `/api/facturas?clienteId=${clienteId}` : `/api/facturas`;
  const res = await fetch(url, {
    method: "GET",
    headers: { "Content-Type": "application/json" },
  });
  
  const data: any[] = await manejarRespuesta<any[]>(res);
  console.log('Respuesta Ventas (Raw de /api/facturas):', data);

  // Mapeamos el modelo genérico de Facturas al formato esperado por el PDF y la UI
  return data
    .filter(f => String(f.tipoOperacion).toLowerCase() === 'venta')
    .map(f => ({
      id: f.id,
      fecha: f.fecha,
      tipoFactura: f.tipoComprobante || "A",
      numeroFactura: f.id.toString().padStart(4, "0") + "-00",
      clienteNombre: f.cliente || "Cliente Final",
      cuit: "XX-XXXXXXXX-X", // El form genérico no pide CUIT actualmente
      neto: f.subtotal || 0,
      iva: f.iva || 0,
      total: f.total || 0,
      condicionPago: f.condicionPago || ""
    }));
}

/** Exporta el Libro Venta a Excel */
export async function exportarExcel(clienteId?: string): Promise<void> {
  const url = clienteId ? `/api/ventas/export?clienteId=${clienteId}` : `/api/ventas/export`;
  const res = await fetch(url, { method: "GET" });
  if (!res.ok) throw new Error("Error al exportar el archivo");
  
  const blob = await res.blob();
  const downloadUrl = window.URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = downloadUrl;
  a.download = `LibroVenta_${clienteId || 'general'}.xlsx`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.URL.revokeObjectURL(downloadUrl);
}

/** Importa el Libro Venta desde Excel */
export async function importarExcel(clienteId: string, file: File): Promise<{ facturasImportadas: number }> {
  const url = `/api/ventas/import?clienteId=${clienteId}`;
  const formData = new FormData();
  formData.append("file", file);

  const res = await fetch(url, {
    method: "POST",
    body: formData, // No seteamos Content-Type, fetch lo hace automático con form-data
  });
  return manejarRespuesta<{ facturasImportadas: number }>(res);
}

export interface CrearFacturaVentaCommand {
  clienteId: number;
  fecha: string;
  tipoFactura: string;
  numeroFactura: string;
  clienteNombre: string;
  cuit: string;
  condicionPago: number;
  items: { productoId: number, cantidad: number }[];
}

/** Crea una factura de venta manual */
export async function crearFacturaVenta(command: CrearFacturaVentaCommand): Promise<{ id: number }> {
  const url = `/api/ventas`;
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(command)
  });
  return manejarRespuesta<{ id: number }>(res);
}
