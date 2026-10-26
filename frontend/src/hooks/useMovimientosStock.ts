import { useQuery } from "@tanstack/react-query";
import { obtenerMovimientosStock, type MovimientoStockDto } from "../api/inventarioApi";

export function useMovimientosStock(productoId: number | null) {
  return useQuery<MovimientoStockDto[], Error>({
    queryKey: ["movimientosStock", productoId],
    queryFn: () => obtenerMovimientosStock(productoId!),
    enabled: !!productoId,
  });
}
