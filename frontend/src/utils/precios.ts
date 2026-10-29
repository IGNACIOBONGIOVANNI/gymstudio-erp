// Qué precio se propone al facturar un producto: el de venta si se vende, el de compra (costo) si se compra.
// Sin dependencias a propósito: así se prueba con el ejecutor de pruebas de Node.

export type Operacion = 'Venta' | 'Compra';

export interface ProductoConPrecios {
  id: number;
  /** Precio de compra (costo). */
  precioUnitario: number;
  precioVenta: number;
}

/** Precio que se propone para el producto en esa operación; 0 si no se conoce o todavía no tiene cargado ese precio. */
export function precioSugerido(producto: ProductoConPrecios | undefined, operacion: Operacion): number {
  if (!producto) return 0;
  const precio = Number(operacion === 'Venta' ? producto.precioVenta : producto.precioUnitario);
  return Number.isFinite(precio) && precio > 0 ? precio : 0;
}

interface ItemConPrecio {
  productoId: number;
  precioUnitario: number;
  /** Verdadero si la persona escribió el precio: ese no se toca. */
  precioManual?: boolean;
}

/**
 * Al pasar de Venta a Compra (o al revés), los ítems que traen un producto del inventario y cuyo precio salió del producto
 * toman el precio de la nueva operación. Si el producto no tiene ese precio cargado queda en 0 (no se arrastra el de la otra
 * operación, que sería vender al costo o comprar al precio de venta). Los precios escritos a mano y los conceptos libres no cambian.
 */
export function preciosAlCambiarOperacion<T extends ItemConPrecio>(items: readonly T[], productos: readonly ProductoConPrecios[], operacion: Operacion): T[] {
  return items.map((item) => {
    if (!item.productoId || item.precioManual) return item;
    const producto = productos.find((p) => p.id === item.productoId);
    if (!producto) return item;
    return { ...item, precioUnitario: precioSugerido(producto, operacion) };
  });
}
