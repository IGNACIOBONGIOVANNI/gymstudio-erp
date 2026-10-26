import { mensajeDeError } from '../../../api/errores';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';

interface EditarMovimientoBancoCommand {
  id: number;
  data: any;
}

export function useEditarMovimientoBanco() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, data }: EditarMovimientoBancoCommand) => {
      const response = await fetch(`/api/banco/${id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        throw new Error(await mensajeDeError(response, 'Error al editar el movimiento'));
      }

      return response.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['libro-banco'] });
      queryClient.invalidateQueries({ queryKey: ['asientos'] });
      queryClient.invalidateQueries({ queryKey: ['mayor'] });
      toast.success('Movimiento actualizado exitosamente');
    },
    onError: (error: any) => {
      toast.error(error?.message || 'Error al actualizar el movimiento');
    }
  });
}