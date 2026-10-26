import { useMutation, useQueryClient } from '@tanstack/react-query';
import { crearMovimientoBanco, type CrearMovimientoBancoCommand } from '../../../api/bancoApi';
import toast from 'react-hot-toast';

export function useCrearMovimientoBanco() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (command: CrearMovimientoBancoCommand) => crearMovimientoBanco(command),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['libro-banco'] });
      toast.success('Movimiento bancario registrado exitosamente');
    },
    onError: (error: any) => {
      toast.error(error?.message || 'Error al registrar el movimiento');
    }
  });
}
