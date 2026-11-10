import assert from 'node:assert/strict';
import { existsSync, readdirSync } from 'node:fs';
import { test } from 'node:test';
import {
  CODIGOS_DESTACADOS, PAISES, PAISES_ORDENADOS, PAIS_POR_DEFECTO, buscarPaises, paisDelPrefijo, paisPorCodigo, prefijoAlComienzo, sinTildes,
} from '../src/utils/paises.ts';
import {
  armarTelefono, errorDeTelefono, interpretarEscrito, interpretarPegado, leerTelefono, maximoDeDigitos, normalizarTelefono, soloDigitos,
} from '../src/utils/telefono.ts';
import { enlaceWhatsApp, telefonoParaWhatsApp } from '../src/utils/whatsapp.ts';

// ---------- países ----------

test('Argentina es el país por defecto y se llama Argentina con el código 54', () => {
  assert.equal(PAIS_POR_DEFECTO, 'AR');
  assert.deepEqual(paisPorCodigo('AR'), { codigo: 'AR', prefijo: '54', nombre: 'Argentina' });
});

test('hay más de 200 países, sin códigos ISO repetidos y todos con código telefónico de 1 a 3 dígitos', () => {
  assert.ok(PAISES.length > 200, String(PAISES.length));
  assert.equal(new Set(PAISES.map((p) => p.codigo)).size, PAISES.length);
  for (const p of PAISES) {
    assert.match(p.codigo, /^[A-Z]{2}$/);
    assert.match(p.prefijo, /^[1-9][0-9]{0,2}$/, p.codigo);
    assert.ok(p.nombre.length > 2 && p.nombre !== p.codigo, `${p.codigo}: ${p.nombre}`);
  }
});

test('los códigos telefónicos del mundo no son prefijo unos de otros (si no, no se sabría dónde termina el código)', () => {
  const prefijos = [...new Set(PAISES.map((p) => p.prefijo))];
  for (const a of prefijos) {
    for (const b of prefijos) if (a !== b) assert.ok(!b.startsWith(a), `+${a} es prefijo de +${b}`);
  }
});

test('todos los países tienen su bandera en la librería de banderas', () => {
  const carpeta = new URL('../node_modules/country-flag-icons/3x2/', import.meta.url);
  assert.ok(existsSync(carpeta), 'falta instalar country-flag-icons');
  const archivos = new Set(readdirSync(carpeta));
  const sinBandera = PAISES.filter((p) => !archivos.has(`${p.codigo}.svg`)).map((p) => p.codigo);
  assert.deepEqual(sinBandera, []);
});

test('algunos códigos conocidos', () => {
  const casos: [string, string][] = [
    ['AR', '54'], ['UY', '598'], ['CL', '56'], ['PY', '595'], ['BO', '591'], ['BR', '55'], ['PE', '51'], ['CO', '57'], ['VE', '58'],
    ['EC', '593'], ['MX', '52'], ['US', '1'], ['CA', '1'], ['ES', '34'], ['IT', '39'], ['DE', '49'], ['FR', '33'], ['GB', '44'],
    ['CN', '86'], ['JP', '81'], ['IN', '91'], ['AU', '61'], ['ZA', '27'], ['RU', '7'], ['CR', '506'], ['DO', '1'], ['AE', '971'],
  ];
  for (const [codigo, prefijo] of casos) assert.equal(paisPorCodigo(codigo)?.prefijo, prefijo, codigo);
});

test('un código compartido muestra su país principal y los demás se encuentran por su ISO', () => {
  assert.equal(paisDelPrefijo('1')?.codigo, 'US');
  assert.equal(paisDelPrefijo('44')?.codigo, 'GB');
  assert.equal(paisDelPrefijo('7')?.codigo, 'RU');
  assert.equal(paisDelPrefijo('54')?.codigo, 'AR');
  assert.equal(paisDelPrefijo('999'), undefined);
  assert.equal(paisDelPrefijo(''), undefined);
});

