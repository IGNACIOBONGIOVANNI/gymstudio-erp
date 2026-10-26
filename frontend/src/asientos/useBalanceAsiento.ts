// src/features/asientos/hooks/useBalanceAsiento.ts
import { useMemo } from "react";
import Decimal from "decimal.js";
import { parseMonto } from "./decimal";
import type { LineaAsientoFormValues } from "./types";

export interface BalanceAsiento {
  totalDebe: Decimal;
  totalHaber: Decimal;
  diferencia: Decimal; // totalDebe - totalHaber
  estaBalanceado: boolean;
}

/**
 * Deriva Debe/Haber/diferencia a partir de las líneas del formulario,
 * SIEMPRE con decimal.js. Sumar montos con `number` nativo (ej. lineas
 * .reduce((a, l) => a + Number(l.debe), 0)) puede acumular error de
 * redondeo con muchas líneas y marcar un asiento como "balanceado"
 * cuando en realidad tiene una diferencia de centavos.
 */
export function useBalanceAsiento(lineas: LineaAsientoFormValues[]): BalanceAsiento {
  return useMemo(() => {
    const totalDebe = lineas.reduce(
      (acumulado, linea) => acumulado.plus(parseMonto(linea.debe)),
      new Decimal(0)
    );
    const totalHaber = lineas.reduce(
      (acumulado, linea) => acumulado.plus(parseMonto(linea.haber)),
      new Decimal(0)
    );
    const diferencia = totalDebe.minus(totalHaber);

    return {
      totalDebe,
      totalHaber,
      diferencia,
      // > 0 evita habilitar "Confirmar" en un asiento vacío (0 == 0).
      estaBalanceado: diferencia.isZero() && totalDebe.greaterThan(0),
    };
  }, [lineas]);
}
