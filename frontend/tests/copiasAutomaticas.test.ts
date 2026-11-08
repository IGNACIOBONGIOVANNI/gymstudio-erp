import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  DIAS_DE_LA_SEMANA, FRECUENCIAS, horaComoTexto, parsearHora, programacionesIguales, resumenDeProgramacion, tamanoLegible, textoDeFecha,
  validarProgramacion, type Programacion,
} from '../src/features/configuracion/copiasAutomaticasUtil.ts';

const diaria: Programacion = { activa: true, frecuencia: 'Diaria', hora: 22, minuto: 0, diaDeLaSemana: null, diaDelMes: null, conservar: 14 };
const con = (cambios: Partial<Programacion>): Programacion => ({ ...diaria, ...cambios });

test('las frecuencias son las cinco del servidor, con su texto', () => {
  assert.deepEqual(FRECUENCIAS.map((f) => f.valor), ['CadaSeisHoras', 'CadaDoceHoras', 'Diaria', 'Semanal', 'Mensual']);
  assert.deepEqual(FRECUENCIAS.map((f) => f.texto), ['Cada 6 horas', 'Cada 12 horas', 'Todos los días', 'Una vez por semana', 'Una vez por mes']);
  assert.deepEqual(DIAS_DE_LA_SEMANA.map((d) => d.numero), [1, 2, 3, 4, 5, 6, 7]);
  assert.equal(DIAS_DE_LA_SEMANA[0].nombre, 'Lunes');
  assert.equal(DIAS_DE_LA_SEMANA[6].nombre, 'Domingo');
});

test('una programación válida no tiene errores, en ninguna frecuencia', () => {
  assert.deepEqual(validarProgramacion(diaria), {});
  assert.deepEqual(validarProgramacion(con({ frecuencia: 'CadaSeisHoras', hora: 0, minuto: 59, conservar: 1 })), {});
  assert.deepEqual(validarProgramacion(con({ frecuencia: 'Semanal', diaDeLaSemana: 7, conservar: 60 })), {});
  assert.deepEqual(validarProgramacion(con({ frecuencia: 'Mensual', diaDelMes: 28 })), {});
});

test('la hora, los minutos y las copias a conservar tienen su rango y el mensaje lo dice', () => {
  assert.equal(validarProgramacion(con({ hora: 24 })).hora, 'La hora va de 0 a 23.');
  assert.equal(validarProgramacion(con({ hora: -1 })).hora, 'La hora va de 0 a 23.');
  assert.equal(validarProgramacion(con({ hora: 1.5 })).hora, 'La hora va de 0 a 23.');
  assert.equal(validarProgramacion(con({ hora: Number.NaN })).hora, 'La hora va de 0 a 23.');
  assert.equal(validarProgramacion(con({ minuto: 60 })).minuto, 'Los minutos van de 0 a 59.');
  assert.equal(validarProgramacion(con({ conservar: 0 })).conservar, 'Las copias a conservar van de 1 a 60.');
  assert.equal(validarProgramacion(con({ conservar: 61 })).conservar, 'Las copias a conservar van de 1 a 60.');
});

test('el día de la semana solo se pide en la semanal y el del mes solo en la mensual (del 1 al 28)', () => {
  assert.equal(validarProgramacion(con({ frecuencia: 'Semanal', diaDeLaSemana: null })).diaDeLaSemana, 'Elegí el día de la semana.');
  assert.equal(validarProgramacion(con({ frecuencia: 'Semanal', diaDeLaSemana: 8 })).diaDeLaSemana, 'El día de la semana va de 1 (lunes) a 7 (domingo).');
  assert.equal(validarProgramacion(con({ frecuencia: 'Mensual', diaDelMes: null })).diaDelMes, 'Elegí el día del mes.');
  for (const dia of [0, 29, 31]) assert.equal(validarProgramacion(con({ frecuencia: 'Mensual', diaDelMes: dia })).diaDelMes, 'El día del mes va de 1 a 28.');
  // Un día que no corresponde a la frecuencia elegida se ignora.
  assert.deepEqual(validarProgramacion(con({ frecuencia: 'Diaria', diaDeLaSemana: 99, diaDelMes: 99 })), {});
  assert.deepEqual(validarProgramacion(con({ frecuencia: 'Semanal', diaDeLaSemana: 3, diaDelMes: 99 })), {});
});

