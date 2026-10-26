import { useQuery } from '@tanstack/react-query';
import { obtenerLibroCaja, type ResumenCajaDto } from '../../../api/cajaApi';

export function useLibroCaja(clienteId?: string) {
  return useQuery<ResumenCajaDto, Error>({
    queryKey: ['libro-caja', clienteId],
    queryFn: () => obtenerLibroCaja(clienteId),
  });
}
