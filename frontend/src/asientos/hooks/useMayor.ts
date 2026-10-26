import { useQuery } from '@tanstack/react-query';
import { obtenerSaldosMayor, type SaldoMayorDto } from '../asientosApi';

export function useMayor(clienteId: string) {
  return useQuery<SaldoMayorDto[], Error>({
    queryKey: ['mayor', clienteId],
    queryFn: () => obtenerSaldosMayor(clienteId),
    enabled: !!clienteId,
  });
}
