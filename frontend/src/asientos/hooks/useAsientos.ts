import { useQuery } from '@tanstack/react-query';
import { obtenerAsientos, type AsientoDto } from '../asientosApi';

export function useAsientos(clienteId?: string) {
  return useQuery<AsientoDto[], Error>({
    queryKey: ['asientos', clienteId],
    queryFn: () => obtenerAsientos(clienteId),
  });
}
