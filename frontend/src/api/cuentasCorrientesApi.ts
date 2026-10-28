export type TipoCuenta = 'clientes' | 'proveedores';

export interface AntiguedadDeuda {
  corriente: number;
  de31A60: number;
  de61A90: number;
  mas90: number;
  total: number;
}

export interface TerceroConSaldo {
  id: number | null;
  nombre: string;
  cuit: string;
  facturado: number;
  pagado: number;
  saldo: number;
  aFavor: number;
  antiguedad: AntiguedadDeuda;
  facturasPendientes: number;
  diasMasAntigua: number;
}

export interface ListadoCuentas {
  tipo: TipoCuenta;
  hoy: string;
  total: { facturado: number; pagado: number; saldo: number; aFavor: number; antiguedad: AntiguedadDeuda };
  terceros: TerceroConSaldo[];
  mayor: { cuenta: string; saldo: number; diferencia: number };
}

export interface FilaEstadoDeCuenta {
  fecha: string;
  descripcion: string;
  debe: number;
  haber: number;
  saldo: number;
}

export interface FacturaPendiente {
  id: number;
  fecha: string;
  descripcion: string;
  total: number;
  pendiente: number;
  dias: number;
}

export interface DetalleCuenta {
  tercero: { id: number; nombre: string; cuit: string; condicionIva: string };
  hoy: string;
  facturado: number;
  pagado: number;
  saldo: number;
  aFavor: number;
  antiguedad: AntiguedadDeuda;
  pendientes: FacturaPendiente[];
  estado: FilaEstadoDeCuenta[];
}

/** Textos según se trate de clientes (a cobrar) o de proveedores (a pagar). */
export const TEXTOS: Record<TipoCuenta, { titulo: string; tercero: string; terceros: string; pagado: string; verbo: string; mayor: string }> = {
  clientes: { titulo: 'Cuentas a cobrar', tercero: 'Cliente', terceros: 'clientes', pagado: 'Cobrado', verbo: 'cobros', mayor: 'Deudores por Ventas' },
  proveedores: { titulo: 'Cuentas a pagar', tercero: 'Proveedor', terceros: 'proveedores', pagado: 'Pagado', verbo: 'pagos', mayor: 'Proveedores' },
};
