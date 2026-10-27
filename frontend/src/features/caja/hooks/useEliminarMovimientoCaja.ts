import { mensajeDeError } from "../../../api/errores";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import toast from "react-hot-toast";
import { avisarSiCajaNegativa } from "../../../api/avisoCaja";

export function useEliminarMovimientoCaja() {
  const queryClient = useQueryClient();

  return useMutation({
    // 👇 Si tu backend usa otra ruta, cambiala acá (por ejemplo "/api/caja/")
    mutationFn: async (id: number) => {
      const response = await fetch(`/api/caja/${id}`, {
        method: "DELETE",
      });
      if (!response.ok) {
        throw new Error(await mensajeDeError(response, "No se pudo eliminar el movimiento"));
      }
      return true;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["libro-caja"] });
      queryClient.invalidateQueries({ queryKey: ["mayor"] });
      queryClient.invalidateQueries({ queryKey: ["facturas"] });
      queryClient.invalidateQueries({ queryKey: ["libro-banco"] });
      queryClient.invalidateQueries({ queryKey: ["asientos"] });
      toast.success("Movimiento eliminado exitosamente");
      void avisarSiCajaNegativa();
    },
    onError: (error: any) => {
      toast.error(error.message || "Error al eliminar");
    },
  });
}