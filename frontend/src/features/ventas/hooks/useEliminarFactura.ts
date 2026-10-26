import { useMutation, useQueryClient } from "@tanstack/react-query";
import { eliminarFactura } from "../../../api/facturacionApi";
import toast from "react-hot-toast";

export function useEliminarFactura() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => eliminarFactura(id),
    onSuccess: () => {
      // ¡ESTA ES LA LLAVE MÁGICA PARA LAS FACTURAS!
      queryClient.invalidateQueries({ queryKey: ["facturas"] });
      
      // 🔥 Y ESTAS SON LAS LLAVES MÁGICAS PARA LIMPIAR LA CAJA Y EL BANCO 🔥
      // (Fijate de usar el nombre exacto que le hayas puesto en useLibroCaja y useLibroBanco, 
      // si tus llaves se llaman distinto, ej: ["caja"], cambialo acá)
      queryClient.invalidateQueries({ queryKey: ["libro-caja"] });
      queryClient.invalidateQueries({ queryKey: ["libro-banco"] });
      queryClient.invalidateQueries({ queryKey: ["asientos"] });
      
      toast.success("Factura eliminada correctamente");
    },
    onError: (error: any) => {
      toast.error(error.message || "Error al eliminar la factura");
    },
  });
}