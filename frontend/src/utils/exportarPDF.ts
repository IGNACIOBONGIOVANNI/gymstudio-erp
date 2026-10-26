import { formatearFechaLocal } from "./formatters";
import jsPDF from "jspdf";
import autoTable from "jspdf-autotable";

export const exportarReporteCompletoPDF = (
  asientos: any[],
  ventas: any[],
  compras: any[],
  caja: any,
  libroMayor: any[]
) => {
  const doc = new jsPDF();
  const fechaActual = new Date().toLocaleDateString();

  const formatMoney = (amount: number) => {
    return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' }).format(amount || 0);
  };

  doc.setFontSize(18);
  doc.text(`Reporte Contable General - ${fechaActual}`, 14, 22);
  
  let currentY = 35;

  // 1. Libro Diario
  if (asientos && asientos.length > 0) {
    doc.setFontSize(14);
    doc.text("Libro Diario", 14, currentY);
    currentY += 5;

    const diarioBody: any[] = [];
    
    // Agrupar por fecha
    const asientosPorFecha: Record<string, any[]> = {};
    asientos.forEach(a => {
        const f = formatearFechaLocal(a.fecha);
        if (!asientosPorFecha[f]) asientosPorFecha[f] = [];
        asientosPorFecha[f].push(a);
    });

    Object.keys(asientosPorFecha).forEach(fecha => {
        // 1. Separador de Fecha
        diarioBody.push([
            { content: `ASIENTOS DEL ${fecha}`, colSpan: 3, styles: { fillColor: [226, 232, 240], fontStyle: 'bold', textColor: [15, 23, 42], halign: 'center' } }
        ]);

        let totalDiaDebe = 0;
        let totalDiaHaber = 0;

        asientosPorFecha[fecha].forEach(a => {
            const numAsiento = a.numeroAsiento != null ? `#${a.numeroAsiento.toString().padStart(4, '0')}` : '';
            const desc = a.descripcion ? ` - ${a.descripcion}` : '';
            const tituloAsiento = numAsiento ? `Asiento ${numAsiento}${desc}` : (a.descripcion || "Asiento Contable");

            // 2. Encabezado del asiento
            diarioBody.push([
                { content: tituloAsiento, colSpan: 3, styles: { fillColor: [241, 245, 249], fontStyle: 'bold' } }
            ]);

            // 3. Detalle (Partida doble)
            if (a.lineas && Array.isArray(a.lineas)) {
                a.lineas.forEach((linea: any) => {
                    diarioBody.push([
                        linea.nombreCuenta || linea.cuenta || "Cuenta",
                        linea.debe > 0 ? formatMoney(linea.debe) : "-",
                        linea.haber > 0 ? formatMoney(linea.haber) : "-"
                    ]);
                });
            }

            // 4. Totales del asiento
            const totDebe = a.totalDebe !== undefined ? a.totalDebe : (a.debe || 0);
            const totHaber = a.totalHaber !== undefined ? a.totalHaber : (a.haber || 0);
            totalDiaDebe += totDebe;
            totalDiaHaber += totHaber;

            diarioBody.push([
                { content: "Totales:", styles: { halign: 'right', fontStyle: 'bold' } },
                { content: formatMoney(totDebe), styles: { fontStyle: 'bold' } },
                { content: formatMoney(totHaber), styles: { fontStyle: 'bold' } }
            ]);
            
            // Espacio entre asientos
            diarioBody.push([{ content: "", colSpan: 3, styles: { minCellHeight: 2, fillColor: [255, 255, 255] } }]);
        });

        // 5. Total del Día
        diarioBody.push([
            { content: `TOTAL DEL DÍA ${fecha}:`, styles: { halign: 'right', fontStyle: 'bold', fillColor: [210, 215, 225] } },
            { content: formatMoney(totalDiaDebe), styles: { fontStyle: 'bold', fillColor: [210, 215, 225] } },
            { content: formatMoney(totalDiaHaber), styles: { fontStyle: 'bold', fillColor: [210, 215, 225] } }
        ]);
        diarioBody.push([{ content: "", colSpan: 3, styles: { minCellHeight: 4, fillColor: [255, 255, 255] } }]);
    });

    autoTable(doc, {
      startY: currentY,
      head: [["Cuenta", "Debe", "Haber"]],
      body: diarioBody,
      theme: 'grid',
      styles: { fontSize: 9 },
      headStyles: { fillColor: [15, 23, 42] }
    });

    currentY = (doc as any).lastAutoTable.finalY + 15;
  }

  // 2. Libro Ventas
  if (ventas && ventas.length > 0) {
    if (currentY > 250) { doc.addPage(); currentY = 20; }
    doc.setFontSize(14);
    doc.text("Libro Ventas", 14, currentY);
    currentY += 5;

    const ventasData = ventas.map(v => [
      formatearFechaLocal(v.fecha),
      `${v.tipoFactura || ''} ${v.numeroFactura || ''}`,
      v.clienteNombre || "",
      formatMoney(v.neto),
      formatMoney(v.iva),
      formatMoney(v.total)
    ]);

    const totalVentasNeto = ventas.reduce((sum, v) => sum + (v.neto || 0), 0);
    const totalVentasIva = ventas.reduce((sum, v) => sum + (v.iva || 0), 0);
    const totalVentasTotal = ventas.reduce((sum, v) => sum + (v.total || 0), 0);

    autoTable(doc, {
      startY: currentY,
      head: [["Fecha", "Factura", "Cliente", "Neto", "IVA", "Total"]],
      body: ventasData,
      foot: [["", "", "TOTALES", formatMoney(totalVentasNeto), formatMoney(totalVentasIva), formatMoney(totalVentasTotal)]],
      theme: 'grid',
      styles: { fontSize: 9 },
      footStyles: { fillColor: [241, 245, 249], textColor: [15, 23, 42], fontStyle: 'bold' }
    });
    currentY = (doc as any).lastAutoTable.finalY + 15;
  }

  // 3. Libro Compras
  if (compras && compras.length > 0) {
    if (currentY > 250) { doc.addPage(); currentY = 20; }
    doc.setFontSize(14);
    doc.text("Libro Compras", 14, currentY);
    currentY += 5;

    const comprasData = compras.map(c => [
      formatearFechaLocal(c.fecha),
      `${c.tipoFactura || ''} ${c.numeroFactura || ''}`,
      c.proveedorNombre || "",
      formatMoney(c.neto),
      formatMoney(c.iva),
      formatMoney(c.total)
    ]);

    const totalComprasNeto = compras.reduce((sum, c) => sum + (c.neto || 0), 0);
    const totalComprasIva = compras.reduce((sum, c) => sum + (c.iva || 0), 0);
    const totalComprasTotal = compras.reduce((sum, c) => sum + (c.total || 0), 0);

    autoTable(doc, {
      startY: currentY,
      head: [["Fecha", "Factura", "Proveedor", "Neto", "IVA", "Total"]],
      body: comprasData,
      foot: [["", "", "TOTALES", formatMoney(totalComprasNeto), formatMoney(totalComprasIva), formatMoney(totalComprasTotal)]],
      theme: 'grid',
      styles: { fontSize: 9 },
      footStyles: { fillColor: [241, 245, 249], textColor: [15, 23, 42], fontStyle: 'bold' }
    });
    currentY = (doc as any).lastAutoTable.finalY + 15;
  }

  // 4. Libro Caja
  const movimientosCaja = caja?.listaMovimientos || caja || [];
  if (movimientosCaja && movimientosCaja.length > 0) {
    if (currentY > 250) { doc.addPage(); currentY = 20; }
    doc.setFontSize(14);
    doc.text("Libro Caja", 14, currentY);
    currentY += 5;

    const cajaData = movimientosCaja.map((m: any) => [
      formatearFechaLocal(m.fecha),
      m.detalle || "",
      m.ingreso ? formatMoney(m.ingreso) : "-",
      m.egreso ? formatMoney(m.egreso) : "-"
    ]);
    
    const totalIngresos = movimientosCaja.reduce((sum: number, m: any) => sum + (m.ingreso || 0), 0);
    const totalEgresos = movimientosCaja.reduce((sum: number, m: any) => sum + (m.egreso || 0), 0);
    const saldoCaja = caja?.saldoActual !== undefined ? caja.saldoActual : (totalIngresos - totalEgresos);

    autoTable(doc, {
      startY: currentY,
      head: [["Fecha", "Detalle", "Ingreso", "Egreso"]],
      body: cajaData,
      foot: [["", "SALDO ACTUAL", "", formatMoney(saldoCaja)]],
      theme: 'grid',
      styles: { fontSize: 9 },
      footStyles: { fillColor: [241, 245, 249], textColor: [15, 23, 42], fontStyle: 'bold' }
    });
    currentY = (doc as any).lastAutoTable.finalY + 15;
  }

  // 5. Libro Mayor
  if (libroMayor && libroMayor.length > 0) {
    if (currentY > 250) { doc.addPage(); currentY = 20; }
    doc.setFontSize(16);
    doc.text("Libro Mayor (Movimientos por Cuenta)", 14, currentY);
    currentY += 8;

    libroMayor.forEach((cuenta: any) => {
      // Filtrar movimientos de esta cuenta a partir de los Asientos
      const movimientosCuenta: any[] = [];
      if (asientos && asientos.length > 0) {
        asientos.forEach((asiento: any) => {
          if (asiento.lineas) {
            asiento.lineas.forEach((linea: any) => {
              if (linea.cuentaId === cuenta.cuentaId || linea.nombreCuenta === (cuenta.nombreCuenta || cuenta.cuenta)) {
                movimientosCuenta.push({
                  fecha: asiento.fecha,
                  detalle: linea.descripcion || asiento.descripcion || `Asiento N° ${asiento.numeroAsiento}`,
                  debe: linea.debe,
                  haber: linea.haber
                });
              }
            });
          }
        });
      }

      if (movimientosCuenta.length === 0) return;

      if (currentY > 260) { doc.addPage(); currentY = 20; }
      
      doc.setFontSize(12);
      doc.setFont("helvetica", "bold");
      doc.text(`Cuenta: ${cuenta.nombreCuenta || cuenta.cuenta}`, 14, currentY);
      currentY += 4;

      const mayorData = movimientosCuenta.map(m => [
        formatearFechaLocal(m.fecha),
        m.detalle,
        formatMoney(m.debe),
        formatMoney(m.haber)
      ]);

      const totalDebe = movimientosCuenta.reduce((sum, m) => sum + (m.debe || 0), 0);
      const totalHaber = movimientosCuenta.reduce((sum, m) => sum + (m.haber || 0), 0);
      const saldoFinal = cuenta.saldo !== undefined ? cuenta.saldo : Math.abs(totalDebe - totalHaber);

      autoTable(doc, {
        startY: currentY,
        head: [["Fecha", "Detalle", "Debe", "Haber"]],
        body: mayorData,
        foot: [["", "TOTALES", formatMoney(totalDebe), formatMoney(totalHaber)], ["", `SALDO FINAL: ${formatMoney(saldoFinal)}`, "", ""]],
        theme: 'grid',
        styles: { fontSize: 9 },
        footStyles: { fillColor: [241, 245, 249], textColor: [15, 23, 42], fontStyle: 'bold' }
      });
      currentY = (doc as any).lastAutoTable.finalY + 10;
    });
  }

  doc.save(`Reporte_Contable_${fechaActual.replace(/\//g, '-')}.pdf`);
};


