import { useMutation, useQueryClient } from "@tanstack/react-query";
import { crearFactura, type CrearFacturaDto } from "../../../api/facturacionApi";
import toast from "react-hot-toast";
import { avisarSiCajaNegativa } from "../../../api/avisoCaja";

export function useCrearFactura() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: CrearFacturaDto) => crearFactura(payload),
    onSuccess: () => {
      // Invalida la caché para forzar a las tablas a buscar los datos más recientes del backend
      queryClient.invalidateQueries({ queryKey: ["asientos"] });
      queryClient.invalidateQueries({ queryKey: ["facturas"] });
      queryClient.invalidateQueries({ queryKey: ["productos"] });
      queryClient.invalidateQueries({ queryKey: ["compras"] }); // Refresca el Libro Compra
      queryClient.invalidateQueries({ queryKey: ["ventas"] });  // Refresca el Libro Venta
      
      toast.success("Factura emitida correctamente");
      void avisarSiCajaNegativa();
    },
    onError: (error: any) => {
      toast.error(error.message || "Error al emitir la factura");
    },
  });
}