import jsPDF from 'jspdf';
import autoTable, { type CellDef } from 'jspdf-autotable';
import { DIAS, filasParaMostrar, textoDelTurno, type Grilla } from '../features/calendario/calendarioUtil';
import { MARGEN, abrirPDF, encabezadosYPies, estiloTabla } from './pdfComun';

const ANCHO_APAISADO = 297;
const ALTO_APAISADO = 210;
const ANCHO_HORA = 36;
const ANCHO_DIA = (ANCHO_APAISADO - 2 * MARGEN - ANCHO_HORA) / DIAS.length;

/**
 * Calendario semanal de encargados listo para imprimir (blanco y negro): hoja A4 apaisada con el logo y los datos del negocio,
 * las horas en filas (06:00 a 22:00) y los días de lunes a viernes en columnas. La misma persona en horas seguidas sale como un
 * solo bloque y las celdas libres quedan en blanco para anotar a mano.
 */
export function generarCalendarioPDF(grilla: Grilla) {
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });
  doc.setTextColor(0);
  doc.setDrawColor(0);

  const base = estiloTabla();
  const body: CellDef[][] = filasParaMostrar(grilla).map(({ hora, celdas }) => {
    const fila: CellDef[] = [{ content: textoDelTurno(hora), styles: { fontStyle: 'bold', halign: 'center', valign: 'middle' } }];
    for (const celda of celdas) {
      if (celda === null) continue;   // la cubre el bloque de arriba (rowSpan)
      fila.push({
        content: celda.texto,
        rowSpan: celda.filas,
        styles: { halign: 'center', valign: 'middle', fontStyle: celda.texto ? 'bold' : 'normal' },
      });
    }
    return fila;
  });

  autoTable(doc, {
    ...base,
    startY: 33,
    margin: { left: MARGEN, right: MARGEN, top: 33, bottom: 14 },
    head: [[{ content: 'Hora', styles: { halign: 'center' } }, ...DIAS.map((d) => ({ content: d.nombre, styles: { halign: 'center' as const } }))]],
    body,
    styles: { ...base.styles, fontSize: 10, cellPadding: 1.5, minCellHeight: 9, lineColor: 90, lineWidth: 0.2 },
    headStyles: { ...base.headStyles, fontSize: 10.5, minCellHeight: 9 },
    // Los cinco días miden lo mismo: un nombre largo se parte en dos líneas en vez de ensanchar su columna.
    columnStyles: {
      0: { cellWidth: ANCHO_HORA },
      ...Object.fromEntries(DIAS.map((_, i) => [i + 1, { cellWidth: ANCHO_DIA }])),
    },
  });

  encabezadosYPies(
    doc,
    () => ({ titulo: 'Calendario de encargados', periodo: 'Lunes a viernes, de 06:00 a 22:00' }),
    'Turnos de una hora',
    { ancho: ANCHO_APAISADO, alto: ALTO_APAISADO },
  );
  abrirPDF(doc, 'Calendario de encargados');
}
