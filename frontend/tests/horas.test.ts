import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  borradorDesde, cambiosDelBorrador, claveDeDia, conDiaNuevo, formatoDeHoras, formatoDeImporte, hoyEnArgentina, mensajeDeHoras, mensajeDePeriodo,
  mensajeDeValorHora, nombreDelDia, parsearHoras, parsearValorHora, rangoDelMes, resumenPorEntrenador, type Borrador, type DiaDeHoras,
} from '../src/features/entrenadores/horasUtil.ts';

const dia = (entrenadorId: number, fecha: string, programadas: number, cumplidas: number | null = null, nota: string | null = null, valorHora: number | null = null): DiaDeHoras =>
  ({ entrenadorId, fecha, programadas, cumplidas, valorHora: cumplidas === null ? null : (valorHora ?? 1000), nota });

// ---------- las horas que se escriben ----------

test('parsearHoras: vacío es «sin cargar» y las horas válidas se leen con coma o con punto', () => {
  assert.equal(parsearHoras(''), null);
  assert.equal(parsearHoras('   '), null);
  assert.equal(parsearHoras('7,5'), 7.5);
  assert.equal(parsearHoras('7.5'), 7.5);
  assert.equal(parsearHoras('0'), 0);          // 0 = faltó
  assert.equal(parsearHoras('24'), 24);
  assert.equal(parsearHoras('0,25'), 0.25);
  assert.equal(parsearHoras(' 8 '), 8);
  assert.equal(parsearHoras('8,00'), 8);
});

test('parsearHoras y mensajeDeHoras: fuera de rango, que no sean cuarto de hora o con basura son inválidas con un mensaje', () => {
  for (const mala of ['24,5', '24,25', '-1', '0,1', '0,3', 'abc', '1,2,3', '7,5h', '1e1', ',5', '7,']) {
    assert.equal(parsearHoras(mala), 'invalida', mala);
    assert.equal(mensajeDeHoras(mala), 'Las horas van de 0 a 24, de a cuarto de hora.', mala);
  }
  for (const buena of ['', '7,5', '0', '24']) assert.equal(mensajeDeHoras(buena), null, buena);
});

test('formatoDeHoras: la coma de los números argentinos y sin ceros de más', () => {
  assert.equal(formatoDeHoras(7.5), '7,5');
  assert.equal(formatoDeHoras(8), '8');
  assert.equal(formatoDeHoras(0.25), '0,25');
  assert.equal(formatoDeHoras(0), '0');
});

// ---------- el borrador y lo que se envía ----------

const dias = [
  dia(1, '2026-11-02', 3, 3),                 // cargado igual a lo programado
  dia(1, '2026-11-04', 1),                    // le tocaba y todavía no se cargó
  dia(1, '2026-11-07', 0, 4, 'Un sábado'),    // cargado a mano, con nota
  dia(2, '2026-11-03', 2, 2),
];

test('borradorDesde: lo cargado como se escribe y lo sin cargar vacío', () => {
  const borrador = borradorDesde(dias);
  assert.deepEqual(borrador[claveDeDia(1, '2026-11-02')], { horas: '3', nota: '' });
  assert.deepEqual(borrador[claveDeDia(1, '2026-11-04')], { horas: '', nota: '' });
  assert.deepEqual(borrador[claveDeDia(1, '2026-11-07')], { horas: '4', nota: 'Un sábado' });
  assert.equal(Object.keys(borrador).length, 4);
  assert.equal(borradorDesde([dia(1, '2026-11-02', 1, 7.5)])[claveDeDia(1, '2026-11-02')].horas, '7,5');
});

test('cambiosDelBorrador: sin tocar nada no se envía nada', () => {
  assert.deepEqual(cambiosDelBorrador(dias, borradorDesde(dias)), []);
});

test('cambiosDelBorrador: cambiar las horas de un día cargado es un cambio', () => {
  const borrador = borradorDesde(dias);
  borrador[claveDeDia(1, '2026-11-02')].horas = '9,5';
  assert.deepEqual(cambiosDelBorrador(dias, borrador), [{ entrenadorId: 1, fecha: '2026-11-02', horas: 9.5, nota: null }]);
});

test('cambiosDelBorrador: cargar un día que no tenía registro es un cambio, y 0 (faltó) también', () => {
  const borrador = borradorDesde(dias);
  borrador[claveDeDia(1, '2026-11-04')].horas = '1';
  assert.deepEqual(cambiosDelBorrador(dias, borrador), [{ entrenadorId: 1, fecha: '2026-11-04', horas: 1, nota: null }]);

  borrador[claveDeDia(1, '2026-11-04')].horas = '0';
  assert.deepEqual(cambiosDelBorrador(dias, borrador), [{ entrenadorId: 1, fecha: '2026-11-04', horas: 0, nota: null }]);
});

