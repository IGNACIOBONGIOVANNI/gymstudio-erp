import { useMutation, useQueryClient } from "@tanstack/react-query";
import { anularFactura } from "../../../api/facturacionApi";
import toast from "react-hot-toast";
import { avisarSiCajaNegativa } from "../../../api/avisoCaja";

export function useAnularFactura() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => anularFactura(id),
    onSuccess: () => {
      // La anulación toca facturas, libro diario/mayor, caja, banco y stock.
      queryClient.invalidateQueries({ queryKey: ["facturas"] });
      queryClient.invalidateQueries({ queryKey: ["asientos"] });
      queryClient.invalidateQueries({ queryKey: ["mayor"] });
      queryClient.invalidateQueries({ queryKey: ["libro-caja"] });
      queryClient.invalidateQueries({ queryKey: ["libro-banco"] });
      queryClient.invalidateQueries({ queryKey: ["productos"] });
      queryClient.invalidateQueries({ queryKey: ["movimientosStock"] });

      toast.success("Factura anulada: su asiento quedó anulado y se revirtieron caja/banco y stock");
      void avisarSiCajaNegativa();
    },
    onError: (error: any) => {
      toast.error(error.message || "Error al anular la factura");
    },
  });
}
