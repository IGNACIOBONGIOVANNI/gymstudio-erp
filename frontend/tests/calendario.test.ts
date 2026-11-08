import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  DIAS, HORAS, LARGO_MAXIMO_DEL_NOMBRE, aplicarRango, celdasDelRango, clave, encargadosDistintos, esCeldaValida, filasParaMostrar,
  grillaDesdeTurnos, grillasIguales, horasPorEncargado, limpiarNombre, nombreDelDia, ponerEncargado, problemaDelRango,
  textoDeHora, textoDelTurno, turnosDesdeGrilla, turnosQueSePisan, type Grilla,
  buscarEntrenador, canonizarGrilla, celdasSinEntrenador, turnoDesdeServidor, turnosParaGuardar, type Entrenador,
} from '../src/features/calendario/calendarioUtil.ts';

test('la semana es de lunes a viernes y de 06 a 22: 5 días por 16 turnos de una hora', () => {
  assert.deepEqual(DIAS.map((d) => d.nombre), ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes']);
  assert.equal(HORAS.length, 16);
  assert.equal(HORAS[0], 6);
  assert.equal(HORAS[HORAS.length - 1], 21);
  assert.equal(textoDeHora(6), '06:00');
  assert.equal(textoDelTurno(6), '06:00 a 07:00');
  assert.equal(textoDelTurno(21), '21:00 a 22:00');   // el último turno cierra a las 22
  assert.equal(nombreDelDia(3), 'Miércoles');
});

test('las celdas válidas son las de la semana y nada más', () => {
  assert.equal(esCeldaValida(1, 6), true);
  assert.equal(esCeldaValida(5, 21), true);
  for (const [d, h] of [[0, 8], [6, 8], [1, 5], [1, 22], [1.5, 8], [1, 8.5], [NaN, 8]]) assert.equal(esCeldaValida(d, h), false, `${d}-${h}`);
});

test('el nombre se limpia: sin espacios repetidos, sin caracteres de control y con un largo máximo', () => {
  assert.equal(limpiarNombre('  Ana   María  '), 'Ana María');
  assert.equal(limpiarNombre('Ana\u0000Beto\n'), 'Ana Beto');
  assert.equal(limpiarNombre(null), '');
  assert.equal(limpiarNombre('x'.repeat(200)).length, LARGO_MAXIMO_DEL_NOMBRE);
  assert.equal(limpiarNombre("<b>'; --"), "<b>'; --");   // el texto se acepta tal cual: se guarda con parámetros y se muestra escapado
});

test('de la API a la grilla y de vuelta: solo viajan las celdas con una persona, en orden', () => {
  const grilla = grillaDesdeTurnos([
    { dia: 5, hora: 21, encargado: 'Ana' },
    { dia: 1, hora: 7, encargado: ' Beto ' },
    { dia: 1, hora: 6, encargado: 'Beto' },
    { dia: 9, hora: 6, encargado: 'Fuera de la semana' },
    { dia: 2, hora: 8, encargado: '   ' },
  ]);
  assert.deepEqual(Object.keys(grilla).sort(), ['1-6', '1-7', '5-21']);
  assert.deepEqual(turnosDesdeGrilla(grilla), [
    { dia: 1, hora: 6, encargado: 'Beto' },
    { dia: 1, hora: 7, encargado: 'Beto' },
    { dia: 5, hora: 21, encargado: 'Ana' },
  ]);
});

test('escribir en una celda la carga y dejarla en blanco la libera', () => {
  let g: Grilla = {};
  g = ponerEncargado(g, 2, 9, 'Carlos');
  assert.equal(g[clave(2, 9)], 'Carlos');
  g = ponerEncargado(g, 2, 9, '   ');
  assert.deepEqual(g, {});
  const original: Grilla = { '1-6': 'Ana' };
  ponerEncargado(original, 1, 6, 'Otro');
  assert.deepEqual(original, { '1-6': 'Ana' });   // no modifica la que recibe
});

test('comparar grillas ignora espacios y orden', () => {
  assert.equal(grillasIguales({ '1-6': 'Ana', '2-7': 'Beto' }, { '2-7': ' Beto ', '1-6': 'Ana' }), true);
  assert.equal(grillasIguales({ '1-6': 'Ana' }, { '1-6': 'Beto' }), false);
  assert.equal(grillasIguales({ '1-6': 'Ana' }, {}), false);
  assert.equal(grillasIguales({}, { '1-6': '   ' }), true);
});

test('un rango abarca los días elegidos y las horas desde el inicio hasta antes del fin', () => {
  const celdas = celdasDelRango({ encargado: 'Ana', dias: [3, 1], desde: 8, hasta: 11 });
  assert.deepEqual(celdas.map((c) => clave(c.dia, c.hora)), ['1-8', '1-9', '1-10', '3-8', '3-9', '3-10']);
  // De 21:00 a 22:00 es un solo turno; el rango no puede pasar de las 22.
  assert.equal(celdasDelRango({ encargado: 'Ana', dias: [1], desde: 21, hasta: 22 }).length, 1);
  assert.equal(celdasDelRango({ encargado: 'Ana', dias: [1], desde: 20, hasta: 30 }).length, 2);
  assert.equal(celdasDelRango({ encargado: 'Ana', dias: [1, 1, 1], desde: 6, hasta: 7 }).length, 1);
});

test('aplicar un rango carga a la persona, pisa lo anterior y no toca el resto', () => {
  const antes: Grilla = { '1-8': 'Beto', '1-12': 'Beto', '2-8': 'Carla' };
  const rango = { encargado: ' Ana ', dias: [1], desde: 8, hasta: 10 };
  assert.equal(turnosQueSePisan(antes, rango), 1);   // 1-8 era de Beto; 1-9 estaba libre
  const despues = aplicarRango(antes, rango);
  assert.deepEqual(despues, { '1-8': 'Ana', '1-9': 'Ana', '1-12': 'Beto', '2-8': 'Carla' });
  assert.deepEqual(antes, { '1-8': 'Beto', '1-12': 'Beto', '2-8': 'Carla' });   // no modifica la que recibe
  assert.equal(turnosQueSePisan(despues, rango), 0);   // volver a aplicar lo mismo no pisa a nadie
});

test('un rango con el nombre vacío deja libres las celdas', () => {
  const despues = aplicarRango({ '1-8': 'Beto', '1-9': 'Beto', '1-10': 'Beto' }, { encargado: '', dias: [1], desde: 8, hasta: 10 });
  assert.deepEqual(despues, { '1-10': 'Beto' });
});

test('el rango dice qué le falta antes de aplicarse', () => {
  assert.match(problemaDelRango({ encargado: '', dias: [1], desde: 8, hasta: 10 })!, /nombre/);
  assert.equal(problemaDelRango({ encargado: '', dias: [1], desde: 8, hasta: 10 }, true), null);
  assert.match(problemaDelRango({ encargado: 'Ana', dias: [], desde: 8, hasta: 10 })!, /día/);
  assert.match(problemaDelRango({ encargado: 'Ana', dias: [1], desde: 10, hasta: 10 })!, /posterior/);
  assert.match(problemaDelRango({ encargado: 'Ana', dias: [1], desde: 12, hasta: 9 })!, /posterior/);
  assert.equal(problemaDelRango({ encargado: 'Ana', dias: [1], desde: 8, hasta: 9 }), null);
});

test('la misma persona en horas seguidas se muestra como un solo bloque y lo que cubre queda en null', () => {
  const grilla: Grilla = { '1-6': 'Ana', '1-7': 'Ana', '1-8': 'Ana', '1-9': 'Beto', '1-10': 'Ana', '2-6': 'Ana' };
  const filas = filasParaMostrar(grilla);
  assert.equal(filas.length, 16);
  const lunes = (hora: number) => filas.find((f) => f.hora === hora)!.celdas[0];
  assert.deepEqual(lunes(6), { texto: 'Ana', filas: 3 });
  assert.equal(lunes(7), null);
  assert.equal(lunes(8), null);
  assert.deepEqual(lunes(9), { texto: 'Beto', filas: 1 });
  assert.deepEqual(lunes(10), { texto: 'Ana', filas: 1 });   // Ana de nuevo, pero no seguida: otro bloque
  assert.deepEqual(lunes(11), { texto: '', filas: 1 });
  // Otro día no se mezcla con el lunes.
  assert.deepEqual(filas[0].celdas[1], { texto: 'Ana', filas: 1 });
});

test('un bloque no se pasa de la última hora', () => {
  const grilla: Grilla = { '5-20': 'Ana', '5-21': 'Ana' };
  const filas = filasParaMostrar(grilla);
  assert.deepEqual(filas.find((f) => f.hora === 20)!.celdas[4], { texto: 'Ana', filas: 2 });
  assert.equal(filas.find((f) => f.hora === 21)!.celdas[4], null);
});

test('cada fila de lo mostrado cubre exactamente 5 días y las horas cubiertas suman las ocupadas', () => {
  const grilla: Grilla = { '1-6': 'Ana', '1-7': 'Ana', '3-10': 'Beto', '3-11': 'Beto', '3-12': 'Beto', '5-21': 'Carla' };
  const filas = filasParaMostrar(grilla);
  for (const f of filas) assert.equal(f.celdas.length, 5);
  const ocupadas = filas.flatMap((f) => f.celdas).reduce((s, c) => s + (c && c.texto ? c.filas : 0), 0);
  assert.equal(ocupadas, 6);
});

test('los nombres distintos y las horas de cada persona', () => {
  const grilla: Grilla = { '1-6': 'Beto', '1-7': 'Ana', '2-6': 'Ana', '3-6': 'Ana' };
  assert.deepEqual(encargadosDistintos(grilla), ['Ana', 'Beto']);
  assert.deepEqual(horasPorEncargado(grilla), [{ encargado: 'Ana', horas: 3 }, { encargado: 'Beto', horas: 1 }]);
  assert.deepEqual(horasPorEncargado({}), []);
});

// ---------- entrenadores: la grilla solo acepta nombres de fichas activas ----------

const ficha = (id: number, nombre: string, activo = true): Entrenador =>
  ({ id, nombre, telefono: null, especialidad: null, email: null, whatsApp: null, activo, horasSemanales: 0, tieneFoto: false, fotoActualizada: null, valorHora: null });
const entrenadores = [ficha(1, 'Ana López'), ficha(2, 'Beto'), ficha(3, 'Carla', false)];

test('buscarEntrenador encuentra sin distinguir mayúsculas ni espacios repetidos, pero los acentos sí cuentan', () => {
  assert.equal(buscarEntrenador(entrenadores, 'ana   LÓPEZ')?.id, 1);
  assert.equal(buscarEntrenador(entrenadores, '  Beto ')?.id, 2);
  assert.equal(buscarEntrenador(entrenadores, 'Ana Lopez'), undefined);
  assert.equal(buscarEntrenador(entrenadores, ''), undefined);
  assert.equal(buscarEntrenador(entrenadores, '   '), undefined);
});

test('buscarEntrenador ignora a los desactivados', () => {
  assert.equal(buscarEntrenador(entrenadores, 'Carla'), undefined);
});

test('celdasSinEntrenador lista cada nombre ajeno una sola vez y en orden', () => {
  const grilla: Grilla = { '1-6': 'Zeta', '1-7': 'Zeta', '2-6': 'ana lópez', '2-7': 'Carla', '3-6': 'Alfa' };
  assert.deepEqual(celdasSinEntrenador(grilla, entrenadores), ['Alfa', 'Carla', 'Zeta']);
  assert.deepEqual(celdasSinEntrenador({}, entrenadores), []);
  assert.deepEqual(celdasSinEntrenador({ '1-6': 'Beto' }, []), ['Beto']);
});

test('canonizarGrilla deja la grafía de la ficha y respeta lo que no coincide', () => {
  const grilla: Grilla = { '1-6': 'ana  lópez', '2-6': 'Intruso', '3-6': 'BETO' };
  assert.deepEqual(canonizarGrilla(grilla, entrenadores), { '1-6': 'Ana López', '2-6': 'Intruso', '3-6': 'Beto' });
});

test('turnosParaGuardar devuelve día, hora e id del entrenador, en orden', () => {
  const grilla: Grilla = { '2-7': 'Beto', '1-6': 'ana lópez', '1-7': 'Ana López' };
  assert.deepEqual(turnosParaGuardar(grilla, entrenadores), [
    { dia: 1, hora: 6, entrenadorId: 1 },
    { dia: 1, hora: 7, entrenadorId: 1 },
    { dia: 2, hora: 7, entrenadorId: 2 },
  ]);
  assert.deepEqual(turnosParaGuardar({}, entrenadores), []);
});

test('turnosParaGuardar lanza un Error con un nombre que no es entrenador activo', () => {
  assert.throws(() => turnosParaGuardar({ '1-6': 'Intruso' }, entrenadores), /Intruso/);
  assert.throws(() => turnosParaGuardar({ '1-6': 'Carla' }, entrenadores), /Carla/);
});

test('turnoDesdeServidor usa el nombre del entrenador como encargado de la celda', () => {
  assert.deepEqual(turnoDesdeServidor({ dia: 1, hora: 6, entrenadorId: 5, entrenador: '  Ana   López ' }), { dia: 1, hora: 6, encargado: '  Ana   López ' });
  assert.deepEqual(grillaDesdeTurnos([turnoDesdeServidor({ dia: 3, hora: 8, entrenadorId: 5, entrenador: 'Ana' })]), { '3-8': 'Ana' });
});
