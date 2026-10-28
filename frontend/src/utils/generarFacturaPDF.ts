import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { ALTO_PAGINA, ANCHO_PAGINA, abrirPDF, datosDelNegocio, estiloTabla, fecha, formatearCuit, importe, pesos } from './pdfComun';

// Acepta tanto la factura del listado (/api/facturas) como la que devuelve
// /api/asientos/{id}/factura: los datos del tercero vienen con nombres distintos.
interface FacturaParaImprimir {
  id: number;
  fecha: string;
  tipoOperacion?: string;
  tipoComprobante?: string;
  tipo?: string;
  condicionPago?: string;
  numeroFactura?: string | null;
  cliente?: string | any;
  clienteNombre?: string;
  cuit?: string | null;
  clienteObj?: any;
  proveedor?: any;
  proveedorObj?: any;
  socio?: any;
  socioObj?: any;
  subtotal?: number;
  iva?: number;
  total: number;
  cae?: string | null;
  fechaVencimientoCae?: string | null;
  anulada?: boolean;
  fechaAnulacion?: string | null;
  items?: { descripcion: string; cantidad: number; precioUnitario: number; subtotal: number }[];
}

const CODIGO_COMPROBANTE: Record<string, string> = { A: '001', B: '006', C: '011' };
const CONDICION_DE_VENTA: Record<string, string> = {
  ContadoCaja: 'Contado',
  TransferenciaBanco: 'Transferencia bancaria',
  CuentaCorriente: 'Cuenta corriente',
};

// Patrones Interleaved 2 of 5 (n = fino, w = ancho), el que usa el código de barras de ARCA.
const PATRON_ITF = ['nnwwn', 'wnnnw', 'nwnnw', 'wwnnn', 'nnwnw', 'wnwnn', 'nwwnn', 'nnnww', 'wnnwn', 'nwnwn'];

function hash32(texto: string, semilla: number) {
  let h = (2166136261 ^ semilla) >>> 0;
  for (let i = 0; i < texto.length; i++) h = Math.imul(h ^ texto.charCodeAt(i), 16777619) >>> 0;
  return h;
}

// CAE simulado: 14 dígitos que salen siempre igual para el mismo comprobante (no cambia al reimprimir).
function caeSimulado(clave: string) {
  const n = hash32(clave, 1) * 100000 + (hash32(clave, 2) % 100000);
  return String(n % 100000000000000).padStart(14, '0');
}

function aAAAAMMDD(fechaIso: string, sumarDias: number) {
  const d = new Date(fechaIso);
  const base = Number.isNaN(d.getTime()) ? new Date() : d;
  base.setUTCDate(base.getUTCDate() + sumarDias);
  return { compacta: `${base.getUTCFullYear()}${String(base.getUTCMonth() + 1).padStart(2, '0')}${String(base.getUTCDate()).padStart(2, '0')}`, legible: `${String(base.getUTCDate()).padStart(2, '0')}/${String(base.getUTCMonth() + 1).padStart(2, '0')}/${base.getUTCFullYear()}` };
}

// Dígito verificador del código de barras de ARCA: impares x3 + pares, complemento a 10.
function digitoVerificador(codigo: string) {
  let impares = 0, pares = 0;
  [...codigo].forEach((c, i) => { if (i % 2 === 0) impares += Number(c); else pares += Number(c); });
  return String((10 - ((impares * 3 + pares) % 10)) % 10);
}

function dibujarCodigoITF(doc: jsPDF, digitos: string, x: number, y: number, alto: number) {
  const fino = 0.33, ancho = fino * 2.5;
  doc.setFillColor(0, 0, 0);
  let cursor = x;
  const barra = (w: number, pintar: boolean) => { if (pintar) doc.rect(cursor, y, w, alto, 'F'); cursor += w; };
  [fino, fino, fino, fino].forEach((w, i) => barra(w, i % 2 === 0)); // inicio
  for (let i = 0; i < digitos.length; i += 2) {
    const barras = PATRON_ITF[Number(digitos[i])], espacios = PATRON_ITF[Number(digitos[i + 1])];
    for (let k = 0; k < 5; k++) {
      barra(barras[k] === 'w' ? ancho : fino, true);
      barra(espacios[k] === 'w' ? ancho : fino, false);
    }
  }
  barra(ancho, true); barra(fino, false); barra(fino, true); // fin
  return cursor - x;
}

