import { useQuery } from '@tanstack/react-query';
import { obtenerLibroVenta, type FacturaVentaDto } from '../../../api/ventasApi';

export function useLibroVenta(clienteId?: string) {
  return useQuery<FacturaVentaDto[], Error>({
    queryKey: ['libro-venta', clienteId],
    queryFn: () => obtenerLibroVenta(clienteId),
  });
}
