import { useMutation, useQueryClient } from "@tanstack/react-query";
import { editarFactura, type CrearFacturaDto } from "../../../api/facturacionApi";
import toast from "react-hot-toast";

export function useEditarFactura() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: CrearFacturaDto }) => editarFactura(id, payload),
    onSuccess: async () => {
      // 1. Tiramos el cartel de éxito
      toast.success("Factura editada correctamente");
      
      // 2. LA BOMBA NUCLEAR: Forzamos la recarga de TODA la tabla al instante
      await queryClient.refetchQueries(); 
    },
    onError: (error: any) => {
      toast.error(error.message || "Error al editar la factura");
    },
  });
}