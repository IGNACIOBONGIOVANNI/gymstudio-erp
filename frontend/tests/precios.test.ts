import assert from 'node:assert/strict';
import { test } from 'node:test';
import { precioSugerido, preciosAlCambiarOperacion, type ProductoConPrecios } from '../src/utils/precios.ts';

const productos: ProductoConPrecios[] = [
  { id: 1, precioUnitario: 100, precioVenta: 150 },
  { id: 2, precioUnitario: 200, precioVenta: 0 },        // nació de una compra: todavía no tiene precio de venta
  { id: 3, precioUnitario: 0, precioVenta: 500 },        // un servicio: no tiene costo
];

test('se propone el precio de venta al vender y el de compra al comprar', () => {
  assert.equal(precioSugerido(productos[0], 'Venta'), 150);
  assert.equal(precioSugerido(productos[0], 'Compra'), 100);
});

test('si el producto no tiene ese precio, o no hay producto, no se propone ninguno', () => {
  assert.equal(precioSugerido(productos[1], 'Venta'), 0);
  assert.equal(precioSugerido(productos[2], 'Compra'), 0);
  assert.equal(precioSugerido(undefined, 'Venta'), 0);
  assert.equal(precioSugerido({ id: 9, precioUnitario: Number.NaN, precioVenta: -5 }, 'Venta'), 0);
});

test('al pasar de Venta a Compra el precio tomado del producto cambia al de compra, y viceversa', () => {
  const venta = [{ productoId: 1, precioUnitario: 150 }];
  const compra = preciosAlCambiarOperacion(venta, productos, 'Compra');
  assert.equal(compra[0].precioUnitario, 100);
  assert.equal(preciosAlCambiarOperacion(compra, productos, 'Venta')[0].precioUnitario, 150);
});

test('un precio escrito a mano no se pisa al cambiar de operación', () => {
  const items = [{ productoId: 1, precioUnitario: 175, precioManual: true }];
  assert.equal(preciosAlCambiarOperacion(items, productos, 'Compra')[0].precioUnitario, 175);
});

test('un concepto libre o un producto que ya no existe no cambia', () => {
  const items = [{ productoId: 0, precioUnitario: 999 }, { productoId: 77, precioUnitario: 40 }];
  const r = preciosAlCambiarOperacion(items, productos, 'Compra');
  assert.deepEqual(r.map((i) => i.precioUnitario), [999, 40]);
});

test('si el producto no tiene precio en la nueva operación queda en 0 en vez de arrastrar el de la otra', () => {
  const comprando = [{ productoId: 2, precioUnitario: 200 }];            // costo $ 200 propuesto al comprar
  assert.equal(preciosAlCambiarOperacion(comprando, productos, 'Venta')[0].precioUnitario, 0);   // sin precio de venta: no vender a $ 200
});

test('no modifica la lista original', () => {
  const items = [{ productoId: 1, precioUnitario: 150 }];
  preciosAlCambiarOperacion(items, productos, 'Compra');
  assert.equal(items[0].precioUnitario, 150);
});