test('cambiosDelBorrador: vaciar un día cargado lo borra (horas null)', () => {
  const borrador = borradorDesde(dias);
  borrador[claveDeDia(2, '2026-11-03')].horas = '';
  assert.deepEqual(cambiosDelBorrador(dias, borrador), [{ entrenadorId: 2, fecha: '2026-11-03', horas: null, nota: null }]);
});

test('cambiosDelBorrador: cambiar solo la nota es un cambio y una nota de espacios es lo mismo que ninguna', () => {
  const borrador = borradorDesde(dias);
  borrador[claveDeDia(1, '2026-11-02')].nota = '  Cubrió a Beto ';
  assert.deepEqual(cambiosDelBorrador(dias, borrador), [{ entrenadorId: 1, fecha: '2026-11-02', horas: 3, nota: 'Cubrió a Beto' }]);

  borrador[claveDeDia(1, '2026-11-02')].nota = '    ';   // espacios: sigue sin nota, no hay cambio
  assert.deepEqual(cambiosDelBorrador(dias, borrador), []);

  borrador[claveDeDia(1, '2026-11-07')].nota = '   ';   // borrar la nota que tenía es un cambio, y se envía null
  assert.deepEqual(cambiosDelBorrador(dias, borrador), [{ entrenadorId: 1, fecha: '2026-11-07', horas: 4, nota: null }]);
});

test('cambiosDelBorrador: un texto inválido no se envía y una nota sola en un día sin horas tampoco', () => {
  const borrador = borradorDesde(dias);
  borrador[claveDeDia(1, '2026-11-02')].horas = '24,5';
  borrador[claveDeDia(1, '2026-11-04')].nota = 'Nota sin horas';
  assert.deepEqual(cambiosDelBorrador(dias, borrador), []);
});

test('cambiosDelBorrador: varios cambios salen en el orden de los días', () => {
  const borrador = borradorDesde(dias);
  borrador[claveDeDia(2, '2026-11-03')].horas = '1';
  borrador[claveDeDia(1, '2026-11-02')].horas = '2';
  assert.deepEqual(cambiosDelBorrador(dias, borrador).map((c) => [c.entrenadorId, c.fecha]), [[1, '2026-11-02'], [2, '2026-11-03']]);
});

// ---------- agregar un día ----------

test('conDiaNuevo: agrega un renglón con programadas 0, vacío, en su lugar; repetirlo no lo duplica', () => {
  const borrador = borradorDesde(dias);

  const una = conDiaNuevo(dias, borrador, 1, '2026-11-05');
  assert.equal(una.dias.length, 5);
  assert.deepEqual(una.dias.map((d) => `${d.entrenadorId}|${d.fecha}`), ['1|2026-11-02', '1|2026-11-04', '1|2026-11-05', '1|2026-11-07', '2|2026-11-03']);
  assert.deepEqual(una.dias.find((d) => d.fecha === '2026-11-05'), { entrenadorId: 1, fecha: '2026-11-05', programadas: 0, cumplidas: null, valorHora: null, nota: null });
  assert.deepEqual(una.borrador[claveDeDia(1, '2026-11-05')], { horas: '', nota: '' });
  assert.equal(dias.length, 4);   // lo original no cambia

  const repetida = conDiaNuevo(una.dias, una.borrador, 1, '2026-11-05');
  assert.equal(repetida.dias.length, 5);
  const yaExistente = conDiaNuevo(dias, borrador, 1, '2026-11-02');
  assert.equal(yaExistente.dias.length, 4);
});

test('conDiaNuevo: lo ya escrito en el borrador se conserva y un entrenador sin renglones queda al final', () => {
  const borrador: Borrador = borradorDesde(dias);
  borrador[claveDeDia(1, '2026-11-02')].horas = '9';

  const nuevo = conDiaNuevo(dias, borrador, 3, '2026-11-06');

  assert.equal(nuevo.borrador[claveDeDia(1, '2026-11-02')].horas, '9');
  assert.equal(nuevo.dias[nuevo.dias.length - 1].entrenadorId, 3);
});

// ---------- resumen ----------

