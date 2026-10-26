import { useQuery } from '@tanstack/react-query';
import { obtenerLibroCompra, type FacturaCompraDto } from '../../../api/comprasApi';

export function useLibroCompra(clienteId?: string) {
  return useQuery<FacturaCompraDto[], Error>({
    queryKey: ['libro-compra', clienteId],
    queryFn: () => obtenerLibroCompra(clienteId),
  });
}
