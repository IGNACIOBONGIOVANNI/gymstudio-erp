import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { nombreDelMes } from '../api/dashboardApi';
import { PERSPECTIVAS, TEXTO_DEL_SEMAFORO, textoDeValor, textoDeVariacion, type IndicadoresDelMes } from '../features/tablero/tableroUtil';
import { ALTO_ENCABEZADO, MARGEN, abrirPDF, encabezadosYPies, estiloTabla } from './pdfComun';

const derecha = { halign: 'right' as const };

/**
 * El tablero de comando de un mes, listo para imprimir (blanco y negro, A4 vertical): una tabla por perspectiva del
 * Balanced Scorecard con el valor, el mes anterior, la variación, la meta y el estado del semáforo en palabras.
 */
export function generarTableroPDF(datos: IndicadoresDelMes) {
  const doc = new jsPDF();
  doc.setTextColor(0);
  doc.setDrawColor(0);
  let y = ALTO_ENCABEZADO + 2;

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(8.5);
  doc.text(`Comparado con ${nombreDelMes(datos.mesAnterior)}. Medido hasta el ${datos.cierre.split('-').reverse().join('/')}.`, MARGEN, y);
  y += 4;

  for (const perspectiva of PERSPECTIVAS) {
    const filas = datos.indicadores.filter((i) => i.perspectiva === perspectiva);
    autoTable(doc, {
      ...estiloTabla(),
      startY: y + 2,
      head: [[
        `Perspectiva ${perspectiva}`, { content: 'Valor', styles: derecha }, { content: 'Mes anterior', styles: derecha },
        { content: 'Variación', styles: derecha }, { content: 'Meta', styles: derecha }, 'Estado',
      ]],
      body: filas.map((i) => [
        i.nombre,
        { content: textoDeValor(i.valor, i.esPorcentaje, i.clave), styles: derecha },
        { content: textoDeValor(i.anterior, i.esPorcentaje, i.clave), styles: derecha },
        { content: textoDeVariacion(i.variacion, i.esPorcentaje).replace('▲', '+').replace('▼', '-').replace('= ', ''), styles: derecha },
        { content: i.admiteMeta ? textoDeValor(i.meta, i.esPorcentaje, i.clave) : '—', styles: derecha },
        TEXTO_DEL_SEMAFORO[i.semaforo],
      ]),
      styles: { ...estiloTabla().styles, fontSize: 8.5, cellPadding: 1.6 },
      // Mismos anchos en las cuatro tablas, para que las columnas queden alineadas en la hoja.
      columnStyles: { 0: { cellWidth: 56 }, 1: { cellWidth: 24 }, 2: { cellWidth: 24 }, 3: { cellWidth: 20 }, 4: { cellWidth: 24 } },
    });
    y = (doc as jsPDF & { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 3;
  }

  doc.setFont('helvetica', 'italic');
  doc.setFontSize(7.5);
  doc.text('Ciclo de control: el Administrador fija la meta, el sistema mide el valor real, el estado muestra el desvío y en Decisiones se ve qué corregir.',
    MARGEN, Math.min(y + 4, 280), { maxWidth: 182 });

  encabezadosYPies(doc, () => ({ titulo: 'Tablero de comando', periodo: nombreDelMes(datos.mes) }), 'Importes en pesos ($)');
  abrirPDF(doc, `Tablero de comando ${datos.mes}`);
}
