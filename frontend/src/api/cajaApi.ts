export interface MovimientoCajaDto {
  fecha: string;
  detalle: string;
  ingreso: number | null;
  egreso: number | null;
}

export interface ResumenCajaDto {
  saldoAnterior: number;
  totalIngresos: number;
  totalEgresos: number;
  saldoActual: number;
  listaMovimientos: MovimientoCajaDto[];
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

/** Obtiene el Libro de Caja */
export async function obtenerLibroCaja(clienteId?: string): Promise<ResumenCajaDto> {
  const url = clienteId ? `/api/caja?clienteId=${clienteId}` : `/api/caja`;
  const res = await fetch(url, {
    method: "GET",
    headers: { "Content-Type": "application/json" },
  });
  return manejarRespuesta<ResumenCajaDto>(res);
}

export interface CrearMovimientoCajaCommand {
  clienteId: string;
  fecha: string;
  detalle: string;
  tipoMovimiento: number; // 1 = Ingreso, 2 = Egreso
  importe: number;
  // Cuenta contra la que se registra el asiento (null = la de siempre).
  cuentaContrapartidaId?: number | null;
  // Cliente al que se le cobró o proveedor al que se le pagó (cuando la contrapartida es Deudores o Proveedores).
  clienteComercialId?: number | null;
  proveedorId?: number | null;
}

/** Registra un nuevo movimiento en la caja */
export async function crearMovimientoCaja(command: CrearMovimientoCajaCommand): Promise<{ id: number }> {
  const url = `/api/caja`;
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(command)
  });
  return manejarRespuesta<{ id: number }>(res);
}