test('prefijoAlComienzo encuentra el código al principio de una racha de dígitos', () => {
  assert.equal(prefijoAlComienzo('5491155551234'), '54');
  assert.equal(prefijoAlComienzo('12125551234'), '1');
  assert.equal(prefijoAlComienzo('598912345'), '598');
  assert.equal(prefijoAlComienzo('34612345678'), '34');
  assert.equal(prefijoAlComienzo('0123'), null);
  assert.equal(prefijoAlComienzo(''), null);
});

test('la lista sin buscar lleva primero a Argentina y sus vecinos, y cada país aparece una sola vez', () => {
  assert.deepEqual(PAISES_ORDENADOS.slice(0, CODIGOS_DESTACADOS.length).map((p) => p.codigo), [...CODIGOS_DESTACADOS]);
  assert.equal(PAISES_ORDENADOS[0].codigo, 'AR');
  assert.equal(PAISES_ORDENADOS.length, PAISES.length);
  assert.equal(new Set(PAISES_ORDENADOS.map((p) => p.codigo)).size, PAISES.length);
  assert.deepEqual(buscarPaises('').map((p) => p.codigo), PAISES_ORDENADOS.map((p) => p.codigo));
});

test('buscar por nombre sin importar mayúsculas ni tildes, por código telefónico o por ISO', () => {
  assert.deepEqual(buscarPaises('peru').map((p) => p.codigo), ['PE']);
  assert.deepEqual(buscarPaises('PERÚ').map((p) => p.codigo), ['PE']);
  assert.ok(buscarPaises('uni').some((p) => p.codigo === 'GB'));                     // «Reino Unido»
  assert.ok(buscarPaises('estados unidos').some((p) => p.codigo === 'US'));
  assert.deepEqual(buscarPaises('+54').map((p) => p.codigo), ['AR']);
  assert.deepEqual(buscarPaises('54').map((p) => p.codigo), ['AR']);
  assert.deepEqual(buscarPaises('598').map((p) => p.codigo), ['UY']);
  assert.ok(buscarPaises('1').every((p) => p.prefijo.startsWith('1')) && buscarPaises('1').some((p) => p.codigo === 'US'));
  assert.deepEqual(buscarPaises('ar').map((p) => p.codigo).slice(0, 1), ['AR']);     // el ISO exacto
  assert.deepEqual(buscarPaises('zzzz'), []);
  assert.equal(sinTildes('Perú Ñandú'), 'peru nandu');
});

// ---------- teléfono ----------

test('armarTelefono: «+código número», o vacío si no hay número', () => {
  assert.equal(armarTelefono('AR', '1155551234'), '+54 1155551234');
  assert.equal(armarTelefono('UY', '91234567'), '+598 91234567');
  assert.equal(armarTelefono('US', '2125551234'), '+1 2125551234');
  assert.equal(armarTelefono('AR', ''), '');
  assert.equal(armarTelefono('AR', ' - '), '');
  assert.equal(armarTelefono('AR', '11 5555-1234'), '+54 1155551234');   // los signos no entran
});

test('el número se corta para que entre código y número no pasen de 15 dígitos ni el número de 14', () => {
  assert.equal(maximoDeDigitos('AR'), 13);
  assert.equal(maximoDeDigitos('US'), 14);
  assert.equal(maximoDeDigitos('UY'), 12);
  assert.equal(armarTelefono('AR', '1'.repeat(30)), `+54 ${'1'.repeat(13)}`);
  assert.equal(armarTelefono('US', '1'.repeat(30)), `+1 ${'1'.repeat(14)}`);
  assert.equal(interpretarEscrito('9'.repeat(40), 'UY').numero, '9'.repeat(12));
});

test('soloDigitos descarta todo lo que no es 0-9, también los dígitos de otros alfabetos', () => {
  assert.equal(soloDigitos('(011) 15-5555.1234 x'), '011155555' + '1234');
  assert.equal(soloDigitos('abc'), '');
  assert.equal(soloDigitos('٣٤٥'), '');
});

