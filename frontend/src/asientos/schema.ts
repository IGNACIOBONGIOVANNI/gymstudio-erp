// src/features/asientos/schema.ts
import { z } from "zod";
import { MONTO_REGEX, parseMonto } from "./decimal";

// Validación de FORMA únicamente (campos requeridos, formato de número).
// La regla contable "Debe == Haber" NO se valida acá: se calcula en vivo
// con decimal.js (ver useBalanceAsiento) y además el backend la revalida
// de forma autoritativa en AsientoContable.Confirmar().
const lineaAsientoSchema = z
  .object({
    cuentaId: z.string().min(1, "Seleccioná una cuenta"),
    descripcion: z.string().max(300, "Máximo 300 caracteres").optional(),
    debe: z.string().regex(MONTO_REGEX, "Monto inválido"),
    haber: z.string().regex(MONTO_REGEX, "Monto inválido"),
  })
  .refine(
    (linea) => {
      const debe = parseMonto(linea.debe);
      const haber = parseMonto(linea.haber);
      // Regla de partida doble a nivel de línea: exclusivamente Debe O Haber.
      return !(debe.greaterThan(0) && haber.greaterThan(0));
    },
    { message: "Cargá el importe sólo en Debe o en Haber, no en ambos", path: ["haber"] }
  )
  .refine(
    (linea) => {
      const debe = parseMonto(linea.debe);
      const haber = parseMonto(linea.haber);
      return debe.greaterThan(0) || haber.greaterThan(0);
    },
    { message: "Ingresá un importe en Debe o en Haber", path: ["debe"] }
  );

export const asientoFormSchema = z.object({
  fecha: z.string().min(1, "La fecha es obligatoria"),
  descripcion: z
    .string()
    .min(1, "La descripción es obligatoria")
    .max(500, "Máximo 500 caracteres"),
  lineas: z
    .array(lineaAsientoSchema)
    .min(2, "El asiento requiere al menos dos líneas (una al Debe y otra al Haber)"),
});

export type AsientoFormValues = z.infer<typeof asientoFormSchema>;
