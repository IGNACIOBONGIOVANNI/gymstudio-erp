import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { nombreDelMes, type ResumenInicio } from '../api/dashboardApi';
import { ALTO_ENCABEZADO, ALTO_PAGINA, MARGEN, abrirPDF, encabezadosYPies, estiloTabla, importe, pesos } from './pdfComun';

/** Resumen del mes para imprimir (blanco y negro): los mismos números que ve la pantalla de inicio. */
export function generarResumenPDF(datos: ResumenInicio) {
  const doc = new jsPDF();
  const periodo = nombreDelMes(datos.mes);
  let y = ALTO_ENCABEZADO + 2;

  const seccion = (texto: string) => {
    if (y > ALTO_PAGINA - 45) { doc.addPage(); y = ALTO_ENCABEZADO + 2; }
    doc.setTextColor(0);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.text(texto, MARGEN, y + 4);
    y += 6;
  };
  const tabla = (head: string[], body: (string | number)[][], derecha: number[] = []) => {
    autoTable(doc, {
      ...estiloTabla(),
      startY: y,
      head: [head.map((h, i) => ({ content: h, styles: { halign: derecha.includes(i) ? 'right' as const : 'left' as const } }))],
      body: body.map(fila => fila.map((c, i) => ({ content: String(c), styles: { halign: derecha.includes(i) ? 'right' as const : 'left' as const } }))),
      styles: { ...estiloTabla().styles, fontSize: 9, cellPadding: 2 },
    });
    y = (doc as any).lastAutoTable.finalY + 8;
  };

  const f = datos.finanzas;
  if (f) {
    seccion(`Indicadores de ${periodo}`);
    tabla(['Concepto', 'Importe'], [
      [`Ventas netas (${f.ventas.cantidad} ${f.ventas.cantidad === 1 ? "comprobante" : "comprobantes"})`, pesos(f.ventas.neto)],
      [`IVA de ventas (débito fiscal)`, pesos(f.ventas.iva)],
      [`Compras netas (${f.compras.cantidad} ${f.compras.cantidad === 1 ? "comprobante" : "comprobantes"})`, pesos(f.compras.neto)],
      [`IVA de compras (crédito fiscal)`, pesos(f.compras.iva)],
      ['Resultado del mes (ventas netas - compras netas)', pesos(f.resultado)],
      [f.ivaAPagar >= 0 ? 'IVA a pagar' : 'IVA a favor', pesos(Math.abs(f.ivaAPagar))],
      ['Saldo de Caja', pesos(f.cajaSaldo)],
      ['Saldo de Banco', pesos(f.bancoSaldo)],
      ['Facturas anuladas del mes', String(f.facturasAnuladas)],
    ], [1]);

    seccion('Ventas y compras de los últimos 6 meses (neto)');
    tabla(['Mes', 'Ventas', 'Compras', 'Diferencia'],
      f.serie.map(m => [nombreDelMes(m.mes), importe(m.ventas), importe(m.compras), importe(m.ventas - m.compras)]), [1, 2, 3]);

    seccion('Ingresos del mes por origen (neto)');
    tabla(['Origen', 'Importe'], f.ingresosPorOrigen.map(o => [o.origen, importe(o.neto)]), [1]);

    seccion('Productos más vendidos del mes');
    tabla(['Producto', 'Unidades', 'Importe'],
      f.topProductos.length ? f.topProductos.map(p => [p.producto, p.cantidad, importe(p.total)]) : [['Sin ventas de productos de inventario', '', '']], [1, 2]);
  }

  const g = datos.gimnasio;
  seccion('Gimnasio');
  tabla(['Concepto', 'Cantidad'], [
    ['Socios con cuota al día', g.sociosConCuotaAlDia],
    [`Cuotas que vencen en ${g.diasPorVencer} días`, g.cuotasPorVencer],
    ['Socios con cuota vencida', g.sociosVencidos],
    ['Socios al día y sin rutina', g.sociosSinRutina],
    ['Rutinas vencidas', g.rutinasVencidas],
  ], [1]);

  seccion(`Stock bajo (hasta ${datos.umbralStockBajo} unidades)`);
  tabla(['Producto', 'Stock'],
    datos.stockBajo.length ? datos.stockBajo.map(p => [p.nombre, p.stock]) : [['Ningún producto con poco stock', '']], [1]);

  encabezadosYPies(doc, () => ({ titulo: 'Resumen del mes', periodo }), `Al ${datos.hoy.split('-').reverse().join('/')}`);
  abrirPDF(doc, `Resumen ${periodo}`);
}
