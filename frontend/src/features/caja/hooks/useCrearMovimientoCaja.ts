import { useMutation, useQueryClient } from '@tanstack/react-query';
import { crearMovimientoCaja, type CrearMovimientoCajaCommand } from '../../../api/cajaApi';
import toast from 'react-hot-toast';
import { avisarSiCajaNegativa } from "../../../api/avisoCaja";

export function useCrearMovimientoCaja() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (command: CrearMovimientoCajaCommand) => crearMovimientoCaja(command),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['libro-caja'] });
      queryClient.invalidateQueries({ queryKey: ['asientos'] });
      queryClient.invalidateQueries({ queryKey: ['mayor'] });
      toast.success('Movimiento de caja registrado exitosamente');
      void avisarSiCajaNegativa();
    },
    onError: (error: any) => {
      toast.error(error?.message || 'Error al registrar el movimiento');
    }
  });
}
