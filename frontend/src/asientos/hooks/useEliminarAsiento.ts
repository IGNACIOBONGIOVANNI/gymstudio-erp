import { useMutation, useQueryClient } from "@tanstack/react-query";
import { eliminarAsiento, ApiError } from "../asientosApi";

export function useEliminarAsiento() {
  const queryClient = useQueryClient();

  return useMutation<void, ApiError, string>({
    mutationFn: (id: string) => eliminarAsiento(id),
    onSuccess: () => {
      // Invalidar agresivamente todas las queries afectadas
      queryClient.invalidateQueries({ queryKey: ["asientos"] });
      queryClient.invalidateQueries({ queryKey: ["caja"] });
      queryClient.invalidateQueries({ queryKey: ["banco"] });
    },
    onError: (error) => {
      alert("Error al eliminar: " + (error.message || "Ocurrió un error inesperado al eliminar el asiento."));
    },
  });
}
