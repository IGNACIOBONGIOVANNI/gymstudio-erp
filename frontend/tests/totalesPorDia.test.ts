import assert from 'node:assert/strict';
import { test } from 'node:test';
import { agruparPorDia, claveDelDia } from '../src/utils/totalesPorDia.ts';

const asiento = (numero: number, fecha: string, debe: number, haber = debe) => ({ numero, fecha, totalDebe: debe, totalHaber: haber });

test('claveDelDia toma solo la fecha, sin la hora ni la zona horaria', () => {
  assert.equal(claveDelDia('2026-10-27T00:00:00Z'), '2026-10-27');
  assert.equal(claveDelDia('2026-10-27T23:59:59.999-03:00'), '2026-10-27');
  assert.equal(claveDelDia('2026-10-27'), '2026-10-27');
  assert.equal(claveDelDia(null), '');
  assert.equal(claveDelDia(undefined), '');
});

test('los asientos del mismo día se juntan y cada día suma su Debe y su Haber (el ejemplo del Libro Diario)', () => {
  const asientos = [
    asiento(1, '2026-10-27T00:00:00Z', 2359.5),
    asiento(2, '2026-10-28T00:00:00Z', 400),
    asiento(3, '2026-10-29T00:00:00Z', 150000),
  ];
  const dias = agruparPorDia(asientos);
  assert.deepEqual(dias.map((d) => [d.dia, d.debe, d.haber, d.asientos.map((a) => a.numero)]), [
    ['2026-10-27', 2359.5, 2359.5, [1]],
    ['2026-10-28', 400, 400, [2]],
    ['2026-10-29', 150000, 150000, [3]],
  ]);
});

test('varios asientos en un día suman juntos y conservan su orden', () => {
  const dias = agruparPorDia([
    asiento(1, '2026-11-01T10:00:00Z', 100.1),
    asiento(2, '2026-11-01T15:30:00Z', 200.2),
    asiento(3, '2026-11-02T09:00:00Z', 50),
    asiento(4, '2026-11-02T18:00:00Z', 0.05),
  ]);
  assert.equal(dias.length, 2);
  assert.deepEqual(dias[0].asientos.map((a) => a.numero), [1, 2]);
  assert.equal(dias[0].debe, 300.3);          // sin el 300.30000000000004 de sumar decimales en coma flotante
  assert.equal(dias[1].debe, 50.05);
});

test('la suma de los totales de cada día da el total general', () => {
  const asientos = Array.from({ length: 60 }, (_, i) => asiento(i + 1, `2026-10-${String(1 + (i % 28)).padStart(2, '0')}T12:00:00Z`, 0.1 * (i + 1), 0.1 * (i + 1)))
    .sort((a, b) => a.fecha.localeCompare(b.fecha) || a.numero - b.numero);
  const dias = agruparPorDia(asientos);
  const general = Math.round(asientos.reduce((s, a) => s + a.totalDebe, 0) * 100) / 100;
  assert.equal(Math.round(dias.reduce((s, d) => s + d.debe, 0) * 100) / 100, general);
  assert.equal(dias.length, 28);
  assert.equal(dias.flatMap((d) => d.asientos).length, 60);
});

test('un asiento con Debe y Haber distintos muestra cada suma por separado', () => {
  const [dia] = agruparPorDia([asiento(1, '2026-11-01', 100, 90)]);
  assert.deepEqual([dia.debe, dia.haber], [100, 90]);
});

test('los totales que faltan cuentan como cero y una lista vacía no tiene días', () => {
  assert.deepEqual(agruparPorDia([]), []);
  const [dia] = agruparPorDia([{ fecha: '2026-11-01', totalDebe: null, totalHaber: undefined }]);
  assert.deepEqual([dia.debe, dia.haber], [0, 0]);
});

test('un día que vuelve a aparecer después de otro forma su propio grupo (la lista llega ordenada por fecha)', () => {
  const dias = agruparPorDia([asiento(1, '2026-11-01', 1), asiento(2, '2026-11-02', 2), asiento(3, '2026-11-01', 3)]);
  assert.deepEqual(dias.map((d) => d.dia), ['2026-11-01', '2026-11-02', '2026-11-01']);
});