test('leerTelefono: lo guardado se separa en país y número', () => {
  assert.deepEqual(leerTelefono('+54 1155551234'), { pais: 'AR', numero: '1155551234' });
  assert.deepEqual(leerTelefono('+598 91234567'), { pais: 'UY', numero: '91234567' });
  assert.deepEqual(leerTelefono('+1 2125551234'), { pais: 'US', numero: '2125551234' });
  assert.deepEqual(leerTelefono('+34 612345678'), { pais: 'ES', numero: '612345678' });
  assert.deepEqual(leerTelefono(''), { pais: 'AR', numero: '' });
  assert.deepEqual(leerTelefono(null), { pais: 'AR', numero: '' });
  assert.deepEqual(leerTelefono(undefined, 'UY'), { pais: 'UY', numero: '' });
});

test('leerTelefono: lo guardado con el formato de siempre se lee como argentino y con solo sus dígitos', () => {
  assert.deepEqual(leerTelefono('+54 11 5555-0101'), { pais: 'AR', numero: '1155550101' });   // los usuarios de ejemplo de la base real
  assert.deepEqual(leerTelefono('+54 9 11 5555-1234'), { pais: 'AR', numero: '91155551234' });
  assert.deepEqual(leerTelefono('11 5555-1234'), { pais: 'AR', numero: '1155551234' });
  assert.deepEqual(leerTelefono('(011) 15 5555-1234'), { pais: 'AR', numero: '0111555551234' });
  assert.deepEqual(leerTelefono('+5491155551234'), { pais: 'AR', numero: '91155551234' });    // sin el espacio: el código se reconoce igual
  assert.deepEqual(leerTelefono('+598912345678'), { pais: 'UY', numero: '912345678' });
  assert.deepEqual(leerTelefono('hola'), { pais: 'AR', numero: '' });
  assert.deepEqual(leerTelefono('4444-5555 / 4444-6666'), { pais: 'AR', numero: '' });   // varios números o texto: no se inventa uno
  assert.deepEqual(leerTelefono('+999 12345678'), { pais: 'AR', numero: '99912345678' });     // código que no existe: se toma como dígitos
});

test('armar y leer son inversos para cualquier país y número válido', () => {
  for (const p of PAISES) {
    const numero = '2'.repeat(Math.min(10, maximoDeDigitos(p.codigo)));
    const partes = leerTelefono(armarTelefono(p.codigo, numero));
    assert.equal(partes.numero, numero, p.codigo);
    assert.equal(partes.pais, paisDelPrefijo(p.prefijo)?.codigo, p.codigo);   // los países que comparten código vuelven como el principal
    assert.equal(armarTelefono(partes.pais, partes.numero), armarTelefono(p.codigo, numero), p.codigo);
  }
});

test('escribir en el campo: solo dígitos y el país no cambia', () => {
  assert.deepEqual(interpretarEscrito('11 5555-1234', 'AR'), { pais: 'AR', numero: '1155551234' });
  assert.deepEqual(interpretarEscrito('abc', 'UY'), { pais: 'UY', numero: '' });
  assert.deepEqual(interpretarEscrito('+', 'UY'), { pais: 'UY', numero: '' });
});

test('pegar un número internacional cambia el país; pegar uno nacional solo pone los dígitos', () => {
  assert.deepEqual(interpretarPegado('+54 9 11 5555-1234', 'UY'), { pais: 'AR', numero: '91155551234' });
  assert.deepEqual(interpretarPegado('  +598 91 234 567 ', 'AR'), { pais: 'UY', numero: '91234567' });
  assert.deepEqual(interpretarPegado('+1 (212) 555-1234', 'AR'), { pais: 'US', numero: '2125551234' });
  assert.deepEqual(interpretarPegado('11 5555-1234', 'UY'), { pais: 'UY', numero: '1155551234' });
  assert.deepEqual(interpretarPegado('+999 123', 'UY'), { pais: 'UY', numero: '999123' });
});

