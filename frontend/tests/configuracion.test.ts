import assert from 'node:assert/strict';
import { test } from 'node:test';
import { CONFIGURACION_INICIAL } from '../src/features/configuracion/configuracionStorage.ts';
import { compararConfiguracion, leerArchivoDeConfiguracion, mostrarValor, CAMPOS } from '../src/features/configuracion/compararConfiguracion.ts';

const actual = CONFIGURACION_INICIAL;
const archivo = (cambios: Record<string, unknown> = {}) => JSON.stringify({ ...actual, ...cambios });

function leer(texto: string) {
  const r = leerArchivoDeConfiguracion(texto);
  assert.ok(r.ok, r.ok ? '' : r.error);
  return r;
}

test('un archivo igual a la configuración actual no cambia nada', () => {
  const r = leer(archivo());
  const c = compararConfiguracion(actual, r.config);
  assert.equal(c.cambios.length, 0);
  assert.equal(c.iguales, Object.keys(CAMPOS).length);
  assert.equal(c.sinDato, 0);
  assert.deepEqual([r.ignorados, r.invalidos], [[], []]);
});

test('la comparación dice qué campos cambian, con su etiqueta, el valor de ahora y el del archivo', () => {
  const r = leer(archivo({ razonSocial: 'OTRO GYM S.A.', alicuotaIvaDefecto: 10.5, modoDemostracion: false, telefono: '' }));
  const c = compararConfiguracion(actual, r.config);
  assert.deepEqual(
    c.cambios.map((x) => [x.etiqueta, x.actual, x.nuevo]),
    [
      ['Razón social', 'GYM STUDIO S.R.L.', 'OTRO GYM S.A.'],
      ['Teléfono', '+54 11 4567-8900', '(vacío)'],
      ['Alícuota de IVA por defecto (%)', '21', '10.5'],
      ['Modo demostración', 'Sí', 'No'],
    ],
  );
  assert.equal(c.cambios[0].seccion, 'Datos fiscales y empresa');
  assert.equal(c.iguales, Object.keys(CAMPOS).length - 4);
});

test('un archivo de una versión anterior que no trae algunos datos los deja como están', () => {
  const r = leer(JSON.stringify({ razonSocial: 'GYM VIEJO', cuit: '30-71234567-1', puntoVenta: '0002' }));
  const c = compararConfiguracion(actual, r.config);
  assert.deepEqual(c.cambios.map((x) => x.campo), ['razonSocial', 'puntoVenta']);
  assert.equal(c.iguales, 1);
  assert.equal(c.sinDato, Object.keys(CAMPOS).length - 3);
});

test('lo que no es de la configuración se ignora y nunca se carga, ni siquiera claves peligrosas', () => {
  const texto = '{"razonSocial":"X SA","cuit":"30-71234567-1","__proto__":{"polluted":true},"constructor":{"prototype":{"polluted":true}},"hackeo":"<script>alert(1)</script>"}';
  const r = leer(texto);
  assert.deepEqual(r.ignorados.sort(), ['__proto__', 'constructor', 'hackeo'].sort());
  assert.equal(Object.prototype.hasOwnProperty.call(r.config, 'hackeo'), false);
  assert.equal(({} as Record<string, unknown>).polluted, undefined);
  assert.deepEqual(Object.keys(r.config).sort(), ['cuit', 'razonSocial']);
});

test('los valores con forma inválida se informan y no se cargan, y los válidos sí', () => {
  const r = leer(archivo({
    alicuotaIvaDefecto: 'abc', tiempoInactividadMinutos: -5, condicionIva: 'Marciano', email: 'sin-arroba',
    telefono: 'llamame', direccion: 'Calle\u0000 1', nombreFantasia: 'x'.repeat(151), exigirPasswordSegura: 'si',
    puntoVenta: '12345678', razonSocial: 'GYM NUEVO',
  }));
  assert.deepEqual(r.invalidos.map((v) => v.campo).sort(),
    ['alicuotaIvaDefecto', 'condicionIva', 'direccion', 'email', 'exigirPasswordSegura', 'nombreFantasia', 'puntoVenta', 'telefono', 'tiempoInactividadMinutos']);
  assert.equal(r.config.razonSocial, 'GYM NUEVO');
  for (const c of ['alicuotaIvaDefecto', 'condicionIva', 'email', 'tiempoInactividadMinutos']) {
    assert.equal(Object.prototype.hasOwnProperty.call(r.config, c), false, `${c} no tendría que cargarse`);
  }
});

test('los números tienen tope y los de tiempo tienen que ser enteros', () => {
  const r = leer(archivo({ alicuotaIvaDefecto: 101, tiempoInactividadMinutos: 1.5 }));
  assert.deepEqual(r.invalidos.map((v) => v.campo).sort(), ['alicuotaIvaDefecto', 'tiempoInactividadMinutos']);
  const limites = leer(archivo({ alicuotaIvaDefecto: 0, tiempoInactividadMinutos: 1440 }));
  assert.deepEqual(limites.invalidos, []);
});

test('un archivo que no es una configuración se rechaza con un mensaje claro', () => {
  for (const texto of ['esto no es json', '[]', 'null', '12', '"texto"', '{}', '{"razonSocial":"X"}', '{"cuit":"30-71234567-1"}', '{"razonSocial":"","cuit":"30-71234567-1"}', '{"razonSocial":"X","cuit":"abc"}']) {
    const r = leerArchivoDeConfiguracion(texto);
    assert.equal(r.ok, false, `se aceptó ${texto}`);
    if (!r.ok) assert.ok(r.error.length > 10);
  }
  assert.equal(leerArchivoDeConfiguracion(archivo({ nombreFantasia: 'x'.repeat(200_000) })).ok, false);   // demasiado grande
});

test('se informa cuándo se guardó el archivo, sin aceptar un texto enorme', () => {
  assert.equal(leer(archivo({ ultimaActualizacion: '29/10/2026 22:04' })).guardadoEl, '29/10/2026 22:04');
  assert.equal(leer(archivo({ ultimaActualizacion: 'z'.repeat(500) })).guardadoEl?.length, 40);
  assert.equal(leer(archivo({ ultimaActualizacion: 123 })).guardadoEl, undefined);
});

test('los valores se muestran como los entiende una persona', () => {
  assert.equal(mostrarValor(true), 'Sí');
  assert.equal(mostrarValor(false), 'No');
  assert.equal(mostrarValor(''), '(vacío)');
  assert.equal(mostrarValor(undefined), '(vacío)');
  assert.equal(mostrarValor(0), '0');
  assert.equal(mostrarValor(21), '21');
});

test('la configuración inicial del sistema cumple todas las reglas de un archivo', () => {
  const r = leer(JSON.stringify(CONFIGURACION_INICIAL));
  assert.deepEqual(r.invalidos, []);
});
