// Escritor mínimo de archivos .xlsx (Excel), sin dependencias: un .xlsx es un ZIP con unos pocos XML.
// Alcanza para libros contables: texto, números, fechas, encabezados en negrita y anchos de columna.

export type Celda =
  | string
  | number
  | null
  | undefined
  | { v: string | number | null; tipo?: 'importe' | 'entero' | 'fecha' | 'texto'; negrita?: boolean };

export interface Hoja {
  nombre: string;
  filas: Celda[][];
  /** Ancho de cada columna en caracteres; si falta se calcula con el contenido. */
  anchos?: number[];
}

// Estilos: 0 normal, 1 negrita, 2 importe, 3 importe en negrita, 4 fecha, 5 entero, 6 entero en negrita, 7 fecha en negrita.
const ESTILOS = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<numFmts count="2"><numFmt numFmtId="164" formatCode="#,##0.00"/><numFmt numFmtId="165" formatCode="dd/mm/yyyy"/></numFmts>
<fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>
<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
<cellXfs count="8">
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
<xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>
<xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
<xf numFmtId="164" fontId="1" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1"/>
<xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
<xf numFmtId="1" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
<xf numFmtId="1" fontId="1" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1"/>
<xf numFmtId="165" fontId="1" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1"/>
</cellXfs>
<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>`;

const escapar = (s: string) =>
  // Quita los caracteres de control que XML no admite y escapa los especiales.
  // eslint-disable-next-line no-control-regex
  s.replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/g, '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** Número de columna (0 = A) a letras: 0 -> A, 26 -> AA. */
function letras(n: number): string {
  let s = '';
  for (let i = n + 1; i > 0; i = Math.floor((i - 1) / 26)) s = String.fromCharCode(65 + ((i - 1) % 26)) + s;
  return s;
}

/** "2026-10-27" o "2026-10-27T12:00:00Z" -> número de serie de Excel (días desde 1900). */
export function fechaAExcel(valor: string | null | undefined): number | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(valor ?? '');
  if (!m) return null;
  return Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3])) / 86400000 + 25569;
}

function xmlDeHoja(hoja: Hoja): string {
  const anchos = hoja.anchos ?? [];
  if (!hoja.anchos) {
    for (const fila of hoja.filas) fila.forEach((c, i) => {
      const v = typeof c === 'object' && c !== null ? c.v : c;
      const largo = v == null ? 0 : typeof v === 'number' ? 14 : String(v).length + 2;
      anchos[i] = Math.min(60, Math.max(anchos[i] ?? 8, largo));
    });
  }
  const columnas = anchos.map((w, i) => `<col min="${i + 1}" max="${i + 1}" width="${w}" customWidth="1"/>`).join('');

  const filas = hoja.filas.map((fila, r) => {
    const celdas = fila.map((c, i) => {
      const ref = `${letras(i)}${r + 1}`;
      const obj: { v?: string | number | null; tipo?: 'importe' | 'entero' | 'fecha' | 'texto'; negrita?: boolean } =
        typeof c === 'object' && c !== null ? c : { v: c };
      const v = obj.v;
      if (v === null || v === undefined || v === '') return '';
      const negrita = obj.negrita ? 1 : 0;
      const tipo = obj.tipo ?? (typeof v === 'number' ? 'importe' : 'texto');
      if (tipo === 'fecha') {
        const serie = typeof v === 'number' ? v : fechaAExcel(v);
        return serie === null ? `<c r="${ref}" t="inlineStr"><is><t>${escapar(String(v))}</t></is></c>` : `<c r="${ref}" s="${negrita ? 7 : 4}"><v>${serie}</v></c>`;
      }
      if (typeof v === 'number') {
        if (!Number.isFinite(v)) return '';
        const estilo = tipo === 'entero' ? (negrita ? 6 : 5) : (negrita ? 3 : 2);
        return `<c r="${ref}" s="${estilo}"><v>${v}</v></c>`;
      }
      return `<c r="${ref}" s="${negrita}" t="inlineStr"><is><t xml:space="preserve">${escapar(String(v))}</t></is></c>`;
    }).join('');
    return `<row r="${r + 1}">${celdas}</row>`;
  }).join('');

  return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><cols>${columnas}</cols><sheetData>${filas}</sheetData></worksheet>`;
}

// --- ZIP sin compresión (método "store") ---------------------------------------------------------

const TABLA_CRC = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();

function crc32(datos: Uint8Array): number {
  let c = 0xFFFFFFFF;
  for (let i = 0; i < datos.length; i++) c = TABLA_CRC[(c ^ datos[i]) & 0xFF] ^ (c >>> 8);
  return (c ^ 0xFFFFFFFF) >>> 0;
}

