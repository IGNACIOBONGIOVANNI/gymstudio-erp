// Avisos de la campanita: cuáles requieren atención y cuáles son nuevos desde la última consulta.
// Sin dependencias a propósito: así se prueba con el ejecutor de pruebas de Node.

export interface Novedad {
  nivel: 'atencion' | 'info' | 'ok';
  texto: string;
}

/** Textos de las novedades que requieren atención. Tolera datos faltantes: nunca rompe la pantalla. */
export function textosDeAtencion(novedades: readonly Novedad[] | null | undefined): string[] {
  return (novedades ?? [])
    .filter((n) => n?.nivel === 'atencion' && typeof n.texto === 'string' && n.texto.trim() !== '')
    .map((n) => n.texto);
}

/**
 * Identifica un aviso sin sus cifras. Los importes y las cantidades cambian de una consulta a la otra
 * ("Caja en negativo (-$ 1.200,00)" pasa a "-$ 900,00"), pero sigue siendo el mismo aviso y no hay que repetirlo.
 */
export function claveDeAviso(texto: string): string {
  return String(texto ?? '').replace(/[\d.,]+/g, '#').replace(/\s+/g, ' ').trim();
}

/** Avisos de `atencion` que todavía no se habían mostrado (sin repetidos y en el mismo orden). */
export function avisosNuevos(vistos: ReadonlySet<string>, atencion: readonly string[]): string[] {
  const nuevos = new Map<string, string>();
  for (const texto of atencion) {
    const clave = claveDeAviso(texto);
    if (!vistos.has(clave) && !nuevos.has(clave)) nuevos.set(clave, texto);
  }
  return [...nuevos.values()];
}
