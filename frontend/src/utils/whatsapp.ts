// Avisos de cuota por WhatsApp: se arma el enlace https://wa.me/... con el mensaje ya escrito y
// la persona de recepción lo envía desde su propio WhatsApp. No usa ningún servicio externo.
// Sin dependencias a propósito: así se prueba con el ejecutor de pruebas de Node.

export type EstadoDeCuota = 'SinPlan' | 'AlDia' | 'PorVencer' | 'Vencida';

const DIAS_POR_VENCER = 7;

/** Días entre dos fechas "aaaa-mm-dd" (positivo si `hasta` es posterior a `desde`). */
export function diasEntre(desde: string, hasta: string): number {
  const f = (s: string) => {
    const [a, m, d] = s.slice(0, 10).split('-').map(Number);
    return Date.UTC(a, m - 1, d);
  };
  return Math.round((f(hasta) - f(desde)) / 86_400_000);
}

/** Estado de la cuota de un socio a partir de su vencimiento y de la fecha de hoy ("aaaa-mm-dd"). */
export function estadoDeCuota(vencimiento: string | null | undefined, hoy: string, diasPorVencer = DIAS_POR_VENCER): EstadoDeCuota {
  if (!vencimiento) return 'SinPlan';
  const dias = diasEntre(hoy, vencimiento);
  if (dias < 0) return 'Vencida';
  return dias <= diasPorVencer ? 'PorVencer' : 'AlDia';
}

/**
 * Teléfono argentino en el formato que pide WhatsApp: 549 + código de área + número, solo dígitos.
 * Acepta como se suele escribir: "11 5555-1234", "011 15 5555-1234", "+54 9 11 5555 1234",
 * "(0351) 15-555-1234". Devuelve null si no se puede interpretar (para no mandar el aviso a un número inventado).
 */
export function telefonoParaWhatsApp(telefono: string | null | undefined): string | null {
  let d = (telefono ?? '').replace(/\D/g, '');
  if (d.startsWith('00')) d = d.slice(2);

  if (d.startsWith('549') && d.length === 13) return d;          // ya viene completo
  if (d.startsWith('54') && !d.startsWith('549') && d.length === 12) return `549${d.slice(2)}`;   // +54 11 5555 1234
  if (d.startsWith('54') && d.length > 10) d = d.slice(d.startsWith('549') ? 3 : 2);

  if (d.startsWith('0')) d = d.slice(1);                          // 0 del código de área
  if (d.length === 12) {                                          // área + 15 + número: se quita el 15
    for (const largoArea of [2, 3, 4]) {
      if (d.slice(largoArea, largoArea + 2) === '15') { d = d.slice(0, largoArea) + d.slice(largoArea + 2); break; }
    }
  }
  return d.length === 10 ? `549${d}` : null;                       // área + número = 10 dígitos
}

export function enlaceWhatsApp(telefono: string | null | undefined, mensaje: string): string | null {
  const numero = telefonoParaWhatsApp(telefono);
  return numero ? `https://wa.me/${numero}?text=${encodeURIComponent(mensaje)}` : null;
}

const aDDMMAAAA = (f: string) => f.slice(0, 10).split('-').reverse().join('/');

/** Mensaje de aviso según el estado de la cuota. Devuelve null si no hace falta avisar. */
export function mensajeDeCuota(datos: { nombre: string; gimnasio: string; vencimiento: string | null | undefined; hoy: string }): string | null {
  const estado = estadoDeCuota(datos.vencimiento, datos.hoy);
  if ((estado !== 'PorVencer' && estado !== 'Vencida') || !datos.vencimiento) return null;

  const nombre = datos.nombre.trim().split(/\s+/)[0] || 'socio/a';
  const fecha = aDDMMAAAA(datos.vencimiento);
  const saludo = `Hola ${nombre}, te escribimos de ${datos.gimnasio}.`;

  if (estado === 'Vencida') return `${saludo} Tu cuota venció el ${fecha}. Cuando quieras pasá por recepción para renovarla. ¡Te esperamos!`;

  const dias = diasEntre(datos.hoy, datos.vencimiento);
  const cuando = dias === 0 ? 'vence hoy' : dias === 1 ? `vence mañana (${fecha})` : `vence el ${fecha} (en ${dias} días)`;
  return `${saludo} Te recordamos que tu cuota ${cuando}. Podés renovarla en recepción cuando quieras. ¡Gracias!`;
}
