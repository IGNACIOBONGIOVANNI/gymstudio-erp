import assert from 'node:assert/strict';
import { test } from 'node:test';
import { bloquearNoNumericas, caracteresDeTelefono, esTeclaNoNumerica, limpiarRepeticiones, numeroDecimal, repeticionesValidas, soloCuit, soloDigitos } from '../src/utils/entradaNumerica.ts';

test('soloDigitos quita letras, signos, espacios y puntos, y respeta el largo', () => {
  assert.equal(soloDigitos('30.111.222'), '30111222');
  assert.equal(soloDigitos('12abc-3 4e5'), '12345');
  assert.equal(soloDigitos('123456789', 5), '12345');
  assert.equal(soloDigitos(''), '');
});

test('numeroDecimal deja un número positivo con un solo separador y dos decimales', () => {
  assert.equal(numeroDecimal('1500'), '1500');
  assert.equal(numeroDecimal('1500,5'), '1500.5');
  assert.equal(numeroDecimal('1500.567'), '1500.56');   // 4 cifras antes del punto: es decimal
  assert.equal(numeroDecimal('12.3.4'), '12.34');
  assert.equal(numeroDecimal('-25'), '25');
  assert.equal(numeroDecimal('1e5'), '15');
  assert.equal(numeroDecimal('$ 2500'), '2500');
  assert.equal(numeroDecimal(',5'), '0.5');
  assert.equal(numeroDecimal('abc'), '');
});

test('numeroDecimal respeta la cantidad de decimales pedida y el largo', () => {
  assert.equal(numeroDecimal('70,25', 1), '70.2');
  assert.equal(numeroDecimal('8,5', 0), '85');
  assert.equal(numeroDecimal('12345678901234567890'), '123456789012345');
});

test('numeroDecimal entiende el punto de miles como se escribe en Argentina', () => {
  assert.equal(numeroDecimal('25.000'), '25000');
  assert.equal(numeroDecimal('25.000,5'), '25000.5');
  assert.equal(numeroDecimal('1.500.000'), '1500000');
  assert.equal(numeroDecimal('1.500.000,75'), '1500000.75');
  assert.equal(numeroDecimal('1500.50'), '1500.50');
  assert.equal(numeroDecimal('12345.678'), '12345.67');
  // Letra por letra, como al tipear: «25.00» todavía es decimal y la tercera cifra lo convierte en miles.
  let escrito = '';
  for (const c of '25.000') escrito = numeroDecimal(escrito + c);
  assert.equal(escrito, '25000');
});

test('numeroDecimal puede conservar el separador como se escribió', () => {
  assert.equal(numeroDecimal('1500,505', 2, 15, ','), '1500,50');
  assert.equal(numeroDecimal('7.5', 2, 15, ','), '7.5');
  assert.equal(numeroDecimal('80,5kg', 1, 15, ','), '80,5');
  assert.equal(numeroDecimal('1.500', 2, 16, ','), '1500');
  assert.equal(numeroDecimal('1.500,50', 2, 16, ','), '1500,50');
});

test('limpiarRepeticiones deja un número o un rango con un solo guion', () => {
  assert.equal(limpiarRepeticiones('12'), '12');
  assert.equal(limpiarRepeticiones('8 - 10'), '8-10');
  assert.equal(limpiarRepeticiones('30 seg'), '30');
  assert.equal(limpiarRepeticiones('-10'), '10');
  assert.equal(limpiarRepeticiones('8-10-12'), '8-10');
  assert.equal(limpiarRepeticiones('1234'), '123');
  assert.equal(limpiarRepeticiones('8–10'), '8-10');
});

test('repeticionesValidas acepta un número o un rango de menor a mayor', () => {
  for (const v of ['12', '8-10', '1', '100-200']) assert.equal(repeticionesValidas(v), true, v);
  for (const v of ['', '0', '10-', '12-8', '10-10', '30 seg', '1000', '10.5']) assert.equal(repeticionesValidas(v), false, v);
});

test('soloCuit deja dígitos y guiones, sin guiones repetidos, hasta 13 caracteres', () => {
  assert.equal(soloCuit('30-71234567-8'), '30-71234567-8');
  assert.equal(soloCuit('30 71234567 8'), '30712345678');
  assert.equal(soloCuit('30--712abc'), '30-712');
  assert.equal(soloCuit('30-71234567-89999'), '30-71234567-8');
});

test('en los campos type=number se bloquean e, signos y, si son enteros, el separador', () => {
  for (const t of ['e', 'E', '+', '-']) assert.equal(esTeclaNoNumerica(t, true), true, t);
  assert.equal(esTeclaNoNumerica('.', true), false);
  assert.equal(esTeclaNoNumerica('.', false), true);
  assert.equal(esTeclaNoNumerica(',', false), true);
  for (const t of ['5', 'Backspace', 'ArrowLeft', 'Tab']) assert.equal(esTeclaNoNumerica(t, false), false, t);
  let bloqueada = false;
  bloquearNoNumericas(false)({ key: 'e', preventDefault: () => { bloqueada = true; } });
  assert.equal(bloqueada, true);
});

test('caracteresDeTelefono deja solo lo que puede tener un teléfono', () => {
  assert.equal(caracteresDeTelefono('+54 (11) 5555-0101'), '+54 (11) 5555-0101');
  assert.equal(caracteresDeTelefono('11 5555 abc 0101 int. 2'), '11 5555  0101 . 2');
});
