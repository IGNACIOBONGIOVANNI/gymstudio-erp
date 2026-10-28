import jsPDF from 'jspdf';
import type { UserOptions } from 'jspdf-autotable';
import { obtenerConfiguracion } from '../features/configuracion/configuracionStorage';
import { dibujarLogo } from './logoPDF';

// Todo lo impreso por el sistema sale en blanco y negro para que se fotocopie bien:
// texto negro, líneas grises finas y sin fondos oscuros.

export const MARGEN = 14;
export const ANCHO_PAGINA = 210;
export const ALTO_PAGINA = 297;
export const ALTO_ENCABEZADO = 36; // donde empieza el contenido debajo del encabezado

const numero = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const moneda = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' });

export const importe = (n: number | null | undefined) => numero.format(Number(n) || 0);
export const pesos = (n: number | null | undefined) => moneda.format(Number(n) || 0);

/** "20111111112" -> "20-11111111-2"; cualquier otro dato (un DNI) queda como vino. */
export function formatearCuit(valor: string | null | undefined): string {
  if (!valor) return '';
  const digitos = String(valor).replace(/\D/g, '');
  return digitos.length === 11 ? `${digitos.slice(0, 2)}-${digitos.slice(2, 10)}-${digitos.slice(10)}` : String(valor);
}

/** Fecha de la API ("2026-10-20" o "2026-10-20T12:00:00Z") a dd/mm/aaaa sin corrimiento de zona horaria. */
export function fecha(valor: string | null | undefined): string {
  if (!valor) return '';
  const [a, m, d] = String(valor).slice(0, 10).split('-');
  return d && m && a ? `${d}/${m}/${a}` : String(valor);
}

export function datosDelNegocio() {
  const c = obtenerConfiguracion();
  return {
    razonSocial: c.razonSocial || 'Sin razón social',
    nombreFantasia: c.nombreFantasia,
    cuit: formatearCuit(c.cuit),
    condicionIva: c.condicionIva,
    domicilio: c.direccion,
    telefono: c.telefono,
    email: c.email,
    ingresosBrutos: c.ingresosBrutos,
    inicioActividades: c.inicioActividades,
    puntoVenta: Math.max(1, parseInt(c.puntoVenta, 10) || 1),
  };
}

/** Estilo de tabla común: grilla gris fina, encabezado blanco en negrita con borde marcado. */
export function estiloTabla(extra: Partial<UserOptions> = {}): Partial<UserOptions> {
  return {
    theme: 'grid',
    margin: { left: MARGEN, right: MARGEN, top: ALTO_ENCABEZADO + 2, bottom: 18 },
    styles: { font: 'helvetica', fontSize: 8.5, textColor: 0, lineColor: 150, lineWidth: 0.15, cellPadding: 1.6, overflow: 'linebreak' },
    headStyles: { fillColor: 255, textColor: 0, fontStyle: 'bold', lineColor: 0, lineWidth: 0.35 },
    footStyles: { fillColor: 255, textColor: 0, fontStyle: 'bold', lineColor: 0, lineWidth: 0.35 },
    bodyStyles: { fillColor: 255 },
    showFoot: 'lastPage',
    ...extra,
  };
}

/**
 * Encabezado de libro en cada página (datos del negocio a la izquierda, libro y
 * período a la derecha) y pie con fecha de emisión y "Página N de M". Se dibuja al
 * final, cuando ya se sabe cuántas páginas tiene el documento.
 */
export function encabezadosYPies(
  doc: jsPDF,
  tituloDePagina: (pagina: number) => { titulo: string; periodo: string },
  notaDerecha = 'Importes en pesos ($)',
) {
  const negocio = datosDelNegocio();
  const total = doc.getNumberOfPages();
  const ahora = new Date();
  const emitido = `${ahora.toLocaleDateString('es-AR')} ${ahora.toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit' })}`;

  for (let p = 1; p <= total; p++) {
    doc.setPage(p);
    const { titulo, periodo } = tituloDePagina(p);
    doc.setTextColor(0);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(12);
    dibujarLogo(doc, MARGEN, 10, 13);
    doc.text(negocio.razonSocial, MARGEN + 16, 16);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8);
    doc.text(`CUIT ${negocio.cuit}  ·  ${negocio.condicionIva}`, MARGEN + 16, 21);
    if (negocio.domicilio) doc.text(doc.splitTextToSize(negocio.domicilio, 100)[0], MARGEN + 16, 25);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(12);
    doc.text(titulo, ANCHO_PAGINA - MARGEN, 16, { align: 'right' });
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8.5);
    doc.text(periodo, ANCHO_PAGINA - MARGEN, 21, { align: 'right' });
    doc.text(notaDerecha, ANCHO_PAGINA - MARGEN, 25, { align: 'right' });

    doc.setDrawColor(0);
    doc.setLineWidth(0.4);
    doc.line(MARGEN, 29, ANCHO_PAGINA - MARGEN, 29);

    doc.setLineWidth(0.15);
    doc.line(MARGEN, ALTO_PAGINA - 12, ANCHO_PAGINA - MARGEN, ALTO_PAGINA - 12);
    doc.setFontSize(7.5);
    doc.text(`Emitido el ${emitido} por GymStudio ERP`, MARGEN, ALTO_PAGINA - 8);
    doc.text(`Página ${p} de ${total}`, ANCHO_PAGINA - MARGEN, ALTO_PAGINA - 8, { align: 'right' });
  }
}

export function abrirPDF(doc: jsPDF, nombreArchivo: string) {
  doc.setProperties({ title: nombreArchivo });
  const url = URL.createObjectURL(doc.output('blob'));
  const ventana = window.open(url, '_blank');
  // Si el navegador bloquea la ventana emergente, se descarga el archivo.
  if (!ventana) doc.save(`${nombreArchivo}.pdf`);
}