test('la hora se escribe y se lee como HH:MM', () => {
  assert.equal(horaComoTexto(2, 5), '02:05');
  assert.equal(horaComoTexto(22, 30), '22:30');
  assert.deepEqual(parsearHora('22:30'), { hora: 22, minuto: 30 });
  assert.deepEqual(parsearHora('00:00'), { hora: 0, minuto: 0 });
  for (const mal of ['', '2230', '24:00', '12:60', 'ab:cd', '1:5']) assert.equal(parsearHora(mal), null, mal);
});

test('el resumen explica la programación en una frase', () => {
  assert.equal(resumenDeProgramacion(diaria), 'Todos los días a las 22:00');
  assert.equal(resumenDeProgramacion(con({ frecuencia: 'CadaSeisHoras', hora: 2 })), 'Cada 6 horas desde las 02:00 (02:00, 08:00, 14:00 y 20:00)');
  assert.equal(resumenDeProgramacion(con({ frecuencia: 'CadaSeisHoras', hora: 22 })), 'Cada 6 horas desde las 22:00 (04:00, 10:00, 16:00 y 22:00)');
  assert.equal(resumenDeProgramacion(con({ frecuencia: 'CadaDoceHoras', hora: 6, minuto: 30 })), 'Cada 12 horas desde las 06:30 (06:30 y 18:30)');
  assert.equal(resumenDeProgramacion(con({ frecuencia: 'Semanal', hora: 8, minuto: 30, diaDeLaSemana: 1 })), 'Los lunes a las 08:30');
  assert.equal(resumenDeProgramacion(con({ frecuencia: 'Semanal', diaDeLaSemana: 3 })), 'Los miércoles a las 22:00');
  assert.equal(resumenDeProgramacion(con({ frecuencia: 'Semanal', diaDeLaSemana: 6 })), 'Los sábados a las 22:00');
  assert.equal(resumenDeProgramacion(con({ frecuencia: 'Semanal', diaDeLaSemana: 7 })), 'Los domingos a las 22:00');
  assert.equal(resumenDeProgramacion(con({ frecuencia: 'Mensual', diaDelMes: 5 })), 'El día 5 de cada mes a las 22:00');
});

test('dos programaciones son iguales si coinciden en lo que importa para su frecuencia', () => {
  assert.equal(programacionesIguales(diaria, { ...diaria }), true);
  assert.equal(programacionesIguales(diaria, con({ hora: 21 })), false);
  assert.equal(programacionesIguales(diaria, con({ activa: false })), false);
  assert.equal(programacionesIguales(diaria, con({ conservar: 15 })), false);
  assert.equal(programacionesIguales(diaria, con({ diaDelMes: 9, diaDeLaSemana: 2 })), true);   // en la diaria esos días no cuentan
  assert.equal(programacionesIguales(con({ frecuencia: 'Semanal', diaDeLaSemana: 1 }), con({ frecuencia: 'Semanal', diaDeLaSemana: 2 })), false);
  assert.equal(programacionesIguales(con({ frecuencia: 'Mensual', diaDelMes: 1 }), con({ frecuencia: 'Mensual', diaDelMes: 2 })), false);
});

test('las fechas se muestran en hora de Argentina y sin fecha se ve una raya', () => {
  const texto = textoDeFecha('2026-11-08T01:00:00Z');   // 01:00 UTC del 8/11 son las 22:00 del 7/11 en Argentina
  assert.ok(texto.includes('22:00'), texto);
  assert.ok(texto.includes('7/11/2026'), texto);
  assert.equal(textoDeFecha('2026-11-07T22:00:00-03:00').includes('22:00'), true);
  assert.equal(textoDeFecha(null), '—');
  assert.equal(textoDeFecha('no es una fecha'), '—');
});

test('el tamaño se muestra en B, KB o MB con una coma decimal', () => {
  assert.equal(tamanoLegible(0), '0 B');
  assert.equal(tamanoLegible(532), '532 B');
  assert.equal(tamanoLegible(1024), '1,0 KB');
  assert.equal(tamanoLegible(12595), '12,3 KB');
  assert.equal(tamanoLegible(1_048_576), '1,0 MB');
  assert.equal(tamanoLegible(4_300_000), '4,1 MB');
});
