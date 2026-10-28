import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import type { AsignacionDetalle } from '../api/rutinasApi';
import { textoDescanso } from '../api/rutinasApi';
import { ALTO_ENCABEZADO, ALTO_PAGINA, ANCHO_PAGINA, MARGEN, abrirPDF, encabezadosYPies, estiloTabla, fecha } from './pdfComun';

/**
 * Rutina de un socio lista para imprimir (blanco y negro): datos del socio y de la rutina y,
 * por cada día, la tabla de ejercicios con una columna en blanco para anotar el peso.
 */
export function generarRutinaPDF(asignacion: AsignacionDetalle) {
  const doc = new jsPDF();
  const { socio, rutina } = asignacion;
  const nombreSocio = `${socio.apellido}, ${socio.nombre}`;

  doc.setTextColor(0);
  doc.setDrawColor(0);

  // Datos del socio y de la rutina.
  let y = ALTO_ENCABEZADO + 2;
  doc.setLineWidth(0.3);
  doc.rect(MARGEN, y, ANCHO_PAGINA - 2 * MARGEN, 30);
  const columna = (etiqueta: string, valor: string, x: number, yy: number, ancho: number) => {
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(9);
    doc.text(etiqueta, x, yy);
    const separacion = doc.getTextWidth(etiqueta) + 1.5;
    doc.setFont('helvetica', 'normal');
    doc.text(doc.splitTextToSize(valor || '-', ancho - separacion)[0] ?? '', x + separacion, yy);
  };
  const medio = MARGEN + (ANCHO_PAGINA - 2 * MARGEN) / 2;
  columna('Socio:', nombreSocio, MARGEN + 3, y + 7, medio - MARGEN - 3);
  columna('DNI:', socio.dni, medio + 3, y + 7, 70);
  columna('Rutina:', rutina.nombre, MARGEN + 3, y + 14, medio - MARGEN - 3);
  columna('Nivel:', rutina.nivel, medio + 3, y + 14, 70);
  columna('Desde:', fecha(asignacion.fechaInicio), MARGEN + 3, y + 21, 80);
  columna('Hasta:', asignacion.fechaFin ? fecha(asignacion.fechaFin) : 'Sin vencimiento', medio + 3, y + 21, 70);
  columna('Objetivo:', rutina.objetivo, MARGEN + 3, y + 28, ANCHO_PAGINA - 2 * MARGEN - 6);
  y += 36;

  if (asignacion.observaciones) {
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(9);
    doc.text('Observaciones del profesor:', MARGEN, y);
    doc.setFont('helvetica', 'normal');
    const lineas = doc.splitTextToSize(asignacion.observaciones, ANCHO_PAGINA - 2 * MARGEN);
    doc.text(lineas, MARGEN, y + 5);
    y += 5 + lineas.length * 4.5 + 3;
  }

  // Un bloque por día, con su tabla.
  const dias = [...new Set(rutina.ejercicios.map(e => e.dia))].sort((a, b) => a - b);
  for (const dia of dias) {
    if (y > ALTO_PAGINA - 50) { doc.addPage(); y = ALTO_ENCABEZADO + 2; }
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.text(`Día ${dia}`, MARGEN, y + 4);

    autoTable(doc, {
      ...estiloTabla(),
      startY: y + 7,
      head: [['Ejercicio', { content: 'Series', styles: { halign: 'center' } }, { content: 'Repeticiones', styles: { halign: 'center' } },
        { content: 'Descanso', styles: { halign: 'center' } }, 'Indicaciones', { content: 'Peso (kg)', styles: { halign: 'center' } }]],
      body: rutina.ejercicios.filter(e => e.dia === dia).map(e => [
        `${e.ejercicio ?? ''}\n${e.grupoMuscular ?? ''}`,
        { content: String(e.series), styles: { halign: 'center' } },
        { content: e.repeticiones, styles: { halign: 'center' } },
        { content: textoDescanso(e.descansoSegundos), styles: { halign: 'center' } },
        e.indicaciones || '',
        '',
      ]),
      styles: { ...estiloTabla().styles, fontSize: 9, cellPadding: 2.2, minCellHeight: 11 },
      columnStyles: { 0: { cellWidth: 58 }, 1: { cellWidth: 16 }, 2: { cellWidth: 25 }, 3: { cellWidth: 22 }, 5: { cellWidth: 24 } },
    });
    y = (doc as any).lastAutoTable.finalY + 8;
  }

  encabezadosYPies(doc, () => ({ titulo: 'Rutina de entrenamiento', periodo: nombreSocio }), `Vence: ${asignacion.fechaFin ? fecha(asignacion.fechaFin) : 'sin vencimiento'}`);
  abrirPDF(doc, `Rutina ${rutina.nombre} - ${socio.apellido} ${socio.nombre}`);
}
