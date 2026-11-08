import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';

const leer = (ruta: string) => readFileSync(new URL(`../src/features/configuracion/${ruta}`, import.meta.url), 'utf8');

// Toda la pantalla de Configuración es un <form> (guarda los datos de la empresa). Un <form> adentro de otro no es HTML válido, y
// además apretar Enter en un campo de adentro guardaría también la configuración de la empresa, o un botón de envío lo haría solo.
test('el bloque de copias automáticas no abre un <form> ni usa un botón de envío, porque Configuración ya está dentro de uno', () => {
  assert.match(leer('ModuloConfiguracion.tsx'), /<form\b/, 'Configuración dejó de ser un formulario: revisar esta prueba');

  const bloque = leer('CopiasAutomaticas.tsx');
  assert.doesNotMatch(bloque, /<form\b/);
  assert.doesNotMatch(bloque, /type="submit"/);
});

test('el bloque de copias automáticas atiende Enter por su cuenta para que no llegue al formulario de Configuración', () => {
  const bloque = leer('CopiasAutomaticas.tsx');
  assert.match(bloque, /e\.key === 'Enter'/);
  assert.match(bloque, /e\.preventDefault\(\)/);
});
