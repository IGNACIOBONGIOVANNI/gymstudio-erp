// Filtros para los campos que piden números: se aplican en cada onChange y dejan solo lo que el campo admite, así que una
// letra, un signo o una «e» (que <input type="number"> deja pasar) no llegan a escribirse ni pegando. Sin dependencias a
// propósito: así se prueban con el ejecutor de pruebas de Node. El servidor vuelve a validar todo.

/** Solo dígitos (DNI, cantidades, series, días, puntos de venta…), con un largo máximo opcional. */
export function soloDigitos(texto: string, largoMaximo?: number): string {
  const digitos = (texto ?? '').replace(/\D+/g, '');
  return largoMaximo ? digitos.slice(0, largoMaximo) : digitos;
}

/**
 * Un número positivo con decimales (precios, importes, medidas, horas), sin signo y con un solo separador decimal.
 *
 * Se escribe como en Argentina: la coma es decimal y el punto puede ser de miles. Un punto seguido de tres cifras, con una a
 * tres cifras antes (o después de otro punto de miles), es separador de miles y se quita: «25.000» queda «25000» y
 * «1.500.000,50» queda «1500000.50». Con menos de tres cifras después, el punto es decimal: «1500.50» sigue siendo 1500,50.
 *
 * `separador`: '.' deja siempre punto decimal, para los campos que se leen con Number(); ',' conserva el separador tal como se
 * escribió, para los campos cuyo lector ya acepta coma o punto (valor de la hora, horas, medidas).
 */
export function numeroDecimal(texto: string, decimales = 2, largoMaximo = 15, separador: '.' | ',' = '.'): string {
  let entero = '';
  let fraccion: string | null = null;   // null: todavía sin separador decimal
  let marca = '';                        // el separador decimal tal como se mostrará
  let fueConPunto = false;               // ese separador se escribió como punto: puede terminar siendo de miles
  let agrupado = false;                  // la parte entera ya viene agrupada de a miles (1.500.000)
  for (const c of texto ?? '') {
    if (c >= '0' && c <= '9') {
      if (fraccion === null) entero += c;
      else if (fueConPunto && fraccion.length === 2 && decimales >= 2 && (agrupado || entero.length <= 3)) {
        // Tercera cifra después de un punto: era un punto de miles.
        entero += fraccion + c;
        fraccion = null;
        agrupado = true;
      } else if (fraccion.length < decimales) fraccion += c;
    } else if ((c === ',' || c === '.') && fraccion === null && decimales > 0) {
      fraccion = '';
      fueConPunto = c === '.';
      marca = separador === '.' ? '.' : c;
    }
    if (entero.length + (fraccion === null ? 0 : fraccion.length + 1) >= largoMaximo) break;
  }
  if (fraccion === null) return entero;
  return (entero === '' ? '0' : entero) + marca + fraccion;
}

/** Repeticiones de un ejercicio mientras se escriben: dígitos y un solo guion para un rango («8-10»), sin espacios. */
export function limpiarRepeticiones(texto: string): string {
  let resultado = '';
  let guion = false;
  for (const c of texto ?? '') {
    if (c >= '0' && c <= '9') resultado += c;
    else if (c === '-' || c === '–') {
      if (guion) break;                 // un segundo guion corta: «8-10-12» queda «8-10»
      if (resultado === '') continue;   // no puede empezar con guion
      guion = true;
      resultado += '-';
    }
  }
  const [desde, hasta] = resultado.split('-');
  return hasta === undefined ? desde.slice(0, 3) : `${desde.slice(0, 3)}-${hasta.slice(0, 3)}`;
}

/** Igual que el servidor: un número (12) o un rango de menor a mayor (8-10), de 1 a 999. */
export function repeticionesValidas(texto: string): boolean {
  const forma = /^(\d{1,3})(?:-(\d{1,3}))?$/.exec((texto ?? '').trim());
  if (!forma) return false;
  const desde = Number(forma[1]);
  return desde > 0 && (forma[2] === undefined || Number(forma[2]) > desde);
}

/** CUIT mientras se escribe: dígitos y guiones (30-71234567-8), hasta 13 caracteres. */
export function soloCuit(texto: string): string {
  return (texto ?? '').replace(/[^\d-]+/g, '').replace(/-{2,}/g, '-').slice(0, 13);
}

/**
 * Para los <input type="number"> que guardan un número (no un texto) mientras se escribe: el navegador deja pasar «e», «+» y
 * «-» (notación científica y signo); esto los bloquea, y también el separador decimal si el campo es de enteros.
 * Uso: onKeyDown={bloquearNoNumericas(false)}.
 */
export function bloquearNoNumericas(conDecimales: boolean) {
  return (evento: { key: string; preventDefault(): void }) => {
    if (esTeclaNoNumerica(evento.key, conDecimales)) evento.preventDefault();
  };
}

export function esTeclaNoNumerica(tecla: string, conDecimales: boolean): boolean {
  if (tecla === 'e' || tecla === 'E' || tecla === '+' || tecla === '-') return true;
  return !conDecimales && (tecla === '.' || tecla === ',');
}

/** Teléfono escrito libre (el del negocio): dígitos, espacios, +, guiones, puntos y paréntesis. */
export function caracteresDeTelefono(texto: string, largoMaximo = 40): string {
  return (texto ?? '').replace(/[^0-9+()\s.-]+/g, '').slice(0, largoMaximo);
}
