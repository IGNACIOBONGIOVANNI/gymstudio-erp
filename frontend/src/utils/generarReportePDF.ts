import jsPDF from 'jspdf';
import autoTable, { type RowInput } from 'jspdf-autotable';
import { ALTO_ENCABEZADO, MARGEN, abrirPDF, encabezadosYPies, estiloTabla, fecha, formatearCuit, importe } from './pdfComun';

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

// Filas de título dentro de una tabla (asiento, cuenta): negrita, sin fondo.
const filaTitulo = (texto: string, columnas: number): RowInput =>
  [{ content: texto, colSpan: columnas, styles: { fontStyle: 'bold', lineColor: 0, lineWidth: { top: 0.3, bottom: 0.1, left: 0.15, right: 0.15 } } }];

const derecha = { halign: 'right' as const };

export function generarReportePDF(datosParam: any, tipoParam: string, desde?: string, hasta?: string) {
  const doc = new jsPDF();
  const reportes: { tipo: string; datos: any }[] = tipoParam === 'todos' ? datosParam : [{ tipo: tipoParam, datos: datosParam }];
  const periodo = `Período: ${desde ? fecha(desde) : 'desde el inicio'} al ${hasta ? fecha(hasta) : fecha(new Date().toISOString())}`;

  // Título de cada página: cada libro empieza en hoja nueva y sus páginas llevan su nombre.
  const tituloPorPagina: Record<number, string> = {};
  let tituloActual = '';
  const marcarPagina = () => { tituloPorPagina[doc.getNumberOfPages()] = tituloActual; };

  reportes.forEach(({ tipo, datos }, i) => {
    if (i > 0) doc.addPage();
    tituloActual = TITULOS[tipo] ?? 'Reporte';
    marcarPagina();
    const base = estiloTabla({ startY: ALTO_ENCABEZADO + 2, didDrawPage: marcarPagina });

    switch (tipo) {
      case 'diario': return libroDiario(doc, datos ?? [], base);
      case 'mayor': return libroMayor(doc, datos ?? [], base);
      case 'balance': return sumasYSaldos(doc, datos ?? [], base);
      case 'venta':
      case 'compra': return libroIva(doc, datos ?? [], tipo, base);
      case 'stock': return libroStock(doc, datos ?? [], base);
      default: return libroTesoreria(doc, datos, tipo, base);
    }
  });

  encabezadosYPies(doc, p => ({ titulo: tituloPorPagina[p] ?? tituloActual, periodo }));
  abrirPDF(doc, tipoParam === 'todos' ? 'Libros contables' : (TITULOS[tipoParam] ?? 'Reporte'));
}

function sinMovimientos(doc: jsPDF, texto = 'No hay movimientos en el período.') {
  doc.setFont('helvetica', 'normal');
  doc.setFontSize(10);
  doc.text(texto, MARGEN, ALTO_ENCABEZADO + 8);
}

function libroDiario(doc: jsPDF, asientos: any[], base: any) {
  if (asientos.length === 0) return sinMovimientos(doc);
  const body: RowInput[] = [];
  let debe = 0, haber = 0;
  asientos.forEach(a => {
    const numero = String(a.numeroAsiento ?? '').padStart(4, '0');
    body.push(filaTitulo(`Asiento N° ${numero}  -  ${fecha(a.fecha)}  -  ${a.descripcion ?? ''}`, 3));
    (a.lineas ?? []).forEach((l: any) => {
      // Convención del Diario: las cuentas acreedoras van sangradas y precedidas de "a".
      const cuenta = `${l.codigoCuenta ? l.codigoCuenta + '  ' : ''}${l.nombreCuenta}`;
      body.push([
        l.haber > 0 ? `        a ${cuenta}` : cuenta,
        { content: l.debe > 0 ? importe(l.debe) : '', styles: derecha },
        { content: l.haber > 0 ? importe(l.haber) : '', styles: derecha },
      ]);
    });
    debe += a.totalDebe ?? 0;
    haber += a.totalHaber ?? 0;
  });
  autoTable(doc, {
    ...base,
    head: [['Cuenta', { content: 'Debe', styles: derecha }, { content: 'Haber', styles: derecha }]],
    body,
    foot: [[`Totales del período (${asientos.length} asientos)`, { content: importe(debe), styles: derecha }, { content: importe(haber), styles: derecha }]],
    columnStyles: { 1: { cellWidth: 32 }, 2: { cellWidth: 32 } },
  });
}

