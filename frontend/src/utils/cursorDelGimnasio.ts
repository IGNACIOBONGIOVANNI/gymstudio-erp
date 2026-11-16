// Lógica del cursor del gimnasio, sin React ni DOM real para poder probarla sola: qué se muestra junto a la flecha
// (nada, el brazo haciendo curl o el disco girando) y la cuenta de pedidos a la API en curso.

export type ModoDelCursor = 'normal' | 'accion' | 'carga';

/** Una carga más corta que esto no muestra el disco: evita que parpadee en cada pedido rápido. */
export const RETRASO_DE_CARGA_MS = 200;

/** Lo que se puede tocar: con esto debajo del mouse aparece el brazo haciendo curl. */
export const SELECTOR_DE_ACCION = [
  'a[href]', 'button', '[role="button"]', '[role="tab"]', '[role="menuitem"]', '[role="switch"]', '[role="option"]',
  'select', 'summary', 'label[for]', 'input[type="checkbox"]', 'input[type="radio"]', 'input[type="file"]',
  'input[type="submit"]', 'input[type="button"]', 'input[type="month"]', 'input[type="date"]', 'input[type="time"]',
  '.cursor-pointer', '[cursor="pointer"]',
].join(', ');

/** Lo deshabilitado no es una acción: conserva el cartel de prohibido. */
export const SELECTOR_DESHABILITADO = [':disabled', '[aria-disabled="true"]', '.cursor-not-allowed'].join(', ');

/** En los campos para escribir se deja la barrita de texto. */
export const SELECTOR_DE_TEXTO = [
  'textarea', '[contenteditable="true"]',
  'input:not([type]), input[type="text"], input[type="email"], input[type="password"], input[type="number"], input[type="search"], input[type="tel"], input[type="url"]',
].join(', ');

/** Lo mínimo de un elemento del DOM que hace falta (así se puede probar sin navegador). */
export interface ElementoParecido {
  closest(selector: string): ElementoParecido | null;
  matches(selector: string): boolean;
}

export function esAccionable(elemento: ElementoParecido | null): boolean {
  if (!elemento) return false;
  if (elemento.closest(SELECTOR_DE_TEXTO)) return false;
  const objetivo = elemento.closest(SELECTOR_DE_ACCION);
  if (!objetivo) return false;
  return !objetivo.matches(SELECTOR_DESHABILITADO);
}

/** La carga le gana al brazo: si el sistema está ocupado, eso es lo que importa saber. */
export function modoDelCursor(estado: { accionable: boolean; cargandoDesde: number | null; ahora: number }): ModoDelCursor {
  if (estado.cargandoDesde !== null && estado.ahora - estado.cargandoDesde >= RETRASO_DE_CARGA_MS) return 'carga';
  return estado.accionable ? 'accion' : 'normal';
}

export interface ContadorDePedidos {
  empezar(): void;
  terminar(): void;
  enCurso(): number;
  /** Desde cuándo hay al menos un pedido en curso; null si no hay ninguno. */
  desde(): number | null;
  /** Avisa cada vez que cambia la cantidad en curso; devuelve la función para dejar de escuchar. */
  suscribir(alCambiar: () => void): () => void;
}

export function crearContadorDePedidos(reloj: () => number = () => Date.now()): ContadorDePedidos {
  let enCurso = 0;
  let desde: number | null = null;
  const oyentes = new Set<() => void>();
  const avisar = () => oyentes.forEach((f) => f());
  return {
    empezar() {
      if (enCurso === 0) desde = reloj();
      enCurso++;
      avisar();
    },
    terminar() {
      if (enCurso === 0) return;
      enCurso--;
      if (enCurso === 0) desde = null;
      avisar();
    },
    enCurso: () => enCurso,
    desde: () => desde,
    suscribir(alCambiar) {
      oyentes.add(alCambiar);
      return () => { oyentes.delete(alCambiar); };
    },
  };
}

/** El contador de toda la aplicación: lo alimenta el interceptor de fetch y lo lee el cursor. */
export const pedidosALaApi = crearContadorDePedidos();

/** Dónde está el codo dentro del dibujo del brazo (44 × 31 px, viewBox -14 -2 47 33): no se mueve durante el curl. */
export const PUNTO_DEL_BRAZO = { x: 20.6, y: 25.4 };
/** El centro del disco (dibujo de 40 × 40 px). */
export const PUNTO_DEL_DISCO = { x: 20, y: 20 };

/** Con el brazo o el disco a la vista, la flecha se oculta: el dibujo es el cursor. */
export function ocultaLaFlecha(modo: ModoDelCursor): boolean {
  return modo !== 'normal';
}

/** Esquina donde se dibuja el brazo o el disco para que su punto de clic caiga justo donde apunta el mouse. */
export function posicionDelDibujo(modo: ModoDelCursor, x: number, y: number): { x: number; y: number } {
  const punto = modo === 'carga' ? PUNTO_DEL_DISCO : PUNTO_DEL_BRAZO;
  return { x: x - punto.x, y: y - punto.y };
}
