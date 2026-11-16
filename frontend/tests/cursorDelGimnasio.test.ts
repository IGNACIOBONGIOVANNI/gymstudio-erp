import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  RETRASO_DE_CARGA_MS, SELECTOR_DE_ACCION, SELECTOR_DE_TEXTO, SELECTOR_DESHABILITADO, crearContadorDePedidos, esAccionable, modoDelCursor,
  type ElementoParecido,
} from '../src/utils/cursorDelGimnasio.ts';

test('sobre una acción se muestra el brazo; si no, el cursor normal', () => {
  assert.equal(modoDelCursor({ accionable: true, cargandoDesde: null, ahora: 1000 }), 'accion');
  assert.equal(modoDelCursor({ accionable: false, cargandoDesde: null, ahora: 1000 }), 'normal');
});

test('el disco aparece recién cuando la carga dura más de 200 ms, y le gana al brazo', () => {
  assert.equal(RETRASO_DE_CARGA_MS, 200);
  assert.equal(modoDelCursor({ accionable: true, cargandoDesde: 1000, ahora: 1150 }), 'accion');
  assert.equal(modoDelCursor({ accionable: false, cargandoDesde: 1000, ahora: 1199 }), 'normal');
  assert.equal(modoDelCursor({ accionable: true, cargandoDesde: 1000, ahora: 1200 }), 'carga');
  assert.equal(modoDelCursor({ accionable: false, cargandoDesde: 1000, ahora: 5000 }), 'carga');
});

test('el contador de pedidos sabe desde cuándo hay algo cargando y avisa los cambios', () => {
  let reloj = 100;
  const c = crearContadorDePedidos(() => reloj);
  const avisos: number[] = [];
  const dejar = c.suscribir(() => avisos.push(c.enCurso()));
  assert.equal(c.desde(), null);
  c.empezar();
  reloj = 300;
  c.empezar();
  assert.equal(c.enCurso(), 2);
  assert.equal(c.desde(), 100, 'cuenta desde el primer pedido');
  c.terminar();
  assert.equal(c.desde(), 100);
  c.terminar();
  assert.equal(c.enCurso(), 0);
  assert.equal(c.desde(), null);
  c.terminar();
  assert.equal(c.enCurso(), 0, 'nunca queda negativo');
  dejar();
  c.empezar();
  assert.deepEqual(avisos, [1, 2, 1, 0], 'después de desuscribirse no avisa más');
});

/** Un elemento de mentira: responde a closest y matches según la tabla que se le pasa. */
function elemento(closest: Partial<Record<string, ElementoParecido | null>>, matches: Partial<Record<string, boolean>> = {}): ElementoParecido {
  return {
    closest: (s: string) => closest[s] ?? null,
    matches: (s: string) => matches[s] ?? false,
  };
}

test('cuenta como acción lo que se puede tocar, salvo si está deshabilitado o es un campo para escribir', () => {
  const boton = elemento({}, {});
  const deshabilitado = elemento({}, { [SELECTOR_DESHABILITADO]: true });
  assert.equal(esAccionable(elemento({ [SELECTOR_DE_ACCION]: boton })), true);
  assert.equal(esAccionable(elemento({ [SELECTOR_DE_ACCION]: deshabilitado })), false);
  assert.equal(esAccionable(elemento({ [SELECTOR_DE_ACCION]: boton, [SELECTOR_DE_TEXTO]: boton })), false);
  assert.equal(esAccionable(elemento({})), false);
  assert.equal(esAccionable(null), false);
});

test('los selectores cubren botones, enlaces, pestañas y los gráficos que se pueden tocar', () => {
  for (const s of ['a[href]', 'button', '[role="button"]', '[role="tab"]', 'select', 'summary', '.cursor-pointer', '[cursor="pointer"]']) {
    assert.ok(SELECTOR_DE_ACCION.includes(s), `falta ${s}`);
  }
  for (const s of [':disabled', '[aria-disabled="true"]']) assert.ok(SELECTOR_DESHABILITADO.includes(s), `falta ${s}`);
  assert.ok(SELECTOR_DE_TEXTO.includes('textarea'));
});
