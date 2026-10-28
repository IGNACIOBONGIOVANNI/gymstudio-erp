import assert from 'node:assert/strict';
import { test } from 'node:test';
import { diasEntre, enlaceWhatsApp, estadoDeCuota, mensajeDeCuota, telefonoParaWhatsApp } from '../src/utils/whatsapp.ts';

test('el teléfono se normaliza al formato de WhatsApp (549 + área + número)', () => {
  const casos: [string, string][] = [
    ['11 5555-1234', '5491155551234'],
    ['1155551234', '5491155551234'],
    ['011 15 5555-1234', '5491155551234'],           // 0 del área y 15 de celular
    ['+54 9 11 5555 1234', '5491155551234'],
    ['+54 11 5555 1234', '5491155551234'],           // sin el 9
    ['5491155551234', '5491155551234'],
    ['0054 9 11 5555 1234', '5491155551234'],
  ];
  // El caso de Córdoba (área de 3 dígitos): 351 + 15 + 7 dígitos -> 351 + 7 dígitos.
  assert.equal(telefonoParaWhatsApp('(0351) 15-555-1234'), '5493515551234');
  for (const [entrada, esperado] of casos) {
    assert.equal(telefonoParaWhatsApp(entrada), esperado, entrada);
  }
});

test('un teléfono que no se puede interpretar devuelve null', () => {
  for (const malo of ['', '   ', '-', 'sin teléfono', '1234', '555-1234', '12345678901234567', null, undefined]) {
    assert.equal(telefonoParaWhatsApp(malo as string | null | undefined), null, String(malo));
  }
});

test('el enlace lleva el mensaje codificado', () => {
  const enlace = enlaceWhatsApp('11 5555-1234', 'Hola Ana, ¿cómo estás?')!;
  assert.ok(enlace.startsWith('https://wa.me/5491155551234?text='));
  assert.equal(decodeURIComponent(enlace.split('?text=')[1]), 'Hola Ana, ¿cómo estás?');
  assert.equal(enlaceWhatsApp('abc', 'x'), null);
});

test('días entre fechas, incluso cruzando mes y año', () => {
  assert.equal(diasEntre('2026-10-27', '2026-10-30'), 3);
  assert.equal(diasEntre('2026-10-30', '2026-10-27'), -3);
  assert.equal(diasEntre('2026-12-30', '2027-01-02'), 3);
  assert.equal(diasEntre('2026-10-27', '2026-10-27T15:00:00Z'), 0);
});

test('estado de la cuota: sin plan, al día, por vencer y vencida', () => {
  const hoy = '2026-10-27';
  assert.equal(estadoDeCuota(null, hoy), 'SinPlan');
  assert.equal(estadoDeCuota('2026-11-30T12:00:00Z', hoy), 'AlDia');
  assert.equal(estadoDeCuota('2026-11-03', hoy), 'PorVencer');      // en 7 días: justo el límite
  assert.equal(estadoDeCuota('2026-11-04', hoy), 'AlDia');          // en 8 días
  assert.equal(estadoDeCuota('2026-10-27', hoy), 'PorVencer');      // vence hoy: todavía está al día
  assert.equal(estadoDeCuota('2026-10-26', hoy), 'Vencida');
});

test('el mensaje depende del estado y solo se arma si hay que avisar', () => {
  const base = { nombre: 'Ana María', gimnasio: 'Gym Studio', hoy: '2026-10-27' };
  assert.equal(mensajeDeCuota({ ...base, vencimiento: '2026-12-31' }), null);
  assert.equal(mensajeDeCuota({ ...base, vencimiento: null }), null);

  assert.match(mensajeDeCuota({ ...base, vencimiento: '2026-10-27' })!, /^Hola Ana, te escribimos de Gym Studio\..*vence hoy/);
  assert.match(mensajeDeCuota({ ...base, vencimiento: '2026-10-28' })!, /vence mañana \(28\/10\/2026\)/);
  assert.match(mensajeDeCuota({ ...base, vencimiento: '2026-10-30T10:00:00Z' })!, /vence el 30\/10\/2026 \(en 3 días\)/);
  assert.match(mensajeDeCuota({ ...base, vencimiento: '2026-10-20' })!, /Tu cuota venció el 20\/10\/2026/);
});
