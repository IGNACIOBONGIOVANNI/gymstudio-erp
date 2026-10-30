import { useMutation, useQueryClient } from '@tanstack/react-query';
import { importarExcel } from '../../../api/ventasApi';
import toast from 'react-hot-toast';

export function useImportarLibroVenta() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ clienteId, file }: { clienteId: string; file: File }) => 
      importarExcel(clienteId, file),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['libro-venta'] });
      toast.success(`Se importaron ${data.facturasImportadas} facturas exitosamente.`);
      if (data.filasOmitidas) toast(`${data.filasOmitidas} filas se omitieron porque sus datos no eran válidos (fecha, tipo, número, CUIT o importes).`, { icon: '⚠️', duration: 8000 });
    },
    onError: (error: any) => {
      toast.error(error?.message || 'Error al importar el archivo Excel');
    }
  });
}
