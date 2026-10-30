import assert from 'node:assert/strict';
import { test } from 'node:test';
import { REQUISITOS, claveValida, requisitosQueFaltan, tieneNumerosConsecutivos } from '../src/features/usuarios/reglasDeClave.ts';

const faltan = (clave: string) => requisitosQueFaltan(clave).map((r) => r.id);

test('una clave que cumple todo se acepta', () => {
  for (const clave of ['Clave-De-Prueba-17!', 'Abcdef1!', 'Zeta#19Zeta', 'Mi clave 7 segura!', 'Ñandú#4Ñandú', 'Aa1!1111', 'Ab!1x3y5z']) {
    assert.deepEqual(faltan(clave), [], clave);
    assert.equal(claveValida(clave), true, clave);
  }
});

test('cada requisito se detecta por separado', () => {
  assert.deepEqual(faltan('Ab1!'), ['largo']);
  assert.deepEqual(faltan('abcdef1!'), ['mayuscula']);
  assert.deepEqual(faltan('Abcdefg!'), ['numero']);
  assert.deepEqual(faltan('Abcdefg1'), ['especial']);
  assert.deepEqual(faltan('Abcd12!x'), ['consecutivos']);
});

test('un espacio no cuenta como carácter especial', () => {
  assert.deepEqual(faltan('Abc defg 1'), ['especial']);
});

test('los números consecutivos se detectan hacia arriba y hacia abajo, y los repetidos o salteados no cuentan', () => {
  for (const c of ['12', 'a45b', '98', 'x21y', '7890', '0123']) assert.equal(tieneNumerosConsecutivos(c), true, c);
  for (const c of ['11', '13', '1a2', '19', '90', '']) assert.equal(tieneNumerosConsecutivos(c), false, c);
});

test('una clave vacía o demasiado larga no es válida', () => {
  assert.equal(claveValida(''), false);
  assert.equal(claveValida('A1!x' + 'x'.repeat(200)), false);
  assert.equal(claveValida('A1!x' + 'x'.repeat(124)), true);
});

test('la lista de requisitos que se muestra tiene los cinco puntos de la política', () => {
  assert.deepEqual(REQUISITOS.map((r) => r.id), ['largo', 'mayuscula', 'numero', 'especial', 'consecutivos']);
});
