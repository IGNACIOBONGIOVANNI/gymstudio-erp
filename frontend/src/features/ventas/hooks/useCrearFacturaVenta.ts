import { useMutation, useQueryClient } from "@tanstack/react-query";
import { crearFacturaVenta, type CrearFacturaVentaCommand } from "../../../api/ventasApi";
import toast from "react-hot-toast";

export function useCrearFacturaVenta() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (command: CrearFacturaVentaCommand) => crearFacturaVenta(command),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["libroVenta", variables.clienteId.toString()] });
      // Invalidate caja and banco too because of automatic movements
      queryClient.invalidateQueries({ queryKey: ["libroCaja", variables.clienteId.toString()] });
      queryClient.invalidateQueries({ queryKey: ["libroBanco", variables.clienteId.toString()] });
      queryClient.invalidateQueries({ queryKey: ["asientos"] });
      toast.success("Factura de venta creada correctamente");
    },
    onError: (error: any) => {
      toast.error(error.message || "Error al crear la factura");
    },
  });
}