function libroMayor(doc: jsPDF, cuentas: any[], base: any) {
  if (cuentas.length === 0) return sinMovimientos(doc);
  let y = base.startY;
  cuentas.forEach(c => {
    // Columna Debe y columna Haber, cada una con sus movimientos en orden.
    const debe: [string, number][] = [];
    const haber: [string, number][] = [];
    if (c.saldoAnterior > 0) debe.push(['Saldo anterior', c.saldoAnterior]);
    if (c.saldoAnterior < 0) haber.push(['Saldo anterior', -c.saldoAnterior]);
    (c.movimientos ?? []).forEach((m: any) => {
      const etiqueta = `${fecha(m.fecha)}  As. ${String(m.numeroAsiento ?? '').padStart(4, '0')}`;
      if (m.debe > 0) debe.push([etiqueta, m.debe]);
      if (m.haber > 0) haber.push([etiqueta, m.haber]);
    });
    const sumaDebe = debe.reduce((t, [, v]) => t + v, 0);
    const sumaHaber = haber.reduce((t, [, v]) => t + v, 0);
    const filas = Math.max(debe.length, haber.length, 1);
    const body: RowInput[] = Array.from({ length: filas }, (_, i) => [
      debe[i]?.[0] ?? '', { content: debe[i] ? importe(debe[i][1]) : '', styles: derecha },
      { content: haber[i]?.[0] ?? '', styles: { lineWidth: { left: 0.35, top: 0.15, bottom: 0.15, right: 0.15 }, lineColor: 0 } },
      { content: haber[i] ? importe(haber[i][1]) : '', styles: derecha },
    ]);
    const saldo = sumaDebe - sumaHaber;

    if (y > 250) { doc.addPage(); y = base.startY; }
    autoTable(doc, {
      ...base,
      startY: y,
      head: [
        [{ content: `${c.codigo}  ${c.cuenta}`, colSpan: 4, styles: { halign: 'center' } }],
        [{ content: 'D  (Debe)', colSpan: 2 }, { content: 'H  (Haber)', colSpan: 2 }],
      ],
      body,
      foot: [
        ['Total Debe', { content: importe(sumaDebe), styles: derecha }, 'Total Haber', { content: importe(sumaHaber), styles: derecha }],
        [{ content: Math.abs(saldo) < 0.005 ? 'Saldo: 0,00 (cuenta saldada)'
            : `Saldo ${saldo > 0 ? 'deudor' : 'acreedor'}: ${importe(Math.abs(saldo))}`, colSpan: 4, styles: { halign: 'center' } }],
      ],
      showFoot: 'lastPage',
      rowPageBreak: 'avoid',
      // Mismas columnas en todas las cuentas, como las T dibujadas a mano.
      columnStyles: { 0: { cellWidth: 61 }, 1: { cellWidth: 30 }, 2: { cellWidth: 61 }, 3: { cellWidth: 30 } },
    });
    y = (doc as any).lastAutoTable.finalY + 7;
  });
}

function sumasYSaldos(doc: jsPDF, cuentas: any[], base: any) {
  if (cuentas.length === 0) return sinMovimientos(doc);
  const t = cuentas.reduce((acc, c) => ({
    debe: acc.debe + c.totalDebe, haber: acc.haber + c.totalHaber,
    deudor: acc.deudor + c.saldoDeudor, acreedor: acc.acreedor + c.saldoAcreedor,
  }), { debe: 0, haber: 0, deudor: 0, acreedor: 0 });

  autoTable(doc, {
    ...base,
    head: [['Código', 'Cuenta', { content: 'Debe', styles: derecha }, { content: 'Haber', styles: derecha },
      { content: 'Saldo deudor', styles: derecha }, { content: 'Saldo acreedor', styles: derecha }]],
    body: cuentas.map(c => [
      c.codigo, c.cuenta,
      { content: importe(c.totalDebe), styles: derecha }, { content: importe(c.totalHaber), styles: derecha },
      { content: c.saldoDeudor ? importe(c.saldoDeudor) : '', styles: derecha },
      { content: c.saldoAcreedor ? importe(c.saldoAcreedor) : '', styles: derecha },
    ]),
    foot: [['', 'Totales', ...[t.debe, t.haber, t.deudor, t.acreedor].map(v => ({ content: importe(v), styles: derecha }))]],
    columnStyles: { 0: { cellWidth: 18 }, 2: { cellWidth: 27 }, 3: { cellWidth: 27 }, 4: { cellWidth: 27 }, 5: { cellWidth: 27 } },
  });

  // Resumen patrimonial a partir de los saldos, con la ecuación contable.
  const saldoDe = (naturaleza: string) => cuentas.filter(c => c.naturaleza === naturaleza)
    .reduce((s, c) => s + (naturaleza === 'Activo' ? c.saldoDeudor - c.saldoAcreedor : c.saldoAcreedor - c.saldoDeudor), 0);
  const activo = saldoDe('Activo'), pasivo = saldoDe('Pasivo'), pn = saldoDe('PatrimonioNeto'), resultado = saldoDe('Resultado');
  const cierra = Math.abs(t.debe - t.haber) < 0.005 && Math.abs(activo - (pasivo + pn + resultado)) < 0.005;

  autoTable(doc, {
    ...base,
    startY: (doc as any).lastAutoTable.finalY + 6,
    head: [[{ content: 'Resumen', colSpan: 2 }]],
    body: [
      ['Activo', { content: importe(activo), styles: derecha }],
      ['Pasivo', { content: importe(pasivo), styles: derecha }],
      ['Patrimonio neto', { content: importe(pn), styles: derecha }],
      [resultado >= 0 ? 'Resultado del período (ganancia)' : 'Resultado del período (pérdida)', { content: importe(resultado), styles: derecha }],
      [{ content: 'Activo = Pasivo + Patrimonio neto + Resultado', styles: { fontStyle: 'bold' } },
        { content: cierra ? 'Verificado' : 'NO CIERRA', styles: { ...derecha, fontStyle: 'bold' } }],
    ],
    tableWidth: 110,
    columnStyles: { 1: { cellWidth: 35 } },
  });
}