interface Parte { nombre: string; cuit: string; domicilio: string; condicionIva: string }

const Cuit_digitos = (v: string) => v.replace(/[^0-9]/g, '');

export function generarFacturaPDF(factura: FacturaParaImprimir) {
  const doc = new jsPDF();
  const negocio = datosDelNegocio();
  const letra = (factura.tipoComprobante || factura.tipo || 'C').toUpperCase();
  const esCompra = factura.tipoOperacion === 'Compra' || !!(factura.proveedor || factura.proveedorObj);

  // Tercero: cliente o proveedor con sus datos fiscales, socio (cuota) o consumidor final.
  const empresa = factura.proveedor ?? factura.proveedorObj
    ?? (typeof factura.cliente === 'object' && factura.cliente ? factura.cliente : null) ?? factura.clienteObj;
  const socio = factura.socio ?? factura.socioObj;
  const tercero: Parte = empresa
    ? {
        nombre: empresa.razonSocial,
        cuit: formatearCuit(empresa.cuit) || '-',
        domicilio: [empresa.direccion, empresa.localidad].filter(Boolean).join(' - ') || '-',
        condicionIva: empresa.condicionIva || 'Consumidor Final',
      }
    : {
        nombre: socio ? `${socio.nombre ?? ''} ${socio.apellido ?? ''}`.trim()
          : (factura.clienteNombre || (typeof factura.cliente === 'string' ? factura.cliente : '') || 'Consumidor Final'),
        cuit: socio?.dni ? `DNI ${socio.dni}` : (formatearCuit(factura.cuit) || '-'),
        domicilio: '-',
        condicionIva: 'Consumidor Final',
      };
  const propio: Parte = {
    nombre: negocio.razonSocial,
    cuit: negocio.cuit,
    domicilio: negocio.domicilio || '-',
    condicionIva: negocio.condicionIva,
  };

  // En una compra el comprobante lo emitió el proveedor: se imprime como copia del recibido.
  const emisor = esCompra ? tercero : propio;
  const receptor = esCompra ? propio : tercero;
  const [puntoVenta, numero] = (factura.numeroFactura ?? `${String(negocio.puntoVenta).padStart(4, '0')}-${String(factura.id).padStart(8, '0')}`).split('-');

  doc.setTextColor(0);
  doc.setDrawColor(0);

  if (esCompra) {
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(8);
    doc.text('COPIA DE COMPROBANTE RECIBIDO - REGISTRADO EN COMPRAS', ANCHO_PAGINA / 2, 8, { align: 'center' });
  }

  // 1. Cabecera: emisor a la izquierda, letra al centro, comprobante a la derecha.
  doc.setLineWidth(0.4);
  doc.rect(10, 10, 190, 50);
  doc.line(105, 30, 105, 60);
  doc.rect(98, 10, 14, 14);
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(22);
  doc.text(letra, 105, 20.5, { align: 'center' });
  doc.setFontSize(6.5);
  doc.text(`COD. ${CODIGO_COMPROBANTE[letra] ?? '-'}`, 105, 27.5, { align: 'center' });

  doc.setFontSize(13);
  doc.text(doc.splitTextToSize(emisor.nombre, 80), 14, 19);
  doc.setFont('helvetica', 'normal');
  doc.setFontSize(8.5);
  let y = 32;
  // El ancho de la etiqueta se mide en negrita, que es como se imprime.
  const lineaIzq = (etiqueta: string, valor: string) => {
    doc.setFont('helvetica', 'bold');
    doc.text(etiqueta, 14, y);
    const ancho = doc.getTextWidth(etiqueta) + 1.5;
    doc.setFont('helvetica', 'normal');
    doc.text(doc.splitTextToSize(valor, 88 - ancho)[0] ?? '', 14 + ancho, y);
    y += 5;
  };
  lineaIzq('Razón social:', emisor.nombre);
  lineaIzq('Domicilio comercial:', emisor.domicilio);
  lineaIzq('Condición frente al IVA:', emisor.condicionIva);
  if (!esCompra && (negocio.telefono || negocio.email)) lineaIzq('Contacto:', [negocio.telefono, negocio.email].filter(Boolean).join('  ·  '));

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(16);
  doc.text('FACTURA', 116, 19);
  doc.setFontSize(9);
  y = 30;
  const lineaDer = (etiqueta: string, valor: string) => {
    doc.setFont('helvetica', 'bold');
    doc.text(etiqueta, 116, y);
    const ancho = doc.getTextWidth(etiqueta) + 1.5;
    doc.setFont('helvetica', 'normal');
    doc.text(doc.splitTextToSize(valor, 82 - ancho)[0] ?? '', 116 + ancho, y);
    y += 5;
  };
  lineaDer('Punto de venta:', `${puntoVenta}    Comp. Nro: ${numero}`);
  lineaDer('Fecha de emisión:', fecha(factura.fecha));
  lineaDer('CUIT:', emisor.cuit);
  if (!esCompra) {
    lineaDer('Ingresos Brutos:', negocio.ingresosBrutos || '-');
    lineaDer('Inicio de actividades:', fecha(negocio.inicioActividades) || '-');
  }

  // 2. Receptor
  doc.setLineWidth(0.3);
  doc.rect(10, 63, 190, 24);
  doc.setFontSize(9);
  y = 69;
  const lineaReceptor = (etiqueta: string, valor: string, x: number, yy: number, ancho: number) => {
    doc.setFont('helvetica', 'bold');
    doc.text(etiqueta, x, yy);
    const anchoEtiqueta = doc.getTextWidth(etiqueta) + 1.5;
    doc.setFont('helvetica', 'normal');
    doc.text(doc.splitTextToSize(valor, ancho - anchoEtiqueta)[0] ?? '', x + anchoEtiqueta, yy);
  };
  lineaReceptor('Apellido y nombre / Razón social:', receptor.nombre, 14, y, 180);
  lineaReceptor('CUIT / DNI:', receptor.cuit, 14, y + 6, 80);
  lineaReceptor('Condición frente al IVA:', receptor.condicionIva, 105, y + 6, 92);
  lineaReceptor('Domicilio:', receptor.domicilio, 14, y + 12, 88);
  lineaReceptor('Condición de venta:', CONDICION_DE_VENTA[factura.condicionPago ?? ''] ?? '-', 105, y + 12, 92);

  // Alícuota de la factura (la más cercana a IVA / Subtotal).
  const subtotal = factura.subtotal ?? factura.total;
  const iva = factura.iva ?? 0;
  const tasa = subtotal > 0 && iva > 0 ? [10.5, 21, 27].reduce((m, a) => (Math.abs(a - (iva / subtotal) * 100) < Math.abs(m - (iva / subtotal) * 100) ? a : m), 21) : 0;
  const etiquetaTasa = `${String(tasa).replace('.', ',')}%`;

  // 3. Detalle como la hoja FACTURA: en A y B, IVA y total de cada renglón (la C no discrimina IVA).
  const items = factura.items?.length ? factura.items
    : [{ descripcion: 'Concepto facturado', cantidad: 1, precioUnitario: factura.subtotal ?? factura.total, subtotal: factura.subtotal ?? factura.total }];
  autoTable(doc, {
    ...estiloTabla(),
    startY: 91,
    margin: { left: 10, right: 10, top: 15, bottom: 52 },
    styles: { font: 'helvetica', fontSize: 9, textColor: 0, lineColor: 150, lineWidth: 0.15, cellPadding: 1.8 },
    head: [['Cant.', 'Descripción', { content: 'P. Unitario', styles: { halign: 'right' } }, { content: 'Subtotal', styles: { halign: 'right' } },
      ...(letra === 'C' ? [] : [{ content: `IVA ${etiquetaTasa}`, styles: { halign: 'right' as const } }, { content: 'Total', styles: { halign: 'right' as const } }])]],
    body: items.map(i => [
      { content: String(i.cantidad), styles: { halign: 'center' } },
      i.descripcion || 'Concepto',
      { content: importe(i.precioUnitario), styles: { halign: 'right' } },
      { content: importe(i.subtotal), styles: { halign: 'right' } },
      ...(letra === 'C' ? [] : [
        { content: importe(i.subtotal * tasa / 100), styles: { halign: 'right' as const } },
        { content: importe(i.subtotal * (1 + tasa / 100)), styles: { halign: 'right' as const } },
      ]),
    ]),
    columnStyles: letra === 'C'
      ? { 0: { cellWidth: 16 }, 2: { cellWidth: 32 }, 3: { cellWidth: 32 } }
      : { 0: { cellWidth: 14 }, 2: { cellWidth: 26 }, 3: { cellWidth: 26 }, 4: { cellWidth: 24 }, 5: { cellWidth: 26 } },
  });

  // 4. Totales
  const filas: [string, string][] = letra === 'C'
    ? []
    : [['Importe neto gravado', pesos(subtotal)], [`IVA ${String(tasa).replace('.', ',')} %`, pesos(iva)]];
  filas.push(['IMPORTE TOTAL', pesos(factura.total)]);

  y = (doc as any).lastAutoTable.finalY + 6;
  if (y + filas.length * 7 + 52 > ALTO_PAGINA) { doc.addPage(); y = 20; }
  doc.setLineWidth(0.3);
  doc.rect(115, y, 85, filas.length * 7 + 3);
  filas.forEach(([etiqueta, valor], i) => {
    const ultima = i === filas.length - 1;
    doc.setFont('helvetica', ultima ? 'bold' : 'normal');
    doc.setFontSize(ultima ? 11 : 9.5);
    doc.text(etiqueta, 119, y + 6.5 + i * 7);
    doc.text(valor, 196, y + 6.5 + i * 7, { align: 'right' });
  });
  if (letra === 'C') {
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8);
    doc.text('Comprobante C: no discrimina IVA.', 12, y + 6);
  }

  // 5. Anulación y pie (CAE si lo tiene; si no, se aclara que es un comprobante interno).
  if (factura.anulada) {
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(10);
    doc.text(`Comprobante anulado${factura.fechaAnulacion ? ` el ${fecha(factura.fechaAnulacion)}` : ''}.`, 12, y + filas.length * 7 + 12);
    doc.setPage(1);
    doc.setTextColor(150);
    doc.setFontSize(64);
    doc.text('ANULADA', 105, 175, { align: 'center', angle: 25 });
    doc.setTextColor(0);
  }

  const cuitEmisor = Cuit_digitos(emisor.cuit).padStart(11, '0').slice(-11);
  const cae = factura.cae ?? caeSimulado(`${letra}|${puntoVenta}|${numero}|${cuitEmisor}`);
  const vencimiento = factura.fechaVencimientoCae ? { compacta: factura.fechaVencimientoCae.slice(0, 10).replace(/-/g, ''), legible: fecha(factura.fechaVencimientoCae) } : aAAAAMMDD(factura.fecha, 10);
  const codigoBase = `${cuitEmisor}${(CODIGO_COMPROBANTE[letra] ?? '011')}${puntoVenta.replace(/[^0-9]/g, '').padStart(5, '0').slice(-5)}${cae}${vencimiento.compacta}`;
  const codigoBarras = codigoBase + digitoVerificador(codigoBase);
  const esSimulado = !factura.cae;

  const total = doc.getNumberOfPages();
  for (let p = 1; p <= total; p++) {
    doc.setPage(p);
    doc.setTextColor(0);
    doc.setDrawColor(0);
    doc.setLineWidth(0.15);
    doc.line(10, ALTO_PAGINA - 22, 200, ALTO_PAGINA - 22);
    doc.setFontSize(8);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(7.5);
    doc.text(esSimulado
      ? 'Práctica académica: CAE y código de barras SIMULADOS, sin validez fiscal ante ARCA.'
      : (esCompra ? 'Copia interna del comprobante recibido, registrada en el Libro IVA Compras.' : 'Comprobante emitido por el sistema de gestión.'), 10, ALTO_PAGINA - 16);
    if (p === total) {
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(9);
      doc.text(`CAE N°: ${cae}${esSimulado ? ' (simulado)' : ''}`, 10, ALTO_PAGINA - 46);
      doc.text(`Vencimiento CAE: ${vencimiento.legible}`, 200, ALTO_PAGINA - 46, { align: 'right' });
      const anchoCodigo = dibujarCodigoITF(doc, codigoBarras, 10, ALTO_PAGINA - 43, 11);
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(7);
      doc.text(codigoBarras, 10 + anchoCodigo / 2, ALTO_PAGINA - 28, { align: 'center' });
    }
    doc.setFontSize(8);
    doc.setFont('helvetica', 'normal');
    doc.text(`Página ${p} de ${total}`, 200, ALTO_PAGINA - 11, { align: 'right' });
  }

  abrirPDF(doc, `Factura ${letra} ${puntoVenta}-${numero}`);
}