test('errorDeTelefono: vacío vale; el resto tiene que ser «+código número» de solo dígitos', () => {
  for (const bueno of ['', '   ', '+54 1155551234', '+1 2125551234', '+7 12345678901234', '+54 1234']) {
    assert.equal(errorDeTelefono(bueno, 'El teléfono'), null, JSON.stringify(bueno));
  }
  for (const malo of ['1155551234', '+541155551234', '+54 11 5555-1234', '+54 123', '+54 11a55', '54 1155551234', '+54 123456789012345', '+598 12345678901234']) {
    assert.match(errorDeTelefono(malo, 'El WhatsApp') ?? '', /^El WhatsApp /, malo);
  }
});

test('errorDeTelefono: un número a medias (de 1 a 3 dígitos) pide al menos 4', () => {
  for (const aMedias of ['+54 1', '+54 11', '+54 115', '+598 9', '+1 212']) {
    assert.match(errorDeTelefono(aMedias, 'El teléfono') ?? '', /^El teléfono tiene que tener al menos 4 dígitos/, aMedias);
  }
});

test('normalizarTelefono lleva lo guardado de siempre al formato nuevo, y deja vacío lo que no se puede leer', () => {
  assert.equal(normalizarTelefono('11 5555-1234'), '+54 1155551234');
  assert.equal(normalizarTelefono('+54 11 5555-0101'), '+54 1155550101');
  assert.equal(normalizarTelefono('+598 91234567'), '+598 91234567');
  assert.equal(normalizarTelefono(''), '');
  assert.equal(normalizarTelefono(null), '');
  assert.equal(normalizarTelefono('hola'), '');
  assert.equal(normalizarTelefono('4444-5555 / 4444-6666'), '');     // dos números: juntar los dígitos armaría uno inventado
  assert.equal(normalizarTelefono('11 5555-1234 int. 5'), '');
  assert.equal(normalizarTelefono('1-2'), '+54 12');                 // muy corto: queda a medias y al guardar pide completarlo
});

// ---------- enlace de WhatsApp con país ----------

test('el enlace de WhatsApp usa el país guardado', () => {
  const casos: [string, string][] = [
    ['+54 1155551234', '5491155551234'],           // Argentina: se agrega el 9 de los celulares
    ['+54 91155551234', '5491155551234'],
    ['+54 0111555551234', '5491155551234'],        // 0 del área y 15, escritos de más
    ['+598 91234567', '59891234567'],
    ['+598 091234567', '59891234567'],             // el 0 con el que se marca dentro de Uruguay se quita
    ['+1 2125551234', '12125551234'],
    ['+34 612345678', '34612345678'],
    ['+55 11987654321', '5511987654321'],
    ['+44 07911123456', '447911123456'],
  ];
  for (const [entrada, esperado] of casos) assert.equal(telefonoParaWhatsApp(entrada), esperado, entrada);
  assert.equal(enlaceWhatsApp('+598 91234567'), 'https://wa.me/59891234567');
  assert.equal(enlaceWhatsApp('+54 1155551234', 'Hola'), 'https://wa.me/5491155551234?text=Hola');
});

test('un teléfono con país que no se puede interpretar devuelve null', () => {
  for (const malo of ['+54 123', '+598 1234', '+54 1155551234567890']) {
    assert.equal(telefonoParaWhatsApp(malo), null, malo);
  }
});

test('lo guardado con el formato de siempre sigue dando el mismo enlace', () => {
  assert.equal(telefonoParaWhatsApp('11 5555-1234'), '5491155551234');
  assert.equal(telefonoParaWhatsApp('+54 9 11 5555 1234'), '5491155551234');
  assert.equal(telefonoParaWhatsApp('+54 11 5555-0101'), '5491155550101');
});
