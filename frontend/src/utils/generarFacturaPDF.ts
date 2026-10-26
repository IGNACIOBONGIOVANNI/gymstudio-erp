import jsPDF from 'jspdf';

interface FacturaItemDto {
  descripcion: string;
  cantidad: number;
  precioUnitario: number;
  subtotal: number;
}

interface FacturaDto {
  id: number;
  cliente?: string | any;
  clienteNombre?: string;
  nombreCliente?: string;
  cuit?: string;
  dni?: string;
  clienteObj?: any;
  proveedor?: any;
  proveedorObj?: any;
  socio?: any;
  socioObj?: any;
  // 🔥 Nuevas posibles ubicaciones de los datos del socio
  nombre?: string;
  apellido?: string;
  fecha: string;
  total: number;
  subtotal: number;
  tipo?: string;
  tipoOperacion?: string;
  items: FacturaItemDto[];
}

export function generarFacturaPDF(factura: FacturaDto) {
  const doc = new jsPDF();
  const formatter = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' });

  console.log("DATOS RECIBIDOS EN EL PDF:", factura);

  let letraFactura = (factura.tipo || 'C').toUpperCase();
  let codigoAFIP = "011";
  let condicionIVAEmisor = "Responsable Monotributo";

  let objetoCompleto = null;
  let esSocio = false;

  // 1. Buscamos si viene como objeto anidado
  if (factura.proveedor && typeof factura.proveedor === 'object') {
    objetoCompleto = factura.proveedor;
  } else if (factura.proveedorObj && typeof factura.proveedorObj === 'object') {
    objetoCompleto = factura.proveedorObj;
  } else if (factura.cliente && typeof factura.cliente === 'object') {
    objetoCompleto = factura.cliente;
  } else if (factura.clienteObj && typeof factura.clienteObj === 'object') {
    objetoCompleto = factura.clienteObj;
  } else if (factura.socio && typeof factura.socio === 'object') {
    objetoCompleto = factura.socio;
    esSocio = true;
  } else if (factura.socioObj && typeof factura.socioObj === 'object') {
    objetoCompleto = factura.socioObj;
    esSocio = true;
  }

  let nombre = "Consumidor Final";
  let cuit = "Consumidor Final";
  let condIva = "Consumidor Final";
  let direccion = "";

  // 2. Extraemos los datos
  if (objetoCompleto) {
    if (esSocio) {
      const nombreSocio = objetoCompleto.nombre || "";
      const apellidoSocio = objetoCompleto.apellido || "";
      nombre = `${nombreSocio} ${apellidoSocio}`.trim() || "Consumidor Final";
      cuit = objetoCompleto.dni || objetoCompleto.cuit || "Consumidor Final";
      condIva = "Consumidor Final";
    } else {
      nombre = objetoCompleto.razonSocial || "Consumidor Final";
      cuit = objetoCompleto.cuit || "Consumidor Final";
      condIva = objetoCompleto.condicionIva || "Consumidor Final";
      const dom = objetoCompleto.direccion || "";
      const loc = objetoCompleto.localidad || "";
      direccion = dom + (dom && loc ? " - " : "") + loc;
    }
  } else {
    // 🔥 MODO CAZADOR POTENCIADO
    const nombreSuelto = factura.clienteNombre || factura.nombreCliente || factura.cliente || factura.nombre;
    const apellidoSuelto = factura.apellido || "";
    
    if (nombreSuelto && typeof nombreSuelto === 'string' && nombreSuelto.trim() !== "Consumidor Final") {
      nombre = `${nombreSuelto} ${apellidoSuelto}`.trim();
    }
    
    const cuitSuelto = factura.cuit || factura.dni;
    if (cuitSuelto && String(cuitSuelto).trim() !== "") {
      cuit = String(cuitSuelto);
    }
    
    // Si no es un comprobante 'A', asumimos Consumidor Final para los demás
    if(letraFactura !== 'A'){
       condIva = "Consumidor Final";
    }
  }

  // --- REGLA AFIP: FACTURA A A CONSUMIDOR FINAL PASA A SER B ---
  if (letraFactura === 'A') {
      condicionIVAEmisor = "Responsable Inscripto";
      if (condIva === "Consumidor Final") {
          letraFactura = 'B';
          codigoAFIP = "006";
      } else {
          codigoAFIP = "001";
      }
  } else if (letraFactura === 'B') {
    codigoAFIP = "006";
    condicionIVAEmisor = "Responsable Inscripto";
  }

    // --- LOGICA DE COMPRA/VENTA ---
  const esCompra = factura.tipoOperacion === 'Compra' || factura.proveedor || factura.proveedorObj;
  
  let emisorNombre = "GYM STUDIO S.R.L.";
  let emisorRazonSocial = "GYM STUDIO S.R.L.";
  let emisorDomicilio = "Av. San Martín 1500, San Justo";
  let emisorCondIva = condicionIVAEmisor;
  let emisorCuit = "30-12345678-9";
  let emisorIIBB = "30-12345678-9";

  let receptorNombre = nombre;
  let receptorCuit = cuit;
  let receptorDomicilio = direccion;
  let receptorCondIva = condIva;

  if (esCompra) {
      emisorNombre = nombre;
      emisorRazonSocial = nombre;
      emisorDomicilio = direccion || "No especificado";
      emisorCondIva = condIva;
      emisorCuit = cuit;
      emisorIIBB = cuit; // Simplificacion

      receptorNombre = "GYM STUDIO S.R.L.";
      receptorCuit = "30-12345678-9";
      receptorDomicilio = "Av. San Martín 1500, San Justo";
      receptorCondIva = "Responsable Inscripto";
  }

  // 1. Cabecera
  doc.rect(10, 10, 190, 45);
  doc.line(105, 24, 105, 55);

  doc.setFontSize(16);
  doc.setFont("helvetica", "bold");
  doc.text(emisorNombre, 15, 20);
  doc.setFontSize(9);
  doc.setFont("helvetica", "normal");
  doc.text("Razón Social: " + emisorRazonSocial, 15, 28);
  doc.text("Domicilio Comercial: " + emisorDomicilio, 15, 33);
  doc.text(`Condición frente al IVA: ${emisorCondIva}`, 15, 38);

  doc.rect(98, 10, 14, 14);
  doc.setFontSize(22);
  doc.setFont("helvetica", "bold");
  doc.text(letraFactura, 101, 21);

  doc.setFontSize(8);
  doc.text(`Cod. ${codigoAFIP}`, 99, 28);

  doc.setFontSize(14);
  doc.setFont("helvetica", "bold");
  doc.text("FACTURA", 120, 20);
  doc.setFontSize(10);
  const nroFacturaFormat = String(factura.id || "0").padStart(8, '0');
  doc.text(`Punto de Venta: 0001  Comp. Nro: ${nroFacturaFormat}`, 120, 28);

  let fechaFormateada = "";
  if (factura.fecha) {
    const soloFecha = factura.fecha.split('T')[0];
    const [year, month, day] = soloFecha.split('-');
    fechaFormateada = `${day}/${month}/${year}`;
  } else {
    fechaFormateada = new Date().toLocaleDateString('es-AR');
  }
  doc.text(`Fecha de Emisión: ${fechaFormateada}`, 120, 36);

  doc.setFontSize(9);
  doc.setFont("helvetica", "bold");
  doc.text("CUIT: " + emisorCuit, 120, 46);
  doc.setFont("helvetica", "normal");
  doc.text("Ingresos Brutos: " + emisorIIBB, 120, 53);

  // 2. Datos del Receptor
  doc.rect(10, 60, 190, 25);
  doc.setFontSize(10);

  doc.text("Nombre / Razón Social: " + receptorNombre, 15, 67);
  doc.text("CUIT / DNI: " + receptorCuit, 15, 75);
  doc.text("Domicilio: " + receptorDomicilio, 15, 83);
  doc.text("Condición de Venta: Contado", 120, 67);
  doc.text("Condición frente al IVA: " + receptorCondIva, 120, 75);

  // 3. Grilla de Conceptos
  doc.setFillColor(200, 200, 200);
  doc.rect(10, 90, 190, 10, 'F');
  doc.rect(10, 90, 190, 10);

  doc.setFontSize(9);
  doc.setFont("helvetica", "bold");
  doc.text("Código", 12, 96);
  doc.text("Detalle", 35, 96);
  doc.text("Precio Unit.", 130, 96);
  doc.text("Cant.", 155, 96);
  doc.text("Subtotal", 175, 96);

  doc.setFont("helvetica", "normal");
  doc.rect(10, 100, 190, 100);

  let y = 107;
  if (factura.items && factura.items.length > 0) {
    factura.items.forEach((item: any, index: number) => {
      const descripcion = item.descripcion || item.Descripcion || "Concepto general";
      const subtotal = item.subtotal || item.Subtotal || 0;
      const cantidad = item.cantidad || item.Cantidad || 1;
      const precioUnitario = item.precioUnitario || item.PrecioUnitario || subtotal;

      doc.text((index + 1).toString().padStart(3, '0'), 12, y);
      doc.text(descripcion, 35, y);
      doc.text(formatter.format(precioUnitario), 130, y);
      doc.text(cantidad.toString(), 158, y);
      doc.text(formatter.format(subtotal), 175, y);
      y += 8;
    });
  } else {
    doc.text("Servicios Varios / Ajuste Contable", 35, y);
    doc.text(formatter.format(factura.total || 0), 130, y);
    doc.text("1", 158, y);
    doc.text(formatter.format(factura.total || 0), 175, y);
  }

  // 4. Totales Dinámicos
  const total = factura.total || 0;

  if (letraFactura === 'A') {
    const neto = total / 1.21;
    const iva = total - neto;

    doc.rect(120, 210, 80, 25);
    doc.setFontSize(10);
    doc.text("Importe Neto Gravado:", 125, 217);
    doc.text(formatter.format(neto), 175, 217);

    doc.text("IVA 21%:", 125, 224);
    doc.text(formatter.format(iva), 175, 224);

    doc.setFont("helvetica", "bold");
    doc.text("TOTAL ABONADO:", 125, 231);
    doc.text(formatter.format(total), 175, 231);
  } else {
    doc.rect(120, 210, 80, 15);
    doc.setFontSize(12);
    doc.setFont("helvetica", "bold");
    doc.text("TOTAL ABONADO:", 125, 220);
    doc.text(formatter.format(total), 175, 220);
  }

  // 5. FOOTER AFIP: CAE Y CÓDIGO DE BARRAS
  const yFooter = 250;
  let xBarcode = 15;
  doc.setFillColor(0, 0, 0);
  const pattern = [1, 2, 1, 1, 3, 1, 2, 2, 1, 1, 1, 2, 3, 1, 1, 2, 1, 1, 2, 3, 1, 2, 1, 1, 2, 2, 1, 3, 1, 1, 2, 1, 1, 3, 2, 1, 1, 2];
  pattern.forEach(w => {
    doc.rect(xBarcode, yFooter, w * 0.6, 12, 'F');
    xBarcode += (w * 0.6) + 1.2;
  });

  doc.setFontSize(8);
  doc.setFont("helvetica", "normal");
  doc.text("30123456789 01 0001 73412589012345 20261015 6", 15, yFooter + 16);

  doc.setFont("helvetica", "bold");
  doc.text("CAE N°:", 140, yFooter + 5);
  doc.setFont("helvetica", "normal");
  doc.text("73412589012345", 155, yFooter + 5);

  doc.setFont("helvetica", "bold");
  doc.text("Fecha Vto. CAE:", 140, yFooter + 10);
  doc.setFont("helvetica", "normal");
  doc.text("15/10/2026", 165, yFooter + 10);

  const pdfBlobUrl = URL.createObjectURL(doc.output('blob'));
  window.open(pdfBlobUrl, '_blank');
}

