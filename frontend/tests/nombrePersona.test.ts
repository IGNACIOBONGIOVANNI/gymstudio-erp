import assert from 'node:assert/strict';
import { test } from 'node:test';
import { errorDeNombreDePersona, limpiarNombreDePersona, soloLetrasDelNombre } from '../src/utils/nombrePersona.ts';

test('lo escrito o pegado en el nombre queda solo con letras, espacios, puntos, apóstrofos y guiones', () => {
  const casos: [string, string][] = [
    ['Ana López', 'Ana López'],
    ['María José', 'María José'],
    ['Pérez-Gómez', 'Pérez-Gómez'],
    ["D'Angelo", "D'Angelo"],
    ['O’Connor', 'O’Connor'],
    ['Lic. Roberto Gómez', 'Lic. Roberto Gómez'],
    ['Ñandú Güemes', 'Ñandú Güemes'],
    ['Ana2', 'Ana'],
    ['4na', 'na'],
    ['Ana 25', 'Ana '],
    ['jefe@gym.com', 'jefegym.com'],
    ['Ana <b>López</b>', 'Ana bLópezb'],
    ['Ana; DROP TABLE x', 'Ana DROP TABLE x'],
    ['Ana😀', 'Ana'],
    ['١٢٣', ''],
    ['123', ''],
    ['', ''],
  ];
  for (const [escrito, queda] of casos) assert.equal(soloLetrasDelNombre(escrito), queda, escrito);
});

test('el nombre no puede empezar con un signo y los espacios raros pasan a uno común', () => {
  assert.equal(soloLetrasDelNombre('-Ana'), 'Ana');
  assert.equal(soloLetrasDelNombre("'.Ana"), 'Ana');
  assert.equal(soloLetrasDelNombre('  Ana'), 'Ana');
  assert.equal(soloLetrasDelNombre('Ana   López'), 'Ana López');
  assert.equal(soloLetrasDelNombre(`Ana${String.fromCharCode(0x00A0)}López`), 'Ana López');   // espacio duro de pegar desde WhatsApp
  assert.equal(soloLetrasDelNombre(`Ana${String.fromCharCode(0x3000)}López`), 'Ana López');   // espacio ideográfico
  assert.equal(soloLetrasDelNombre('Ana\tLópez\n'), 'Ana López ');
});

test('mientras se escribe se conserva el espacio del final (si no, no se podría escribir el apellido)', () => {
  assert.equal(soloLetrasDelNombre('Ana '), 'Ana ');
  assert.equal(limpiarNombreDePersona('  Ana López  '), 'Ana López');
});

test('la tilde suelta (como la dejan algunos teclados) se conserva', () => {
  const tilde = String.fromCharCode(0x0301);
  const descompuesto = `Jose${tilde} Pe${tilde}rez`;
  assert.equal(soloLetrasDelNombre(descompuesto), descompuesto);
});

test('errorDeNombreDePersona: vacío vale (lo obligatorio lo controla el formulario) y el resto tiene que ser un nombre', () => {
  for (const bueno of ['', '  ', 'Ana', 'Ana López', 'Lic. Ana D’Angelo-Pérez']) {
    assert.equal(errorDeNombreDePersona(bueno), null, JSON.stringify(bueno));
  }
  for (const malo of ['Ana2', 'Ana@', '-Ana', '.Ana', '123', 'Ana_López', 'Ana <b>']) {
    assert.equal(errorDeNombreDePersona(malo), 'El nombre solo puede tener letras, espacios, puntos, apóstrofos y guiones.', malo);
  }
  assert.match(errorDeNombreDePersona('Gómez 3', 'El apellido') ?? '', /^El apellido solo puede tener letras/);
});