function libroIva(doc: jsPDF, facturas: any[], tipo: string, base: any) {
  if (facturas.length === 0) return sinMovimientos(doc, 'No hay comprobantes en el período.');
  const validas = facturas.filter(f => !f.anulada);
  const suma = (k: 'neto' | 'iva' | 'total') => validas.reduce((s, f) => s + (f[k] ?? 0), 0);

  autoTable(doc, {
    ...base,
    styles: { ...(base.styles ?? {}), fontSize: 7.5 },
    head: [['Fecha', 'Tipo', 'Número', tipo === 'venta' ? 'Cliente' : 'Proveedor', 'CUIT / DNI',
      { content: 'Neto', styles: derecha }, { content: 'IVA', styles: derecha }, { content: 'Total', styles: derecha }]],
    body: facturas.map(f => {
      const monto = (v: number) => ({ content: f.anulada ? '-' : importe(v), styles: derecha });
      return [
        fecha(f.fecha), f.comprobante, f.numero ?? 's/n',
        f.anulada ? `${f.tercero}  (ANULADA)` : f.tercero,
        formatearCuit(f.cuit) || '-',
        monto(f.neto), monto(f.iva), monto(f.total),
      ];
    }),
    foot: [[{ content: `Totales (${validas.length} comprobantes)`, colSpan: 5 },
      { content: importe(suma('neto')), styles: derecha }, { content: importe(suma('iva')), styles: derecha },
      { content: importe(suma('total')), styles: derecha }]],
    columnStyles: { 0: { cellWidth: 16 }, 1: { cellWidth: 9 }, 2: { cellWidth: 23 }, 4: { cellWidth: 23 },
      5: { cellWidth: 22 }, 6: { cellWidth: 19 }, 7: { cellWidth: 22 } },
  });

  // Discriminación por alícuota (la que surge de IVA / Neto de cada comprobante).
  const alicuotas = new Map<string, { neto: number; iva: number }>();
  validas.forEach(f => {
    const tasa = f.neto > 0 && f.iva > 0 ? [10.5, 21, 27].reduce((m, a) => (Math.abs(a - (f.iva / f.neto) * 100) < Math.abs(m - (f.iva / f.neto) * 100) ? a : m), 21) : 0;
    const clave = tasa === 0 ? 'Sin IVA discriminado (C / exento)' : `IVA ${String(tasa).replace('.', ',')} %`;
    const acc = alicuotas.get(clave) ?? { neto: 0, iva: 0 };
    alicuotas.set(clave, { neto: acc.neto + f.neto, iva: acc.iva + f.iva });
  });
  autoTable(doc, {
    ...base,
    startY: (doc as any).lastAutoTable.finalY + 6,
    head: [['Alícuota', { content: 'Neto', styles: derecha }, { content: tipo === 'venta' ? 'IVA débito fiscal' : 'IVA crédito fiscal', styles: derecha }]],
    body: [...alicuotas.entries()].map(([clave, v]) => [clave, { content: importe(v.neto), styles: derecha }, { content: importe(v.iva), styles: derecha }]),
    tableWidth: 120,
    columnStyles: { 1: { cellWidth: 32 }, 2: { cellWidth: 32 } },
  });
  if (validas.length < facturas.length) nota(doc, 'Los comprobantes anulados se listan pero no suman en los totales.');
}

