import { descargarXlsx, type Celda, type Hoja } from './xlsx';
import { datosDelNegocio, fecha, formatearCuit } from './pdfComun';

const TITULOS: Record<string, string> = {
  diario: 'Libro Diario',
  mayor: 'Libro Mayor',
  balance: 'Balance de Sumas y Saldos',
  venta: 'Libro IVA Ventas',
  compra: 'Libro IVA Compras',
  caja: 'Libro Caja',
  banco: 'Libro Banco',
  stock: 'Libro Stock',
};

const n = (v: unknown): number => Number(v) || 0;
const negrita = (v: string | number | null, tipo?: 'importe' | 'entero' | 'fecha' | 'texto'): Celda => ({ v, tipo, negrita: true });
const importe = (v: unknown): Celda => ({ v: n(v), tipo: 'importe' });
const entero = (v: unknown): Celda => ({ v: n(v), tipo: 'entero' });
const dia = (v: unknown): Celda => ({ v: typeof v === 'string' ? v : null, tipo: 'fecha' });

/**
 * Hoja de un libro: encabezado con los datos del negocio y el período, una fila de títulos de
 * columna y los datos en tabla plana (sin celdas combinadas) para poder filtrar y sumar en Excel.
 */
function hojaDe(tipo: string, datos: any, periodo: string): Hoja {
  const negocio = datosDelNegocio();
  const titulo = TITULOS[tipo] ?? 'Reporte';
  const cabecera: Celda[][] = [
    [negrita(negocio.razonSocial)],
    [`CUIT ${negocio.cuit}  ·  ${negocio.condicionIva}`],
    [negrita(titulo), periodo],
    [],
  ];
  const { encabezados, filas } = cuerpo(tipo, datos);
  return { nombre: titulo, filas: [...cabecera, encabezados.map(h => negrita(h)), ...filas] };
}

