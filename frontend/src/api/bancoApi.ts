export interface MovimientoBancoDto {
  detalle: string;
  ingreso: number | null;
  egreso: number | null;
}

export interface ResumenBancoDto {
  saldoAnterior: number;
  totalIngresos: number;
  totalEgresos: number;
  saldoActual: number;
  listaMovimientos: MovimientoBancoDto[];
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

/** Obtiene el Libro Banco */
export async function obtenerLibroBanco(clienteId?: string): Promise<ResumenBancoDto> {
  const url = clienteId ? `/api/banco?clienteId=${clienteId}` : `/api/banco`;
  const res = await fetch(url, {
    method: "GET",
    headers: { "Content-Type": "application/json" },
  });
  return manejarRespuesta<ResumenBancoDto>(res);
}

export interface CrearMovimientoBancoCommand {
  clienteId: string;
  fecha: string;
  detalle: string;
  tipoMovimiento: number; // 1 = Ingreso, 2 = Egreso
  importe: number;
}

/** Registra un nuevo movimiento bancario */
export async function crearMovimientoBanco(command: CrearMovimientoBancoCommand): Promise<{ id: number }> {
  const url = `/api/banco`;
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(command)
  });
  return manejarRespuesta<{ id: number }>(res);
}
