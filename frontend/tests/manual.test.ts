import assert from 'node:assert/strict';
import { test } from 'node:test';
import { SECCIONES } from '../src/features/manual/indice.ts';
import { RUTA_MANUAL, VERSION } from '../src/acercaDe.ts';

test('cada sección del manual tiene un id válido como ancla y no se repite', () => {
  const ids = SECCIONES.map((s) => s.id);
  assert.equal(new Set(ids).size, ids.length, 'hay ids repetidos');
  for (const id of ids) assert.match(id, /^[a-z]+(-[a-z]+)*$/, `el id "${id}" no sirve como ancla`);
});

test('cada sección tiene un título que se lee en el índice', () => {
  for (const s of SECCIONES) {
    assert.ok(s.titulo.trim().length >= 3, `la sección ${s.id} no tiene título`);
    assert.ok(s.titulo.length <= 70, `el título de ${s.id} es demasiado largo para el índice`);
  }
});

test('el manual se abre desde una ruta propia y la versión tiene formato x.y.z', () => {
  assert.ok(RUTA_MANUAL.startsWith('/') && !RUTA_MANUAL.includes('#'));
  assert.match(VERSION, /^\d+\.\d+\.\d+$/);
});

test('el manual explica el Tablero de Gestión y el sistema como sistema de información', () => {
  const ids: string[] = SECCIONES.map((s) => s.id);
  assert.ok(ids.includes('tablero'), 'falta la sección del Tablero de Gestión');
  assert.ok(ids.includes('sistema-de-informacion'), 'falta la sección «GymStudio como sistema de información»');
  assert.ok(ids.indexOf('tablero') === ids.indexOf('resumen') + 1, 'el tablero va después del Resumen, como en el menú');
});
