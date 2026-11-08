import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { formatoDeHoras, type Presupuesto } from '../features/entrenadores/horasUtil';
import { ALTO_ENCABEZADO, MARGEN, abrirPDF, encabezadosYPies, estiloTabla, fecha, importe, pesos } from './pdfComun';

const derecha = { halign: 'right' as const };

/**
 * Presupuesto de pago a los entrenadores de un período, listo para imprimir (blanco y negro, A4 vertical): por entrenador, los días con
 * horas, las horas, el valor por hora y el monto; abajo el total. Si el valor de la hora de alguno cambió dentro del período, esa fila dice
 * «varios» y una nota al pie explica que el monto suma cada día con el valor que tenía.
 */
export function generarPresupuestoPDF(presupuesto: Presupuesto) {
  const doc = new jsPDF();
  doc.setTextColor(0);
  doc.setDrawColor(0);
  const hayVarios = presupuesto.filas.some((f) => f.valorHora === null);

  autoTable(doc, {
    ...estiloTabla(),
    startY: ALTO_ENCABEZADO + 2,
    head: [['Entrenador', { content: 'Días', styles: derecha }, { content: 'Horas', styles: derecha }, { content: 'Valor por hora', styles: derecha }, { content: 'Monto', styles: derecha }]],
    body: presupuesto.filas.map((f) => [
      f.nombre,
      { content: String(f.dias), styles: derecha },
      { content: formatoDeHoras(f.horas), styles: derecha },
      { content: f.valorHora === null ? 'varios' : pesos(f.valorHora), styles: derecha },
      { content: importe(f.monto), styles: derecha },
    ]),
    foot: [[
      { content: 'Total', styles: { fontStyle: 'bold' as const } }, '',
      { content: formatoDeHoras(presupuesto.totalHoras), styles: derecha }, '',
      { content: importe(presupuesto.totalMonto), styles: derecha },
    ]],
    styles: { ...estiloTabla().styles, fontSize: 9.5, cellPadding: 2 },
    columnStyles: { 0: { cellWidth: 70 } },
  });

  if (hayVarios) {
    const finalY = (doc as jsPDF & { lastAutoTable: { finalY: number } }).lastAutoTable.finalY;
    doc.setFont('helvetica', 'italic');
    doc.setFontSize(8);
    doc.text('Valor por hora «varios»: cambió dentro del período; el monto suma cada día con el valor que tenía.', MARGEN, Math.min(finalY + 6, 280), { maxWidth: 182 });
  }

  encabezadosYPies(
    doc,
    () => ({ titulo: 'Presupuesto de pago a entrenadores', periodo: `Del ${fecha(presupuesto.desde)} al ${fecha(presupuesto.hasta)}` }),
    'Importes en pesos ($)',
  );
  abrirPDF(doc, `Presupuesto de pago a entrenadores ${presupuesto.desde} al ${presupuesto.hasta}`);
}
