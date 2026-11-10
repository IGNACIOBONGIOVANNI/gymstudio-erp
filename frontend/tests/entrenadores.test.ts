import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  BYTES_MAXIMOS_DE_LA_FOTO, LADO_DE_LA_FOTO, codificarHastaQueEntre, enlaceDeEmail, enlaceDeWhatsAppDeEntrenador, errorDeEmail,
  iniciales, recorteCuadrado,
} from '../src/features/entrenadores/entrenadoresUtil.ts';

test('la foto se achica a 256 por 256 y pesa hasta 150 000 bytes (lo mismo que acepta el servidor)', () => {
  assert.equal(LADO_DE_LA_FOTO, 256);
  assert.equal(BYTES_MAXIMOS_DE_LA_FOTO, 150_000);
});

// ---------- recorte cuadrado ----------

test('recorteCuadrado toma el cuadrado central de una foto apaisada', () => {
  assert.deepEqual(recorteCuadrado(1600, 900), { x: 350, y: 0, lado: 900 });
});

test('recorteCuadrado toma el cuadrado central de una foto vertical', () => {
  assert.deepEqual(recorteCuadrado(900, 1600), { x: 0, y: 350, lado: 900 });
});

test('recorteCuadrado deja una foto cuadrada como está', () => {
  assert.deepEqual(recorteCuadrado(800, 800), { x: 0, y: 0, lado: 800 });
});

test('recorteCuadrado no devuelve decimales cuando la diferencia es impar', () => {
  for (const [ancho, alto] of [[901, 900], [900, 901], [1001, 900], [3, 8], [4001, 3000]]) {
    const { x, y, lado } = recorteCuadrado(ancho, alto);
    assert.ok(Number.isInteger(x) && Number.isInteger(y) && Number.isInteger(lado), `${ancho}x${alto}: ${x},${y},${lado}`);
    assert.ok(x >= 0 && y >= 0 && x + lado <= ancho && y + lado <= alto, `${ancho}x${alto} se sale de la imagen`);
  }
  assert.deepEqual(recorteCuadrado(1001, 900), { x: 50, y: 0, lado: 900 });
});

test('recorteCuadrado con medidas imposibles no devuelve NaN', () => {
  assert.deepEqual(recorteCuadrado(0, 0), { x: 0, y: 0, lado: 0 });
  assert.deepEqual(recorteCuadrado(-5, 100), { x: 0, y: 0, lado: 0 });
});

// ---------- calidad del JPEG ----------

const resultado = (tamanos: Record<string, number>) => {
  const llamadas: number[] = [];
  const codificar = async (calidad: number) => {
    llamadas.push(calidad);
    return { size: tamanos[String(calidad)] ?? Number.MAX_SAFE_INTEGER, calidad };
  };
  return { llamadas, codificar };
};

test('codificarHastaQueEntre prueba primero con 0,85 y se queda con esa si entra', async () => {
  const { llamadas, codificar } = resultado({ '0.85': 90_000 });
  const elegida = await codificarHastaQueEntre(codificar);
  assert.deepEqual(llamadas, [0.85]);
  assert.equal(elegida.size, 90_000);
});

test('codificarHastaQueEntre baja la calidad hasta la primera que entra', async () => {
  const { llamadas, codificar } = resultado({ '0.85': 200_000, '0.75': 150_001, '0.65': 150_000, '0.55': 100_000 });
  const elegida = await codificarHastaQueEntre(codificar);
  assert.deepEqual(llamadas, [0.85, 0.75, 0.65]);   // 150 000 justos entran; no sigue probando
  assert.equal(elegida.size, 150_000);
  assert.ok(llamadas.every((q, i) => i === 0 || q < llamadas[i - 1]), 'cada intento baja la calidad');
});

test('codificarHastaQueEntre prueba hasta 0,35 y lanza un error claro si ninguna entra', async () => {
  const { llamadas, codificar } = resultado({});
  await assert.rejects(() => codificarHastaQueEntre(codificar), (e: unknown) => e instanceof Error && /no se pudo achicar/i.test(e.message));
  assert.deepEqual(llamadas, [0.85, 0.75, 0.65, 0.55, 0.45, 0.35]);
});

test('codificarHastaQueEntre deja pasar el error del codificador (la imagen no se pudo leer)', async () => {
  await assert.rejects(() => codificarHastaQueEntre(async () => { throw new Error('boom'); }), /boom/);
});

// ---------- iniciales ----------

test('iniciales: las de las dos primeras palabras, en mayúscula', () => {
  assert.equal(iniciales('Ana López'), 'AL');
  assert.equal(iniciales('madonna'), 'M');
  assert.equal(iniciales('  ana   maría  lópez '), 'AM');
  assert.equal(iniciales(''), '');
  assert.equal(iniciales('   '), '');
  assert.equal(iniciales('édgar núñez'), 'ÉN');
});

// ---------- enlaces de contacto ----------

test('enlaceDeEmail arma el mailto solo si tiene forma de email', () => {
  assert.equal(enlaceDeEmail('ana@gym.com'), 'mailto:ana@gym.com');
  assert.equal(enlaceDeEmail('  ana@gym.com '), 'mailto:ana@gym.com');
  for (const malo of ['sin-arroba', 'a b@c.com', 'a@b', '', '   ', null, undefined]) {
    assert.equal(enlaceDeEmail(malo as string | null | undefined), null, String(malo));
  }
});

test('enlaceDeEmail no deja que el email agregue destinatarios ni asunto al enlace', () => {
  const enlace = enlaceDeEmail('ana@gym.com?subject=hola&cc=jefe')!;
  assert.ok(enlace.startsWith('mailto:'));
  assert.ok(!enlace.includes('?') && !enlace.includes('&'), enlace);
  assert.equal(enlaceDeEmail('ana@gym.com?bcc=otro@x.com'), null);   // con una segunda arroba ya no es un email
  assert.equal(enlaceDeEmail('ana+gym@gym.com'), 'mailto:ana%2Bgym@gym.com');
});

test('enlaceDeWhatsAppDeEntrenador abre el chat sin mensaje armado', () => {
  assert.equal(enlaceDeWhatsAppDeEntrenador('11 5555-1234'), 'https://wa.me/5491155551234');
  assert.equal(enlaceDeWhatsAppDeEntrenador('+54 9 11 5555-1234'), 'https://wa.me/5491155551234');
  for (const malo of ['abc', '', null, undefined]) {
    assert.equal(enlaceDeWhatsAppDeEntrenador(malo as string | null | undefined), null, String(malo));
  }
});

// ---------- validación del formulario (la misma regla que el servidor, para avisar antes de enviar) ----------

test('errorDeEmail: vacío vale (es opcional), uno con forma de email vale y lo demás explica qué falta', () => {
  for (const bueno of ['', '   ', 'ana@gym.com', '  ana@gym.com ', 'ana.lopez+gym@mail.gym.com.ar']) {
    assert.equal(errorDeEmail(bueno), null, JSON.stringify(bueno));
  }
  for (const malo of ['sin-arroba', 'a b@c.com', 'a@b', '@c.com', 'ana@gym..com']) {
    assert.match(errorDeEmail(malo) ?? '', /email/i, malo);
  }
  assert.match(errorDeEmail('a'.repeat(247) + '@gym.com') ?? '', /254/);   // 255 caracteres
  assert.equal(errorDeEmail('a'.repeat(246) + '@gym.com'), null);          // 254 justos
});
