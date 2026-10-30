import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  CONFIRMACION_DE_RESTAURACION,
  agruparPorModulo,
  confirmacionValida,
  diferenciaDeFilas,
  nombreDeArchivoDeCopia,
  resumirComparacion,
  textoDeFilas,
  type TablaComparada,
} from '../src/features/configuracion/copiasUtil.ts';

const tabla = (cambios: Partial<TablaComparada>): TablaComparada => ({
  tabla: 'contabilidad.x',
  etiqueta: 'X',
  grupo: 'Ventas',
  orden: 10,
  filasCopia: 1,
  filasAhora: 1,
  estado: 'igual',
  esAuditoria: false,
  ...cambios,
});

test('el resumen cuenta cada resultado y deja afuera la auditoría, que crece sola', () => {
  const r = resumirComparacion([
    tabla({ estado: 'igual' }),
    tabla({ estado: 'igual' }),
    tabla({ estado: 'cambia' }),
    tabla({ estado: 'solo-copia' }),
    tabla({ estado: 'solo-ahora' }),
    tabla({ estado: 'cambia', esAuditoria: true }),
  ]);
  assert.deepEqual(r, { cambian: 1, iguales: 2, soloEnLaCopia: 1, soloAhora: 1 });
});

test('las tablas se agrupan por módulo en el orden de pantalla, y dentro de cada módulo por nombre', () => {
  const grupos = agruparPorModulo([
    tabla({ tabla: 'c.socios', grupo: 'Socios', orden: 30 }),
    tabla({ tabla: 'c.facturas', grupo: 'Ventas', orden: 10 }),
    tabla({ tabla: 'c.asientos', grupo: 'Contabilidad', orden: 20 }),
    tabla({ tabla: 'c.compras', grupo: 'Ventas', orden: 10 }),
  ]);
  assert.deepEqual(
    grupos.map((g) => [g.grupo, g.tablas.map((t) => t.tabla)]),
    [
      ['Ventas', ['c.compras', 'c.facturas']],
      ['Contabilidad', ['c.asientos']],
      ['Socios', ['c.socios']],
    ],
  );
});

test('agrupar no modifica la lista que recibe', () => {
  const original = [tabla({ tabla: 'b', orden: 2 }), tabla({ tabla: 'a', orden: 1 })];
  agruparPorModulo(original);
  assert.deepEqual(original.map((t) => t.tabla), ['b', 'a']);
});

test('las filas se muestran con separador de miles, o con un guion si la tabla no existe de ese lado', () => {
  assert.equal(textoDeFilas(0), '0');
  assert.equal(textoDeFilas(null), '—');
  assert.equal(textoDeFilas(1234), (1234).toLocaleString('es-AR'));
});

test('la diferencia de filas lleva su signo y es nula si no hay cambio o falta un lado', () => {
  assert.equal(diferenciaDeFilas({ filasCopia: 3, filasAhora: 5 }), '+2');
  assert.equal(diferenciaDeFilas({ filasCopia: 5, filasAhora: 4 }), '-1');
  assert.equal(diferenciaDeFilas({ filasCopia: 5, filasAhora: 5 }), null);
  assert.equal(diferenciaDeFilas({ filasCopia: null, filasAhora: 5 }), null);
  assert.equal(diferenciaDeFilas({ filasCopia: 5, filasAhora: null }), null);
});

test('el nombre del archivo lleva la fecha y la hora de la PC con ceros a la izquierda', () => {
  assert.equal(nombreDeArchivoDeCopia(new Date(2026, 9, 5, 7, 3, 9)), 'gymstudio_copia_20261005_070309.zip');
  assert.equal(nombreDeArchivoDeCopia(new Date(2026, 9, 29, 22, 15, 30), 'gymstudio_antes-de-restaurar'), 'gymstudio_antes-de-restaurar_20261029_221530.zip');
});

test('la confirmación exige la palabra exacta, en mayúsculas', () => {
  assert.equal(CONFIRMACION_DE_RESTAURACION, 'RESTAURAR');
  assert.equal(confirmacionValida('RESTAURAR'), true);
  assert.equal(confirmacionValida('  RESTAURAR  '), true);
  assert.equal(confirmacionValida('restaurar'), false);
  assert.equal(confirmacionValida('RESTAURA'), false);
  assert.equal(confirmacionValida(''), false);
});