function libroStock(doc: jsPDF, productos: any[], base: any) {
  if (productos.length === 0) return sinMovimientos(doc, 'No hay productos con movimientos en el período.');
  let y = base.startY;
  productos.forEach(p => {
    if (y > 245) { doc.addPage(); y = base.startY; }
    autoTable(doc, {
      ...base,
      startY: y,
      head: [
        [{ content: `Producto: ${p.producto}          Costo unitario: ${importe(p.precioUnitario)}          Precio de venta: ${p.precioVenta ? importe(p.precioVenta) : '-'}`, colSpan: 5 }],
        ['Fecha', 'Detalle', { content: 'Ingreso', styles: derecha }, { content: 'Salida', styles: derecha }, { content: 'Saldo', styles: derecha }],
      ],
      body: [
        ...(p.saldoAnterior ? [['', 'Saldo anterior', '', '', { content: String(p.saldoAnterior), styles: derecha }]] : []),
        ...(p.movimientos ?? []).map((m: any) => [
          fecha(m.fecha), m.detalle,
          { content: m.ingreso ? String(m.ingreso) : '', styles: derecha },
          { content: m.salida ? String(m.salida) : '', styles: derecha },
          { content: String(m.saldo), styles: derecha },
        ]),
      ],
      foot: [[{ content: 'Stock final', colSpan: 4 }, { content: String(p.saldoFinal), styles: derecha }]],
      showFoot: 'lastPage',
      columnStyles: { 0: { cellWidth: 19 }, 2: { cellWidth: 17 }, 3: { cellWidth: 17 }, 4: { cellWidth: 17 } },
    });
    y = (doc as any).lastAutoTable.finalY + 7;
  });
  nota(doc, 'Cantidades en unidades. Ingresos: compras. Salidas: ventas.');
}

function libroTesoreria(doc: jsPDF, datos: any, tipo: string, base: any) {
  // Formato del backend: { saldoInicial, saldoFinal, movimientos }.
  const movimientos: any[] = Array.isArray(datos) ? datos : (datos?.movimientos ?? []);
  const saldoInicial = datos?.saldoInicial ?? 0;
  const ingresos = movimientos.reduce((s, m) => s + (m.ingreso ?? 0), 0);
  const egresos = movimientos.reduce((s, m) => s + (m.egreso ?? 0), 0);
  const saldoFinal = datos?.saldoFinal ?? saldoInicial + ingresos - egresos;

  autoTable(doc, {
    ...base,
    head: [['Fecha', 'Concepto',
      { content: tipo === 'banco' ? 'Depósito' : 'Ingreso', styles: derecha },
      { content: tipo === 'banco' ? 'Cheque / Transf.' : 'Egreso', styles: derecha },
      { content: 'Saldo', styles: derecha }]],
    body: [
      ['', { content: 'Saldo inicial', styles: { fontStyle: 'bold' } }, '', '', { content: importe(saldoInicial), styles: { ...derecha, fontStyle: 'bold' } }],
      ...movimientos.map(m => [
        fecha(m.fecha), m.descripcion ?? '',
        { content: m.ingreso ? importe(m.ingreso) : '', styles: derecha },
        { content: m.egreso ? importe(m.egreso) : '', styles: derecha },
        { content: importe(m.saldo), styles: derecha },
      ]),
    ],
    foot: [['', `Totales y saldo final (${movimientos.length} movimientos)`,
      { content: importe(ingresos), styles: derecha }, { content: importe(egresos), styles: derecha },
      { content: importe(saldoFinal), styles: derecha }]],
    columnStyles: { 0: { cellWidth: 19 }, 2: { cellWidth: 27 }, 3: { cellWidth: 27 }, 4: { cellWidth: 29 } },
  });

  // Resumen como se cierra el libro a mano.
  autoTable(doc, {
    ...base,
    startY: (doc as any).lastAutoTable.finalY + 6,
    body: [
      ['Saldo anterior', { content: importe(saldoInicial), styles: derecha }],
      [tipo === 'banco' ? 'Depósitos' : 'Ingresos', { content: importe(ingresos), styles: derecha }],
      [tipo === 'banco' ? 'Cheques / transferencias' : 'Egresos', { content: importe(egresos), styles: derecha }],
      [{ content: 'Saldo actual', styles: { fontStyle: 'bold' } }, { content: importe(saldoFinal), styles: { ...derecha, fontStyle: 'bold' } }],
    ],
    tableWidth: 80,
    columnStyles: { 1: { cellWidth: 32 } },
  });
}

function nota(doc: jsPDF, texto: string) {
  const y = (doc as any).lastAutoTable.finalY + 5;
  if (y > 280) return;
  doc.setFont('helvetica', 'italic');
  doc.setFontSize(7.5);
  doc.setTextColor(0);
  doc.text(texto, MARGEN, y);
}
