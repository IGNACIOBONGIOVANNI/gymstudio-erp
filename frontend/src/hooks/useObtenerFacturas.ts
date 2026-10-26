import { useQuery } from '@tanstack/react-query';
import { obtenerFacturas, type FacturaDto } from '../api/facturacionApi';

export function useObtenerFacturas() {
  return useQuery<FacturaDto[], Error>({
    queryKey: ['facturas'],
    queryFn: obtenerFacturas,
  });
}
