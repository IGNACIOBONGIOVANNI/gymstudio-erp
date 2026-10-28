import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { METRICAS, formatearMedida, variacion, type Medicion } from '../api/medicionesApi';
import { ALTO_ENCABEZADO, MARGEN, abrirPDF, encabezadosYPies, estiloTabla, fecha } from './pdfComun';

/** Seguimiento de peso y medidas de un socio, para imprimir (blanco y negro). */
export function generarSeguimientoPDF(socio: { nombre: string; apellido: string }, mediciones: Medicion[]) {
  const doc = new jsPDF();
  const nombre = `${socio.apellido}, ${socio.nombre}`;
  const orden = [...mediciones].sort((a, b) => a.fecha.localeCompare(b.fecha));
  let y = ALTO_ENCABEZADO + 2;

  // Resumen: primera y última medición de cada medida.
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(11);
  doc.text('Resumen de la evolución', MARGEN, y + 4);
  const filas = METRICAS.flatMap((m) => {
    const v = variacion(orden, m.clave);
    if (!v) return [];
    const signo = v.diferencia > 0 ? '+' : v.diferencia < 0 ? '-' : '';
    return [[`${m.etiqueta} (${m.unidad})`, fecha(v.desde), formatearMedida(v.inicial), formatearMedida(v.actual), `${signo}${formatearMedida(Math.abs(v.diferencia))}`]];
  });
  autoTable(doc, {
    ...estiloTabla(),
    startY: y + 7,
    head: [['Medida', 'Desde', { content: 'Primera', styles: { halign: 'right' } }, { content: 'Última', styles: { halign: 'right' } }, { content: 'Cambio', styles: { halign: 'right' } }]],
    body: filas.length ? filas.map((f) => f.map((c, i) => ({ content: c, styles: { halign: i >= 2 ? 'right' as const : 'left' as const } })))
      : [['Se necesitan al menos dos mediciones para calcular el cambio.', '', '', '', '']],
    styles: { ...estiloTabla().styles, fontSize: 9, cellPadding: 2 },
  });
  y = (doc as any).lastAutoTable.finalY + 10;

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(11);
  doc.text('Mediciones', MARGEN, y);
  const derecha = { halign: 'right' as const };
  autoTable(doc, {
    ...estiloTabla(),
    startY: y + 3,
    head: [['Fecha', ...METRICAS.map((m) => ({ content: `${m.etiqueta} (${m.unidad})`, styles: derecha })), 'Notas']],
    body: orden.map((m) => [fecha(m.fecha), ...METRICAS.map((x) => ({ content: formatearMedida(m[x.clave]), styles: derecha })), m.notas]),
    styles: { ...estiloTabla().styles, fontSize: 7.5, cellPadding: 1.8 },
    columnStyles: { 0: { cellWidth: 19 } },
  });

  encabezadosYPies(doc, () => ({ titulo: 'Seguimiento del socio', periodo: nombre }), `${orden.length} medición(es)`);
  abrirPDF(doc, `Seguimiento ${socio.apellido} ${socio.nombre}`);
}
