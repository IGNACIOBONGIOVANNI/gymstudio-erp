import { useMutation, useQueryClient } from "@tanstack/react-query";
import { crearFacturaCompra, type CrearFacturaCompraCommand } from "../../../api/comprasApi";
import toast from "react-hot-toast";

export function useCrearFacturaCompra() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (command: CrearFacturaCompraCommand) => crearFacturaCompra(command),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["libroCompra", variables.clienteId.toString()] });
      // Invalidate caja and banco too because of automatic movements
      queryClient.invalidateQueries({ queryKey: ["libroCaja", variables.clienteId.toString()] });
      queryClient.invalidateQueries({ queryKey: ["libroBanco", variables.clienteId.toString()] });
      queryClient.invalidateQueries({ queryKey: ["asientos"] });
      toast.success("Factura de compra creada correctamente");
    },
    onError: (error: any) => {
      toast.error(error.message || "Error al crear la factura");
    },
  });
}
