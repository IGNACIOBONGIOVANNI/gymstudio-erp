import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { obtenerProductos, crearProducto, actualizarProducto, eliminarProducto, type ProductoDto, type CrearProductoDto, type ActualizarProductoDto } from "../api/productosApi";

export function useProductos() {
  return useQuery<ProductoDto[], Error>({
    queryKey: ["productos"],
    queryFn: obtenerProductos,
  });
}

export function useMutacionesProducto() {
  const queryClient = useQueryClient();

  const crearMutation = useMutation({
    mutationFn: (data: CrearProductoDto) => crearProducto(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["productos"] });
    },
  });

  const actualizarMutation = useMutation({
    mutationFn: (data: ActualizarProductoDto) => actualizarProducto(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["productos"] });
    },
  });

  const eliminarMutation = useMutation({
    mutationFn: (id: number) => eliminarProducto(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["productos"] });
    },
  });

  return { crearMutation, actualizarMutation, eliminarMutation };
}
