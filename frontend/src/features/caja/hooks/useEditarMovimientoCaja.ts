import { mensajeDeError } from '../../../api/errores';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';

// Tipamos lo que va a recibir (el id y los datos)
interface EditarMovimientoCajaCommand {
  id: number;
  data: any;
}

export function useEditarMovimientoCaja() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, data }: EditarMovimientoCajaCommand) => {
      // Le pegamos directo a tu API
      const response = await fetch(`/api/caja/${id}`, {
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
      // Invalidamos la query igual que hacés en el Crear para que se refresque la tabla
      queryClient.invalidateQueries({ queryKey: ['libro-caja'] });
      queryClient.invalidateQueries({ queryKey: ['asientos'] });
      queryClient.invalidateQueries({ queryKey: ['mayor'] });
      toast.success('Movimiento actualizado exitosamente');
    },
    onError: (error: any) => {
      toast.error(error?.message || 'Error al actualizar el movimiento');
    }
  });
}