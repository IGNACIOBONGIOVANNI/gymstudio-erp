// src/api/asientosApi.ts

// Refleja el ProblemDetails (RFC 7807) que devuelve ExceptionHandlingMiddleware.
// Los campos extra (totalDebe/totalHaber/diferencia/errores) sólo vienen
// presentes según el tipo de error — por eso son todos opcionales.
export interface ProblemDetails {
  status: number;
  title: string;
  detail?: string;
  type?: string;
  instance?: string;
  totalDebe?: number;
  totalHaber?: number;
  diferencia?: number;
  errores?: Record<string, string[]>;
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
    const problem = (await res.json()) as ProblemDetails;
    throw new ApiError(problem);
  }
  return res.json() as Promise<T>;
}

interface CrearAsientoPayload {
  clienteId: number;
  fecha: string;
  descripcion: string;
  lineas: {
    cuentaId: number;
    descripcion?: string;
    debe: number;
    haber: number;
  }[];
}

interface AsientoCreadoResponse {
  asientoId: number;
}

/** Crea el asiento en estado Borrador junto con sus líneas. */
export async function crearAsiento(payload: CrearAsientoPayload): Promise<AsientoCreadoResponse> {
  const res = await fetch("/api/asientos", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return manejarRespuesta<AsientoCreadoResponse>(res);
}

/**
 * Confirma un asiento existente. Corresponde a
 * POST /api/asientos/{id}/confirmar del AsientosController.
 * Si el backend detecta desbalance (defensa en profundidad, aunque el
 * frontend ya lo previno), responde 422 con el detalle en `problem`.
 */
export async function confirmarAsiento(asientoId: string): Promise<void> {
  const res = await fetch(`/api/asientos/${asientoId}/confirmar`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
  });
  if (!res.ok) {
    const problem = (await res.json()) as ProblemDetails;
    throw new ApiError(problem);
  }
}

export interface LineaAsientoDto {
  id: number;
  cuentaId: number;
  nombreCuenta: string;
  debe: number;
  haber: number;
  descripcion?: string;
  orden: number;
}

export interface AsientoDto {
  id: number;
  clienteId: number;
  numeroAsiento: number;
  fecha: string;
  descripcion?: string;
  estado: number; // 0: Borrador, 1: Confirmado, 2: Anulado
  usuarioCreadorId: number;
  fechaCreacion: string;
  fechaConfirmacion?: string;
  usuarioConfirmacionId?: number;
  totalDebe: number;
  totalHaber: number;
  estaBalanceado: boolean;
  lineas: LineaAsientoDto[];
}

/** Obtiene el listado de asientos */
export async function obtenerAsientos(clienteId?: string): Promise<AsientoDto[]> {
  const url = clienteId ? `/api/asientos?clienteId=${clienteId}` : `/api/asientos`;
  const res = await fetch(url, {
    method: "GET",
    headers: { "Content-Type": "application/json" },
  });
  return manejarRespuesta<AsientoDto[]>(res);
}

export interface SaldoMayorDto {
  cuentaId: number;
  nombreCuenta: string;
  totalDebe: number;
  totalHaber: number;
  saldo: number;
}

/** Obtiene el Libro Mayor agrupado por cuenta */
export async function obtenerSaldosMayor(clienteId: string): Promise<SaldoMayorDto[]> {
  const res = await fetch(`/api/asientos/mayor?clienteId=${clienteId}`, {
    method: "GET",
    headers: { "Content-Type": "application/json" },
  });
  return manejarRespuesta<SaldoMayorDto[]>(res);
}

/** Elimina un asiento contable. */
export async function eliminarAsiento(asientoId: string): Promise<void> {
  const res = await fetch(`/api/asientos/${asientoId}`, {
    method: "DELETE",
    headers: { "Content-Type": "application/json" },
  });
  
  if (!res.ok) {
    let problem: any;
    try {
      problem = await res.json();
    } catch {
      problem = { title: "Error al eliminar el asiento", detail: res.statusText };
    }
    throw new ApiError(problem);
  }
}

