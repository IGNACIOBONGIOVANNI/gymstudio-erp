import { useQuery } from '@tanstack/react-query';
import { obtenerLibroBanco, type ResumenBancoDto } from '../../../api/bancoApi';

export function useLibroBanco(clienteId?: string) {
  return useQuery<ResumenBancoDto, Error>({
    queryKey: ['libro-banco', clienteId],
    queryFn: () => obtenerLibroBanco(clienteId),
  });
}
