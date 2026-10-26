import { useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';

export function useEliminarMovimientoBanco() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number) => {
      // 👇 Asegurate de que esta URL sea la correcta en tu backend
      const response = await fetch(`/api/banco/${id}`, {
        method: 'DELETE',
      });

      if (!response.ok) {
        throw new Error('Error al eliminar el movimiento');
      }

      return true;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["libro-banco"] });
      queryClient.invalidateQueries({ queryKey: ["facturas"] });
      queryClient.invalidateQueries({ queryKey: ["libro-caja"] });
      queryClient.invalidateQueries({ queryKey: ["asientos"] });
      toast.success("Movimiento eliminado exitosamente");
    },
    onError: (error: any) => {
      toast.error(error?.message || 'Error al eliminar el movimiento');
    }
  });
}