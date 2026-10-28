import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { TEXTOS, type DetalleCuenta, type ListadoCuentas, type TipoCuenta } from '../api/cuentasCorrientesApi';
import { ALTO_ENCABEZADO, MARGEN, abrirPDF, encabezadosYPies, estiloTabla, fecha, formatearCuit, importe } from './pdfComun';

const derecha = { halign: 'right' as const };

/** Listado de cuentas a cobrar o a pagar, con la antigüedad de la deuda de cada cliente o proveedor. */
export function generarListadoCuentasPDF(datos: ListadoCuentas) {
  const doc = new jsPDF();
  const t = TEXTOS[datos.tipo];
  const a = datos.total.antiguedad;

  autoTable(doc, {
    ...estiloTabla(),
    startY: ALTO_ENCABEZADO + 2,
    margin: { left: MARGEN, right: MARGEN, top: ALTO_ENCABEZADO + 2, bottom: 18 },
    head: [[t.tercero, { content: 'Facturado', styles: derecha }, { content: t.pagado, styles: derecha }, { content: 'Saldo', styles: derecha },
      { content: '0 a 30 d.', styles: derecha }, { content: '31 a 60', styles: derecha }, { content: '61 a 90', styles: derecha }, { content: '+ de 90', styles: derecha }]],
    body: datos.terceros.map(x => [
      x.cuit ? `${x.nombre}
${formatearCuit(x.cuit)}` : x.nombre,
      { content: importe(x.facturado), styles: derecha }, { content: importe(x.pagado), styles: derecha },
      { content: x.saldo < 0 ? `${importe(-x.saldo)} a favor` : importe(x.saldo), styles: { ...derecha, fontStyle: 'bold' as const } },
      { content: x.antiguedad.corriente ? importe(x.antiguedad.corriente) : '', styles: derecha },
      { content: x.antiguedad.de31A60 ? importe(x.antiguedad.de31A60) : '', styles: derecha },
      { content: x.antiguedad.de61A90 ? importe(x.antiguedad.de61A90) : '', styles: derecha },
      { content: x.antiguedad.mas90 ? importe(x.antiguedad.mas90) : '', styles: derecha },
    ]),
    foot: [['Totales', { content: importe(datos.total.facturado), styles: derecha }, { content: importe(datos.total.pagado), styles: derecha },
      { content: importe(datos.total.saldo), styles: derecha }, { content: importe(a.corriente), styles: derecha }, { content: importe(a.de31A60), styles: derecha },
      { content: importe(a.de61A90), styles: derecha }, { content: importe(a.mas90), styles: derecha }]],
    styles: { ...estiloTabla().styles, fontSize: 7.5, cellPadding: 1.4 },
    columnStyles: { 0: { cellWidth: 40 } },
  });

  const y = (doc as any).lastAutoTable.finalY + 6;
  doc.setFont('helvetica', 'italic');
  doc.setFontSize(8);
  doc.setTextColor(0);
  const coincide = Math.abs(datos.mayor.diferencia) < 0.005;
  doc.text(
    coincide
      ? `El total coincide con la cuenta ${datos.mayor.cuenta} del Libro Mayor (${importe(datos.mayor.saldo)}).`
      : `Atención: la cuenta ${datos.mayor.cuenta} del Libro Mayor muestra ${importe(datos.mayor.saldo)}; hay una diferencia de ${importe(datos.mayor.diferencia)} (${t.verbo} sin ${t.tercero.toLowerCase()} asignado o asientos manuales).`,
    MARGEN, Math.min(y, 280), { maxWidth: 182 });

  encabezadosYPies(doc, () => ({ titulo: t.titulo, periodo: `Al ${fecha(datos.hoy)}` }), 'Importes en pesos ($)');
  abrirPDF(doc, `${t.titulo} al ${fecha(datos.hoy).replace(/\//g, '-')}`);
}

/** Estado de cuenta de un cliente o proveedor: lo que se facturó, lo cobrado o pagado y lo que queda. */
export function generarEstadoDeCuentaPDF(datos: DetalleCuenta, tipo: TipoCuenta) {
  const doc = new jsPDF();
  const t = TEXTOS[tipo];
  let y = ALTO_ENCABEZADO + 2;

  doc.setTextColor(0);
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(12);
  doc.text(datos.tercero.nombre, MARGEN, y + 4);
  doc.setFont('helvetica', 'normal');
  doc.setFontSize(9);
  doc.text(`CUIT / DNI ${formatearCuit(datos.tercero.cuit) || '-'}  ·  ${datos.tercero.condicionIva}`, MARGEN, y + 10);
  y += 15;

  const saldoTexto = datos.saldo < 0 ? `${importe(-datos.saldo)} a favor` : importe(datos.saldo);
  autoTable(doc, {
    ...estiloTabla(),
    startY: y,
    head: [['Resumen', { content: 'Importe', styles: derecha }]],
    body: [
      ['Facturado en cuenta corriente', { content: importe(datos.facturado), styles: derecha }],
      [t.pagado, { content: importe(datos.pagado), styles: derecha }],
      [{ content: 'Saldo', styles: { fontStyle: 'bold' } }, { content: saldoTexto, styles: { ...derecha, fontStyle: 'bold' } }],
    ],
    tableWidth: 100,
    columnStyles: { 1: { cellWidth: 40 } },
    styles: { ...estiloTabla().styles, fontSize: 9 },
  });
  y = (doc as any).lastAutoTable.finalY + 7;

  if (datos.pendientes.length > 0) {
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(10);
    doc.text('Facturas pendientes (los pagos se aplican a las más antiguas)', MARGEN, y);
    autoTable(doc, {
      ...estiloTabla(),
      startY: y + 3,
      head: [['Fecha', 'Comprobante', { content: 'Total', styles: derecha }, { content: 'Pendiente', styles: derecha }, { content: 'Días', styles: derecha }]],
      body: datos.pendientes.map(p => [fecha(p.fecha), p.descripcion, { content: importe(p.total), styles: derecha }, { content: importe(p.pendiente), styles: derecha }, { content: String(p.dias), styles: derecha }]),
      styles: { ...estiloTabla().styles, fontSize: 8.5 },
      columnStyles: { 0: { cellWidth: 22 }, 4: { cellWidth: 16 } },
    });
    y = (doc as any).lastAutoTable.finalY + 7;
  }

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(10);
  doc.text('Movimientos', MARGEN, y);
  autoTable(doc, {
    ...estiloTabla(),
    startY: y + 3,
    head: [['Fecha', 'Detalle', { content: 'Debe (facturas)', styles: derecha }, { content: `Haber (${t.verbo})`, styles: derecha }, { content: 'Saldo', styles: derecha }]],
    body: datos.estado.length ? datos.estado.map(f => [fecha(f.fecha), f.descripcion,
      { content: f.debe ? importe(f.debe) : '', styles: derecha }, { content: f.haber ? importe(f.haber) : '', styles: derecha }, { content: importe(f.saldo), styles: derecha }])
      : [['', 'Sin movimientos en cuenta corriente.', '', '', '']],
    styles: { ...estiloTabla().styles, fontSize: 8.5 },
    columnStyles: { 0: { cellWidth: 22 }, 2: { cellWidth: 30 }, 3: { cellWidth: 30 }, 4: { cellWidth: 30 } },
  });

  encabezadosYPies(doc, () => ({ titulo: 'Estado de cuenta', periodo: datos.tercero.nombre }), `Al ${fecha(datos.hoy)}`);
  abrirPDF(doc, `Estado de cuenta ${datos.tercero.nombre}`);
}