function cuerpo(tipo: string, datos: any): { encabezados: string[]; filas: Celda[][] } {
  switch (tipo) {
    case 'diario': {
      const asientos: any[] = datos ?? [];
      const filas: Celda[][] = [];
      let debe = 0, haber = 0;
      asientos.forEach(a => {
        const lineas = [...(a.lineas ?? [])].sort((x: any, y: any) => (y.debe > 0 ? 1 : 0) - (x.debe > 0 ? 1 : 0));
        lineas.forEach((l: any) => filas.push([dia(a.fecha), entero(a.numeroAsiento), l.nombreCuenta, l.debe > 0 ? importe(l.debe) : null, l.haber > 0 ? importe(l.haber) : null, a.descripcion ?? '']));
        debe += n(a.totalDebe); haber += n(a.totalHaber);
      });
      filas.push([], [null, null, negrita(`TOTALES (${asientos.length} asientos)`), negrita(debe, 'importe'), negrita(haber, 'importe')],
        [null, null, negrita('Diferencia (Debe - Haber)'), negrita(debe - haber, 'importe')]);
      return { encabezados: ['Fecha', 'N° asiento', 'Cuenta', 'Debe', 'Haber', 'Descripción'], filas };
    }
    case 'mayor': {
      const filas: Celda[][] = [];
      (datos ?? []).forEach((c: any) => {
        let debe = 0, haber = 0;
        if (n(c.saldoAnterior) > 0) { filas.push([c.codigo, c.cuenta, null, 'Saldo anterior', importe(c.saldoAnterior), null]); debe += n(c.saldoAnterior); }
        if (n(c.saldoAnterior) < 0) { filas.push([c.codigo, c.cuenta, null, 'Saldo anterior', null, importe(-n(c.saldoAnterior))]); haber -= n(c.saldoAnterior); }
        (c.movimientos ?? []).forEach((m: any) => {
          filas.push([c.codigo, c.cuenta, dia(m.fecha), `As. ${String(m.numeroAsiento ?? '').padStart(4, '0')}`, m.debe > 0 ? importe(m.debe) : null, m.haber > 0 ? importe(m.haber) : null]);
          debe += n(m.debe); haber += n(m.haber);
        });
        const saldo = debe - haber;
        filas.push([negrita(c.codigo), negrita(c.cuenta), null, negrita(Math.abs(saldo) < 0.005 ? 'Saldo: cuenta saldada' : `Saldo ${saldo > 0 ? 'deudor' : 'acreedor'}`),
          negrita(debe, 'importe'), negrita(haber, 'importe'), negrita(Math.abs(saldo), 'importe')], []);
      });
      return { encabezados: ['Código', 'Cuenta', 'Fecha', 'Detalle', 'Debe', 'Haber', 'Saldo'], filas };
    }
    case 'balance': {
      const cuentas: any[] = datos ?? [];
      const t = cuentas.reduce((a, c) => ({ debe: a.debe + n(c.totalDebe), haber: a.haber + n(c.totalHaber), deudor: a.deudor + n(c.saldoDeudor), acreedor: a.acreedor + n(c.saldoAcreedor) }), { debe: 0, haber: 0, deudor: 0, acreedor: 0 });
      return {
        encabezados: ['Código', 'Cuenta', 'Naturaleza', 'Debe', 'Haber', 'Saldo deudor', 'Saldo acreedor'],
        filas: [
          ...cuentas.map(c => [c.codigo, c.cuenta, c.naturaleza ?? '', importe(c.totalDebe), importe(c.totalHaber), importe(c.saldoDeudor), importe(c.saldoAcreedor)]),
          [], [null, negrita('Totales'), null, negrita(t.debe, 'importe'), negrita(t.haber, 'importe'), negrita(t.deudor, 'importe'), negrita(t.acreedor, 'importe')],
        ],
      };
    }
    case 'venta':
    case 'compra': {
      const facturas: any[] = datos ?? [];
      const validas = facturas.filter(f => !f.anulada);
      const suma = (k: string) => validas.reduce((s, f) => s + n(f[k]), 0);
      return {
        encabezados: ['Fecha', 'Tipo', 'Número', tipo === 'venta' ? 'Cliente' : 'Proveedor', 'CUIT / DNI', 'Neto', 'IVA', 'Total', 'Estado'],
        filas: [
          ...facturas.map(f => [dia(f.fecha), f.comprobante, f.numero ?? 's/n', f.tercero, formatearCuit(f.cuit) || '-',
            f.anulada ? null : importe(f.neto), f.anulada ? null : importe(f.iva), f.anulada ? null : importe(f.total), f.anulada ? 'Anulada' : 'Vigente']),
          [], [null, null, null, negrita(`Totales (${validas.length} comprobantes)`), null, negrita(suma('neto'), 'importe'), negrita(suma('iva'), 'importe'), negrita(suma('total'), 'importe')],
        ],
      };
    }
    case 'stock': {
      const filas: Celda[][] = [];
      (datos ?? []).forEach((p: any) => {
        if (n(p.saldoAnterior)) filas.push([p.producto, null, 'Saldo anterior', null, null, entero(p.saldoAnterior), importe(p.precioUnitario), p.precioVenta ? importe(p.precioVenta) : null]);
        (p.movimientos ?? []).forEach((m: any) => filas.push([p.producto, dia(m.fecha), m.detalle, m.ingreso ? entero(m.ingreso) : null, m.salida ? entero(m.salida) : null, entero(m.saldo), importe(p.precioUnitario), p.precioVenta ? importe(p.precioVenta) : null]));
        filas.push([negrita(p.producto), null, negrita('Stock final'), null, null, negrita(n(p.saldoFinal), 'entero')], []);
      });
      return { encabezados: ['Producto', 'Fecha', 'Detalle', 'Ingreso', 'Salida', 'Saldo', 'P. unitario', 'Precio venta'], filas };
    }
    default: { // caja y banco: { saldoInicial, saldoFinal, movimientos }
      const movimientos: any[] = Array.isArray(datos) ? datos : (datos?.movimientos ?? []);
      const saldoInicial = n(datos?.saldoInicial);
      const ingresos = movimientos.reduce((s, m) => s + n(m.ingreso), 0);
      const egresos = movimientos.reduce((s, m) => s + n(m.egreso), 0);
      const saldoFinal = datos?.saldoFinal ?? saldoInicial + ingresos - egresos;
      return {
        encabezados: ['Fecha', 'Concepto', tipo === 'banco' ? 'Depósito' : 'Ingreso', tipo === 'banco' ? 'Cheque / Transf.' : 'Egreso', 'Saldo'],
        filas: [
          [null, negrita('Saldo inicial'), null, null, negrita(saldoInicial, 'importe')],
          ...movimientos.map(m => [dia(m.fecha), m.descripcion ?? '', m.ingreso ? importe(m.ingreso) : null, m.egreso ? importe(m.egreso) : null, importe(m.saldo)]),
          [], [null, negrita(`Totales y saldo final (${movimientos.length} movimientos)`), negrita(ingresos, 'importe'), negrita(egresos, 'importe'), negrita(n(saldoFinal), 'importe')],
        ],
      };
    }
  }
}

/** Arma las hojas (una por libro) sin descargarlas; separado para poder probarlo. */
export function hojasDeReporte(datosParam: any, tipoParam: string, desde?: string, hasta?: string): Hoja[] {
  const periodo = `Período: ${desde ? fecha(desde) : 'desde el inicio'} al ${hasta ? fecha(hasta) : fecha(new Date().toISOString())}`;
  const reportes: { tipo: string; datos: any }[] = tipoParam === 'todos' ? datosParam : [{ tipo: tipoParam, datos: datosParam }];
  return reportes.map(({ tipo, datos }) => hojaDe(tipo, datos, periodo));
}

export function generarReporteExcel(datosParam: any, tipoParam: string, desde?: string, hasta?: string) {
  const sufijo = desde || hasta ? ` ${desde ?? ''}${desde && hasta && desde !== hasta ? ` a ${hasta}` : ''}`.trimEnd() : '';
  descargarXlsx(hojasDeReporte(datosParam, tipoParam, desde, hasta), `${tipoParam === 'todos' ? 'Libros contables' : (TITULOS[tipoParam] ?? 'Reporte')}${sufijo}`);
}