function zip(archivos: { nombre: string; contenido: string }[]): Uint8Array {
  const codificador = new TextEncoder();
  const partes: Uint8Array[] = [];
  const central: Uint8Array[] = [];
  let desplazamiento = 0;

  // 1980-01-01 00:00 como fecha del archivo.
  const hora = 0, dia = (1 << 5) | 1;

  for (const a of archivos) {
    const nombre = codificador.encode(a.nombre);
    const datos = codificador.encode(a.contenido);
    const crc = crc32(datos);

    const local = new DataView(new ArrayBuffer(30));
    local.setUint32(0, 0x04034B50, true); local.setUint16(4, 20, true); local.setUint16(6, 0x0800, true); // UTF-8
    local.setUint16(8, 0, true); local.setUint16(10, hora, true); local.setUint16(12, dia, true);
    local.setUint32(14, crc, true); local.setUint32(18, datos.length, true); local.setUint32(22, datos.length, true);
    local.setUint16(26, nombre.length, true); local.setUint16(28, 0, true);
    partes.push(new Uint8Array(local.buffer), nombre, datos);

    const dir = new DataView(new ArrayBuffer(46));
    dir.setUint32(0, 0x02014B50, true); dir.setUint16(4, 20, true); dir.setUint16(6, 20, true); dir.setUint16(8, 0x0800, true);
    dir.setUint16(10, 0, true); dir.setUint16(12, hora, true); dir.setUint16(14, dia, true);
    dir.setUint32(16, crc, true); dir.setUint32(20, datos.length, true); dir.setUint32(24, datos.length, true);
    dir.setUint16(28, nombre.length, true); dir.setUint32(42, desplazamiento, true);
    central.push(new Uint8Array(dir.buffer), nombre);

    desplazamiento += 30 + nombre.length + datos.length;
  }

  const tamanoCentral = central.reduce((s, p) => s + p.length, 0);
  const fin = new DataView(new ArrayBuffer(22));
  fin.setUint32(0, 0x06054B50, true); fin.setUint16(8, archivos.length, true); fin.setUint16(10, archivos.length, true);
  fin.setUint32(12, tamanoCentral, true); fin.setUint32(16, desplazamiento, true);

  const todo = [...partes, ...central, new Uint8Array(fin.buffer)];
  const salida = new Uint8Array(todo.reduce((s, p) => s + p.length, 0));
  let pos = 0;
  for (const p of todo) { salida.set(p, pos); pos += p.length; }
  return salida;
}

/** Nombre de hoja válido para Excel: sin \ / ? * [ ] : y de hasta 31 caracteres. */
const nombreDeHoja = (n: string, i: number) => (n.replace(/[\\/?*[\]:]/g, ' ').trim().slice(0, 31) || `Hoja ${i + 1}`);

export function crearXlsx(hojas: Hoja[]): Uint8Array {
  const usados = new Set<string>();
  const nombres = hojas.map((h, i) => {
    let n = nombreDeHoja(h.nombre, i), k = 2;
    while (usados.has(n.toLowerCase())) n = `${nombreDeHoja(h.nombre, i).slice(0, 28)} ${k++}`;
    usados.add(n.toLowerCase());
    return n;
  });

  const archivos = [
    { nombre: '[Content_Types].xml', contenido: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>${hojas.map((_, i) => `<Override PartName="/xl/worksheets/sheet${i + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>`).join('')}</Types>` },
    { nombre: '_rels/.rels', contenido: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>` },
    { nombre: 'xl/workbook.xml', contenido: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>${nombres.map((n, i) => `<sheet name="${escapar(n)}" sheetId="${i + 1}" r:id="rId${i + 1}"/>`).join('')}</sheets></workbook>` },
    { nombre: 'xl/_rels/workbook.xml.rels', contenido: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">${hojas.map((_, i) => `<Relationship Id="rId${i + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet${i + 1}.xml"/>`).join('')}<Relationship Id="rId${hojas.length + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>` },
    { nombre: 'xl/styles.xml', contenido: ESTILOS },
    ...hojas.map((h, i) => ({ nombre: `xl/worksheets/sheet${i + 1}.xml`, contenido: xmlDeHoja(h) })),
  ];
  return zip(archivos);
}

/** Descarga el archivo en el navegador. */
export function descargarXlsx(hojas: Hoja[], nombreArchivo: string) {
  const bytes = crearXlsx(hojas);
  const blob = new Blob([bytes.buffer as ArrayBuffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${nombreArchivo.replace(/[\\/:*?"<>|]/g, '-')}.xlsx`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