test('resumenPorEntrenador: suma lo cargado según el borrador (no el original) y lo programado', () => {
  const borrador = borradorDesde(dias);
  assert.deepEqual(resumenPorEntrenador(dias, borrador), [
    { entrenadorId: 1, cargadas: 7, programadas: 4 },   // 3 + 4 (el miércoles sin cargar no suma)
    { entrenadorId: 2, cargadas: 2, programadas: 2 },
  ]);

  borrador[claveDeDia(1, '2026-11-04')].horas = '1';     // cargo el miércoles
  borrador[claveDeDia(2, '2026-11-03')].horas = '24,5';  // un valor inválido no suma
  assert.deepEqual(resumenPorEntrenador(dias, borrador), [
    { entrenadorId: 1, cargadas: 8, programadas: 4 },
    { entrenadorId: 2, cargadas: 0, programadas: 2 },
  ]);
});

// ---------- fechas ----------

test('rangoDelMes: primer y último día, también en febrero y en año bisiesto', () => {
  assert.deepEqual(rangoDelMes('2026-02'), { desde: '2026-02-01', hasta: '2026-02-28' });
  assert.deepEqual(rangoDelMes('2028-02'), { desde: '2028-02-01', hasta: '2028-02-29' });
  assert.deepEqual(rangoDelMes('2026-12'), { desde: '2026-12-01', hasta: '2026-12-31' });
  assert.deepEqual(rangoDelMes('2026-11'), { desde: '2026-11-01', hasta: '2026-11-30' });
});

test('nombreDelDia: abreviatura del día de la semana', () => {
  assert.equal(nombreDelDia('2026-11-09'), 'lun');
  assert.equal(nombreDelDia('2026-11-11'), 'mié');
  assert.equal(nombreDelDia('2026-11-14'), 'sáb');
  assert.equal(nombreDelDia('2026-11-08'), 'dom');
});

test('hoyEnArgentina: a las 22:30 de allá todavía es el mismo día aunque en UTC ya sea el siguiente', () => {
  assert.equal(hoyEnArgentina(new Date('2026-11-13T01:30:00Z')), '2026-11-12');
  assert.equal(hoyEnArgentina(new Date('2026-11-12T03:00:00Z')), '2026-11-12');
  assert.equal(hoyEnArgentina(new Date('2026-11-12T02:59:00Z')), '2026-11-11');
  assert.equal(hoyEnArgentina(new Date('2027-01-01T02:00:00Z')), '2026-12-31');
});

test('mensajeDePeriodo: las dos fechas, en orden y de hasta 62 días', () => {
  assert.equal(mensajeDePeriodo('2026-11-01', '2026-11-30'), null);
  assert.equal(mensajeDePeriodo('2026-11-05', '2026-11-05'), null);
  assert.equal(mensajeDePeriodo('2026-10-01', '2026-12-01'), null);   // 62 días
  assert.match(mensajeDePeriodo('2026-10-01', '2026-12-02') ?? '', /62 días/);   // 63
  assert.match(mensajeDePeriodo('2026-11-10', '2026-11-09') ?? '', /desde/);
  assert.match(mensajeDePeriodo('', '2026-11-09') ?? '', /fechas/);
});

// ---------- importes ----------

test('formatoDeImporte: pesos argentinos con punto de miles y coma decimal', () => {
  assert.equal(formatoDeImporte(1234.5), '$ 1.234,50');
  assert.equal(formatoDeImporte(0), '$ 0,00');
  assert.equal(formatoDeImporte(12000), '$ 12.000,00');
  assert.equal(formatoDeImporte(9259.2), '$ 9.259,20');
  assert.equal(formatoDeImporte(1234567.89), '$ 1.234.567,89');
  assert.equal(formatoDeImporte(25.03), '$ 25,03');
  assert.equal(formatoDeImporte(999999999999), '$ 999.999.999.999,00');
  assert.equal(formatoDeImporte(-1234.5), '-$ 1.234,50');
});

test('parsearValorHora: vacío es «sin valor»; números con hasta 2 decimales; lo demás es inválido con un mensaje', () => {
  assert.equal(parsearValorHora(''), null);
  assert.equal(parsearValorHora('  '), null);
  assert.equal(parsearValorHora('1500'), 1500);
  assert.equal(parsearValorHora('1500,50'), 1500.5);
  assert.equal(parsearValorHora('1500.5'), 1500.5);
  assert.equal(parsearValorHora('0'), 0);
  assert.equal(parsearValorHora('999999999999'), 999999999999);
  for (const malo of ['-1', '1,234', '1.234', 'abc', '1.500,50', '1000000000000', '1,2,3']) {
    assert.equal(parsearValorHora(malo), 'invalido', malo);
    assert.match(mensajeDeValorHora(malo) ?? '', /valor de la hora/i, malo);
  }
  for (const bueno of ['', '1500', '1500,50']) assert.equal(mensajeDeValorHora(bueno), null, bueno);
});
