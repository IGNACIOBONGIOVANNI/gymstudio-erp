// src/lib/decimal.ts
//
// Centraliza el uso de decimal.js para que ningún componente haga
// aritmética de montos con `number` nativo de JS (que sí tiene errores
// de redondeo por representación binaria: 0.1 + 0.2 !== 0.3).
import Decimal from "decimal.js";

/**
 * Convierte un string de input (potencialmente vacío o incompleto
 * mientras el usuario escribe, ej. "12.") a un Decimal seguro.
 * Nunca lanza: ante un valor inválido devuelve Decimal(0).
 */
export function parseMonto(valor: string | undefined | null): Decimal {
  if (!valor || valor.trim() === "" || valor === ".") return new Decimal(0);
  try {
    return new Decimal(valor);
  } catch {
    return new Decimal(0);
  }
}

/** Formatea un Decimal (o number) como moneda ARS para mostrar en UI. */
export function formatMonto(valor: Decimal | number): string {
  const numero = valor instanceof Decimal ? valor.toNumber() : valor;
  return new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "ARS",
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(numero);
}

/** Regex para validar el string tal como lo tipea el usuario: hasta 2 decimales. */
export const MONTO_REGEX = /^\d*(\.\d{0,2})?$/;
