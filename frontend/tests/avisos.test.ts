import assert from 'node:assert/strict';
import { test } from 'node:test';
import { avisosNuevos, claveDeAviso, textosDeAtencion, type Novedad } from '../src/utils/avisos.ts';

test('solo cuentan como avisos las novedades que requieren atención', () => {
  const novedades: Novedad[] = [
    { nivel: 'info', texto: '3 cuotas vencen en los próximos 7 días.' },
    { nivel: 'atencion', texto: '2 productos sin stock: Creatina, Barra.' },
    { nivel: 'ok', texto: 'Contabilidad verificada: 16 controles correctos.' },
    { nivel: 'atencion', texto: '1 rutina vencida: hay que renovarla.' },
  ];
  assert.deepEqual(textosDeAtencion(novedades), ['2 productos sin stock: Creatina, Barra.', '1 rutina vencida: hay que renovarla.']);
});

test('con datos faltantes o raros no se rompe y no hay avisos', () => {
  assert.deepEqual(textosDeAtencion(null), []);
  assert.deepEqual(textosDeAtencion(undefined), []);
  assert.deepEqual(textosDeAtencion([]), []);
  const raros = [null, { nivel: 'atencion' }, { nivel: 'atencion', texto: '   ' }, { nivel: 'atencion', texto: 42 }] as unknown as Novedad[];
  assert.deepEqual(textosDeAtencion(raros), []);
});

test('la clave del aviso ignora los importes y las cantidades', () => {
  assert.equal(
    claveDeAviso('La Caja está en negativo (-$ 1.234,50): revisá los movimientos de caja.'),
    claveDeAviso('La Caja está en negativo (-$ 99,00): revisá los movimientos de caja.'),
  );
  assert.equal(claveDeAviso('1 rutina vencida: hay que renovarla.'), claveDeAviso('7 rutina vencida: hay que renovarla.'));
  assert.notEqual(claveDeAviso('La Caja está en negativo (-$ 5,00).'), claveDeAviso('1 rutina vencida: hay que renovarla.'));
});

test('solo se muestran los avisos que no estaban, sin repetir y en orden', () => {
  const vistos = new Set([claveDeAviso('La Caja está en negativo (-$ 1.000,00).')]);
  const ahora = [
    'La Caja está en negativo (-$ 1.500,00).',   // el mismo aviso con otro importe: no se repite
    '2 rutinas vencidas: hay que renovarlas.',    // nuevo
    '5 rutinas vencidas: hay que renovarlas.',    // mismo aviso que el anterior: una sola vez
    '1 producto sin stock: Creatina.',            // nuevo
  ];
  assert.deepEqual(avisosNuevos(vistos, ahora), ['2 rutinas vencidas: hay que renovarlas.', '1 producto sin stock: Creatina.']);
  assert.deepEqual(avisosNuevos(new Set(ahora.map(claveDeAviso)), ahora), []);
});
