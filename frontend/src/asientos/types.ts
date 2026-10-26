// src/features/asientos/types.ts

export interface CuentaContable {
  id: string;
  codigo: string;
  nombre: string;
}

/**
 * Forma de una línea TAL COMO vive en el formulario: los montos son
 * `string` (no `number`) porque son inputs controlados por el usuario
 * mientras escribe ("12.", "0.", "" son estados intermedios válidos).
 * La conversión a Decimal ocurre recién al calcular totales o al enviar.
 */
export interface LineaAsientoFormValues {
  cuentaId: string;
  descripcion?: string;
  debe: string;
  haber: string;
}

export interface AsientoFormValues {
  fecha: string;
  descripcion: string;
  lineas: LineaAsientoFormValues[];
}
