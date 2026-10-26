import jsPDF from 'jspdf';

interface ComprobanteData {
  socio: {
    nombre: string;
    apellido: string;
    dni: string;
  };
  plan: {
    nombre: string;
    precioMensual: number;
  };
}

export function generarComprobante(data: ComprobanteData) {
  const doc = new jsPDF();
  const formatter = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' });

  // Cabecera
  doc.setFontSize(22);
  doc.setFont("helvetica", "bold");
  doc.text("GYM STUDIO", 105, 20, { align: "center" });
  
  doc.setFontSize(14);
  doc.setFont("helvetica", "normal");
  doc.text("RECIBO DE PAGO - MEMBRESÍA", 105, 30, { align: "center" });

  // Línea separadora
  doc.setLineWidth(0.5);
  doc.line(20, 35, 190, 35);

  // Fecha
  doc.setFontSize(11);
  const fechaActual = new Date().toLocaleDateString('es-AR');
  const horaActual = new Date().toLocaleTimeString('es-AR');
  doc.text(`Fecha de Emisión: ${fechaActual} ${horaActual}`, 20, 45);

  // Datos del Cliente
  doc.setFont("helvetica", "bold");
  doc.text("Datos del Socio:", 20, 60);
  doc.setFont("helvetica", "normal");
  doc.text(`Nombre: ${data.socio.nombre} ${data.socio.apellido}`, 20, 70);
  doc.text(`DNI: ${data.socio.dni}`, 20, 78);

  // Detalles del Pago
  doc.setFont("helvetica", "bold");
  doc.text("Detalle del Cobro:", 20, 95);
  doc.setFont("helvetica", "normal");
  
  // Tabla simple
  doc.setFillColor(240, 240, 240);
  doc.rect(20, 100, 170, 10, 'F');
  doc.text("Concepto", 25, 107);
  doc.text("Importe", 150, 107);

  doc.text(`Renovación Plan: ${data.plan.nombre}`, 25, 120);
  doc.text(formatter.format(data.plan.precioMensual), 150, 120);

  // Línea separadora
  doc.line(20, 130, 190, 130);

  // Total
  doc.setFontSize(14);
  doc.setFont("helvetica", "bold");
  doc.text("TOTAL ABONADO:", 90, 145);
  doc.text(formatter.format(data.plan.precioMensual), 150, 145);

  // Pie de página
  doc.setFontSize(10);
  doc.setFont("helvetica", "italic");
  doc.text("Este comprobante es un documento no válido como factura fiscal.", 105, 270, { align: "center" });
  doc.text("¡Gracias por confiar en nosotros!", 105, 276, { align: "center" });

  // Descargar el PDF
  doc.save(`Comprobante_${data.socio.dni}_${fechaActual.replace(/\//g, '-')}.pdf`);
}