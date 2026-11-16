import { useState } from 'react';
import { FileSpreadsheet, FileText, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { obtenerAnalisis, obtenerIndicadores } from '../../api/tableroApi';
import { nombreDelMes } from '../../api/dashboardApi';
import { generarTableroPDF } from '../../utils/generarTableroPDF';
import { descargarXlsx } from '../../utils/xlsx';
import { hojasDelTablero } from './tableroUtil';

const boton = 'inline-flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50';

/** «aaaa-mm» corrido n meses. */
function correrMes(mes: string, n: number): string {
  const [a, m] = mes.split('-').map(Number);
  const d = new Date(a, m - 1 + n, 1);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
}

/** El tablero de comando en PDF y todos los datos del tablero en Excel (que también se abre en Power BI). */
export function PestanaExportar({ mes, onMes, hoy }: { mes: string; onMes: (mes: string) => void; hoy: string }) {
  const [haciendo, setHaciendo] = useState<'pdf' | 'excel' | null>(null);

  const pdf = async () => {
    setHaciendo('pdf');
    try {
      generarTableroPDF(await obtenerIndicadores(mes, hoy));
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'No se pudo armar el PDF.');
    } finally {
      setHaciendo(null);
    }
  };

  const excel = async () => {
    setHaciendo('excel');
    try {
      const [ind, an] = await Promise.all([obtenerIndicadores(mes, hoy), obtenerAnalisis(correrMes(mes, -11), mes, null)]);
      descargarXlsx(hojasDelTablero(ind, an), `Tablero_de_Gestion_${mes}`);
      toast.success('Excel descargado.');
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'No se pudo armar el Excel.');
    } finally {
      setHaciendo(null);
    }
  };

  return (
    <div className="space-y-4 rounded-xl border border-slate-200 bg-white p-4">
      <div>
        <label htmlFor="exportar-mes" className="mb-1 block text-xs font-semibold text-slate-600">Mes</label>
        <input id="exportar-mes" type="month" value={mes} max={hoy.slice(0, 7)} onChange={(e) => e.target.value && onMes(e.target.value)}
          className="rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-marca-500 focus:outline-none focus:ring-2 focus:ring-marca-500" />
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <button type="button" onClick={() => void pdf()} disabled={haciendo !== null} className={`${boton} bg-marca-600 text-white hover:bg-marca-700`}>
            {haciendo === 'pdf' ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <FileText className="h-4 w-4" aria-hidden="true" />}
            Descargar PDF
          </button>
          <p className="text-xs text-slate-500">El tablero de comando de {nombreDelMes(mes)}, en blanco y negro, para imprimir o presentar.</p>
        </div>
        <div className="space-y-2">
          <button type="button" onClick={() => void excel()} disabled={haciendo !== null} className={`${boton} border border-slate-300 bg-white text-slate-700 hover:bg-slate-50`}>
            {haciendo === 'excel' ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <FileSpreadsheet className="h-4 w-4" aria-hidden="true" />}
            Descargar Excel
          </button>
          <p className="text-xs text-slate-500">
            Seis hojas: indicadores, evolución de los últimos 12 meses, ingresos por plan, asistencia, productos y compras por proveedor.
            El archivo también se puede abrir en Power BI (Obtener datos → Libro de Excel) para armar otros informes.
          </p>
        </div>
      </div>
    </div>
  );
}
