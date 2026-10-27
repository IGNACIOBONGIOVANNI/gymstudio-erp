import { useMutation, useQueryClient } from "@tanstack/react-query";
import { eliminarAsiento, ApiError } from "../asientosApi";
import toast from "react-hot-toast";

export function useEliminarAsiento() {
  const queryClient = useQueryClient();

  return useMutation<void, ApiError, string>({
    mutationFn: (id: string) => eliminarAsiento(id),
    onSuccess: () => {
      // Anular un asiento de factura revierte también caja/banco y stock.
      queryClient.invalidateQueries({ queryKey: ["asientos"] });
      queryClient.invalidateQueries({ queryKey: ["mayor"] });
      queryClient.invalidateQueries({ queryKey: ["facturas"] });
      queryClient.invalidateQueries({ queryKey: ["libro-caja"] });
      queryClient.invalidateQueries({ queryKey: ["libro-banco"] });
      queryClient.invalidateQueries({ queryKey: ["productos"] });
      queryClient.invalidateQueries({ queryKey: ["movimientosStock"] });
      toast.success("Asiento anulado: ya no suma en el Mayor ni en el Balance");
    },
    onError: (error) => {
      toast.error(error.message || "Ocurrió un error inesperado al anular el asiento.");
    },
  });
}
