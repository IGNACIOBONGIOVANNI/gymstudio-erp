import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  hojasDelTablero, simular, textoDeValor, textoDeVariacion, type Analisis, type IndicadoresDelMes, type PlanParaSimular,
} from '../src/features/tablero/tableroUtil.ts';

const plan = (id: number, precioMensual: number, duracionMeses: number, sociosVigentes: number): PlanParaSimular => ({
  id, nombre: `Plan ${id}`, precioMensual, duracionMeses, sociosVigentes,
  recaudacionMensual: Math.round((precioMensual / duracionMeses) * sociosVigentes * 100) / 100,
});

test('el simulador proyecta la recaudación mensual con el aumento de cada plan y la pérdida estimada de socios', () => {
  const r = simular([plan(1, 30000, 3, 2)], { 1: 10 }, 50);
  assert.deepEqual(r, { actual: 20000, proyectada: 11000, diferencia: -9000 });
});

test('sin aumentos ni pérdida, la proyección es igual a lo que se recauda hoy', () => {
  const planes = [plan(1, 30000, 3, 2), plan(2, 18000, 1, 5)];
  const r = simular(planes, {}, 0);
  assert.equal(r.actual, 110000);
  assert.equal(r.proyectada, 110000);
  assert.equal(r.diferencia, 0);
});

test('un aumento o una pérdida fuera de rango se acota: el aumento entre -100 y 1000 %, la pérdida entre 0 y 100 %', () => {
  const planes = [plan(1, 1000, 1, 1)];
  assert.equal(simular(planes, { 1: -500 }, 0).proyectada, 0);
  assert.equal(simular(planes, {}, 300).proyectada, 0);
  assert.equal(simular(planes, { 1: Number.NaN }, Number.NaN).proyectada, 1000);
});

test('la variación se muestra con flecha y en % o en puntos', () => {
  assert.equal(textoDeVariacion(12, false), '▲ 12 %');
  assert.equal(textoDeVariacion(-3, true), '▼ 3 pts');
  assert.equal(textoDeVariacion(2.5, false), '▲ 2,5 %');
  assert.equal(textoDeVariacion(0, false), '= 0 %');
  assert.equal(textoDeVariacion(null, false), '—');
});

test('el valor de un indicador se muestra en pesos, en % o como número, y sin datos con una raya', () => {
  assert.equal(textoDeValor(66.67, true, 'margen'), '66,67 %');
  assert.equal(textoDeValor(null, true, 'margen'), '—');
  assert.equal(textoDeValor(3, false, 'socios-activos'), '3');
  assert.match(textoDeValor(278000, false, 'ingresos'), /^\$\s?278\.000/);
});

test('el Excel del tablero tiene las seis hojas, con los indicadores y el mapa de asistencia', () => {
  const ind: IndicadoresDelMes = {
    mes: '2026-11', mesAnterior: '2026-10', cierre: '2026-11-15',
    indicadores: [{
      clave: 'ingresos', nombre: 'Ingresos (ventas netas)', perspectiva: 'Financiera', sentido: 'mas', esPorcentaje: false, admiteMeta: true,
      valor: 120, anterior: 100, variacion: 20, meta: 150, semaforo: 'rojo', detalle: null,
    }],
  };
  const an: Analisis = {
    granularidad: 'mes',
    evolucion: [{ periodo: '2026-11', ventas: 120, compras: 20, cuotas: 50, resultado: 100 }],
    ingresosPorPlan: [{ plan: 'Pase Libre', neto: 50, cobros: 2 }],
    asistencia: Array.from({ length: 7 }, () => Array.from({ length: 17 }, () => 0)),
    topProductos: [{ producto: 'Agua', cantidad: 3, total: 30 }],
    comprasPorProveedor: [{ proveedor: 'Distribuidora', neto: 20 }],
  };
  const hojas = hojasDelTablero(ind, an);
  assert.deepEqual(hojas.map((h) => h.nombre), ['Indicadores', 'Evolución', 'Ingresos por plan', 'Asistencia', 'Productos', 'Compras por proveedor']);
  assert.equal(hojas[3].filas.length, 1 + 7);       // encabezado + lunes a domingo
  assert.equal(hojas[3].filas[0].length, 1 + 17);   // día + franjas de 06 a 22
  assert.ok(JSON.stringify(hojas[0].filas).includes('Ingresos (ventas netas)'));
});
