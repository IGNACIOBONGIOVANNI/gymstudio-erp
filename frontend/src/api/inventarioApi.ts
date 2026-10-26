export interface MovimientoStockDto {
  id: number;
  fecha: string;
  detalle: string;
  ingreso: number;
  salida: number;
  saldo: number;
}

export const obtenerMovimientosStock = async (productoId: number): Promise<MovimientoStockDto[]> => {
  const res = await fetch(`/api/inventario/movimientos/${productoId}`, {
    method: "GET",
    headers: { "Content-Type": "application/json" },
  });
  if (!res.ok) {
    throw new Error("Error al obtener movimientos de stock");
  }
  return res.json() as Promise<MovimientoStockDto[]>;
};
