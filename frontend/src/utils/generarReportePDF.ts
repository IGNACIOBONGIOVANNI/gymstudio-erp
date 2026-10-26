import { formatearFechaLocal } from "./formatters";
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';

export function generarReportePDF(datosParam: any, tipoParam: string, desde?: string, hasta?: string) {
  const doc = new jsPDF();
  const formatter = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' });

  // Si es "todos", el modal nos pasa un array con todos los libros. Si es individual, lo forzamos a array.
  const reportes = tipoParam === 'todos' ? datosParam : [{ tipo: tipoParam, datos: datosParam }];

  reportes.forEach((reporte: any, rIndex: number) => {
    const { tipo, datos } = reporte;

    // Salto de página entre distintos libros (ej: del Diario al Mayor)
    if (rIndex > 0) {
      doc.addPage();
    }

    // Cabecera principal
    doc.setFontSize(20);
    doc.setFont("helvetica", "bold");
    doc.text("GYM STUDIO S.R.L.", 14, 20);
    
    doc.setFontSize(14);
    const titulos: Record<string, string> = {
      'diario': 'REPORTE DE LIBRO DIARIO',
      'caja': 'REPORTE DE LIBRO CAJA',
      'banco': 'REPORTE DE LIBRO BANCO',
      'mayor': 'REPORTE DE LIBRO MAYOR',
      'balance': 'BALANCE GENERAL',
      'venta': 'LIBRO IVA VENTAS',
      'compra': 'LIBRO IVA COMPRAS'
    };
    doc.text(titulos[tipo] || 'REPORTE', 14, 30);

    // Subtítulo con Período
    doc.setFontSize(10);
    doc.setFont("helvetica", "normal");
    const fechaDesdeStr = desde ? formatearFechaLocal(desde) : 'Inicio';
    const fechaHastaStr = hasta ? formatearFechaLocal(hasta) : 'Hoy';
    doc.text(`Período: Desde ${fechaDesdeStr} - Hasta ${fechaHastaStr}`, 14, 38);

    doc.setFillColor(230, 230, 230);
    doc.rect(14, 46, 182, 10, 'F');
    doc.setFont("helvetica", "bold");

    let y = 62;

    if (tipo === 'mayor') {
      doc.text("Fecha", 16, 52);
      doc.text("Descripción", 40, 52);
      doc.text("Debe", 140, 52);
      doc.text("Haber", 170, 52);
      doc.setFont("helvetica", "normal");

      if (!datos || datos.length === 0) {
          doc.text("Sin movimientos", 16, y);
      } else {
          datos.forEach((cuentaData: any, index: number) => {
            // SALTO DE PÁGINA: Si no es la primera cuenta, forzamos hoja nueva
            if (index > 0) {
              doc.addPage();
              y = 20; 
              
              // Volvemos a dibujar el título en la hoja nueva de la cuenta
              doc.setFontSize(20);
              doc.setFont("helvetica", "bold");
              doc.text("GYM STUDIO S.R.L.", 14, y);
              doc.setFontSize(14);
              doc.text(titulos[tipo], 14, y + 10);
              doc.setFontSize(10);
              doc.setFont("helvetica", "normal");
              doc.text(`Período: Desde ${fechaDesdeStr} - Hasta ${fechaHastaStr}`, 14, y + 18);
              
              y += 26;
              // Redibujamos la cabecera gris
              doc.setFillColor(230, 230, 230);
              doc.rect(14, y, 182, 10, 'F');
              doc.setFont("helvetica", "bold");
              doc.text("Fecha", 16, y + 6);
              doc.text("Descripción", 40, y + 6);
              doc.text("Debe", 140, y + 6);
              doc.text("Haber", 170, y + 6);
              y += 16; 
            }

            if (y > 270) { doc.addPage(); y = 20; } 
            
            doc.setFont("helvetica", "bold");
            doc.text(`CUENTA: ${cuentaData.cuenta.toUpperCase()}`, 16, y);
            y += 8;
            doc.setFont("helvetica", "normal");
            
            let saldoParcial = 0;
            cuentaData.movimientos.forEach((mov: any) => {
              if (y > 270) { doc.addPage(); y = 20; }
              doc.text(formatearFechaLocal(mov.fecha), 16, y);
              doc.text(mov.descripcion.substring(0, 45), 40, y);
              doc.text(formatter.format(mov.debe || 0), 140, y);
              doc.text(formatter.format(mov.haber || 0), 170, y);
              saldoParcial += (mov.debe || 0) - (mov.haber || 0);
              y += 8;
            });

            doc.setFont("helvetica", "bold");
            doc.text(`Saldo Final: ${formatter.format(cuentaData.saldo)}`, 140, y);
            y += 12;
          });
      }

    } else if (tipo === 'balance') {
      doc.text("Naturaleza", 16, 52);
      doc.text("Cuenta", 70, 52);
      doc.text("Saldo", 160, 52);
      doc.setFont("helvetica", "normal");

      let totalActivo = 0, totalPasivo = 0, totalPatri = 0;

      if (!datos || datos.length === 0) {
          doc.text("Sin datos", 16, y);
      } else {
          datos.forEach((item: any) => {
            if (y > 270) { doc.addPage(); y = 20; }
            doc.text(item.naturaleza, 16, y);
            doc.text(item.cuenta, 70, y);
            doc.text(formatter.format(item.saldo), 160, y);
            if (item.naturaleza === 'Activo') totalActivo += item.saldo;
            if (item.naturaleza === 'Pasivo') totalPasivo += item.saldo;
            if (item.naturaleza === 'PatrimonioNeto' || item.naturaleza === 'Resultado' || item.naturaleza === 'Resultado Positivo') totalPatri += item.saldo;
              if (item.naturaleza === 'Resultado Negativo') totalPatri -= item.saldo;
            y += 8;
          });
          
          doc.line(14, y, 196, y);
          y += 8;
          doc.setFont("helvetica", "bold");
          doc.text(`TOTAL ACTIVO: ${formatter.format(totalActivo)}`, 16, y);
          doc.text(`TOTAL PASIVO + PN: ${formatter.format(totalPasivo + totalPatri)}`, 100, y);
      }

    } else if (tipo === 'venta' || tipo === 'compra') {
      doc.text("Fecha", 16, 52);
      doc.text("Comprobante", 40, 52);
      doc.text(tipo === 'venta' ? "Cliente" : "Proveedor", 75, 52);
      doc.text("Neto", 130, 52);
      doc.text("IVA", 155, 52);
      doc.text("Total", 175, 52);
      doc.setFont("helvetica", "normal");

      let sumNeto = 0, sumIva = 0, sumTotal = 0;
      if (!datos || datos.length === 0) {
          doc.text("Sin movimientos", 16, y);
      } else {
          datos.forEach((item: any) => {
            if (y > 270) { doc.addPage(); y = 20; }
            doc.text(formatearFechaLocal(item.fecha), 16, y);
            doc.text(item.comprobante, 40, y);
            const tercero = item.cliente || item.proveedor || "";
            doc.text(tercero.substring(0, 20), 75, y);
            doc.text(formatter.format(item.neto), 130, y);
            doc.text(formatter.format(item.iva), 155, y);
            doc.text(formatter.format(item.total), 175, y);
            sumNeto += item.neto; sumIva += item.iva; sumTotal += item.total;
            y += 8;
          });

          doc.line(14, y, 196, y);
          y += 8;
          doc.setFont("helvetica", "bold");
          doc.text("TOTALES:", 95, y);
          doc.text(formatter.format(sumNeto), 130, y);
          doc.text(formatter.format(sumIva), 155, y);
          doc.text(formatter.format(sumTotal), 175, y);
      }

    } else if (tipo === 'diario') {
        const diarioBody: any[] = [];
        
        const asientosPorFecha: Record<string, any[]> = {};
        if (datos) {
            datos.forEach((a: any) => {
                const f = formatearFechaLocal(a.fecha);
                if (!asientosPorFecha[f]) asientosPorFecha[f] = [];
                asientosPorFecha[f].push(a);
            });
        }

        Object.keys(asientosPorFecha).forEach(fecha => {
            diarioBody.push([
                { content: `ASIENTOS DEL ${fecha}`, colSpan: 3, styles: { fillColor: [226, 232, 240], fontStyle: 'bold', textColor: [15, 23, 42], halign: 'center' } }
            ]);

            let totalDiaDebe = 0;
            let totalDiaHaber = 0;

            asientosPorFecha[fecha].forEach((a: any) => {
                const numAsiento = a.numeroAsiento != null ? `#${a.numeroAsiento.toString().padStart(4, '0')}` : '';
                const desc = a.descripcion ? ` - ${a.descripcion}` : '';
                const tituloAsiento = numAsiento ? `Asiento ${numAsiento}${desc}` : (a.descripcion || "Asiento Contable");

                diarioBody.push([
                    { content: tituloAsiento, colSpan: 3, styles: { fillColor: [241, 245, 249], fontStyle: 'bold' } }
                ]);

                if (a.lineas && Array.isArray(a.lineas)) {
                    a.lineas.forEach((linea: any) => {
                        diarioBody.push([
                            linea.nombreCuenta || linea.cuenta || "Cuenta",
                            linea.debe > 0 ? formatter.format(linea.debe) : "-",
                            linea.haber > 0 ? formatter.format(linea.haber) : "-"
                        ]);
                    });
                }

                const totDebe = a.totalDebe !== undefined ? a.totalDebe : (a.lineas ? a.lineas.reduce((s: number, l: any) => s + (l.debe || 0), 0) : (a.debe || 0));
                const totHaber = a.totalHaber !== undefined ? a.totalHaber : (a.lineas ? a.lineas.reduce((s: number, l: any) => s + (l.haber || 0), 0) : (a.haber || 0));
                
                totalDiaDebe += totDebe;
                totalDiaHaber += totHaber;

                diarioBody.push([
                    { content: "Totales:", styles: { halign: 'right', fontStyle: 'bold' } },
                    { content: formatter.format(totDebe), styles: { fontStyle: 'bold' } },
                    { content: formatter.format(totHaber), styles: { fontStyle: 'bold' } }
                ]);
                
                diarioBody.push([{ content: "", colSpan: 3, styles: { minCellHeight: 2, fillColor: [255, 255, 255] } }]);
            });

            // 5. Total del Día
            diarioBody.push([
                { content: `TOTAL DEL DÍA ${fecha}:`, styles: { halign: 'right', fontStyle: 'bold', fillColor: [210, 215, 225] } },
                { content: formatter.format(totalDiaDebe), styles: { fontStyle: 'bold', fillColor: [210, 215, 225] } },
                { content: formatter.format(totalDiaHaber), styles: { fontStyle: 'bold', fillColor: [210, 215, 225] } }
            ]);
            diarioBody.push([{ content: "", colSpan: 3, styles: { minCellHeight: 4, fillColor: [255, 255, 255] } }]);
        });


        if (diarioBody.length === 0) {
            doc.text("Sin movimientos", 16, y);
        } else {
            autoTable(doc, {
                startY: y,
                head: [["Cuenta", "Debe", "Haber"]],
                body: diarioBody,
                theme: 'grid',
                styles: { fontSize: 9 },
                headStyles: { fillColor: [15, 23, 42] }
            });
        }
    } else {
      // Caja, Banco
      doc.text("Fecha", 16, 52);
      doc.text("Descripción", 45, 52);
      doc.text("Ingreso", 140, 52);
      doc.text("Egreso", 170, 52);

      doc.setFont("helvetica", "normal");
      let total1 = 0, total2 = 0;

      if (!datos || datos.length === 0) {
          doc.text("Sin movimientos", 16, y);
      } else {
          datos.forEach((item: any) => {
            if (y > 270) { doc.addPage(); y = 20; }
            doc.text(formatearFechaLocal(item.fecha), 16, y);
            doc.text(item.descripcion.length > 45 ? item.descripcion.substring(0, 42) + '...' : item.descripcion, 45, y);

            doc.text(formatter.format(item.ingreso || 0), 140, y);
            doc.text(formatter.format(item.egreso || 0), 170, y);
            total1 += (item.ingreso || 0); total2 += (item.egreso || 0);
            
            y += 8;
          });

          doc.line(14, y, 196, y);
          y += 8;
          doc.setFont("helvetica", "bold");
          doc.text("TOTALES:", 100, y);
          doc.text(formatter.format(total1), 140, y);
          doc.text(formatter.format(total2), 170, y);
      }
    }
  });

  // Abrir en nueva pestaña un solo PDF maestro
  window.open(URL.createObjectURL(doc.output('blob')), '_blank');
}