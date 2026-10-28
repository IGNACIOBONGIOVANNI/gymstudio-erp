import { useState } from "react";
import { FileSpreadsheet, Loader2, Printer } from "lucide-react";
import toast from "react-hot-toast";
import { generarReportePDF } from "../../utils/generarReportePDF";
import { generarReporteExcel } from "../../utils/generarReporteExcel";
import { mensajeDeError } from "../../api/errores";

interface Props {
  /** Libro a imprimir: diario, mayor, balance, venta, compra, caja, banco o stock. */
  tipo: string;
  /** Fecha (aaaa-mm-dd) para imprimir sólo ese día; vacío imprime todo. */
  fecha?: string;
  /** Período (aaaa-mm-dd); se usa en lugar de "fecha" cuando se indica. */
  desde?: string;
  hasta?: string;
}

const estilo = "inline-flex items-center justify-center gap-2 rounded-lg border border-slate-300 bg-white px-4 py-2 min-h-[40px] text-sm font-semibold text-slate-700 shadow-sm transition-all hover:bg-slate-50 active:scale-95 disabled:opacity-60 shrink-0";

/**
 * Saca el libro que se está viendo en PDF (para imprimir) o en Excel (para trabajarlo),
 * con los mismos datos y el mismo período que Reportes.
 */
export function BotonPDF({ tipo, fecha, desde = fecha, hasta = fecha }: Props) {
  const [generando, setGenerando] = useState<"pdf" | "excel" | null>(null);

  const generar = async (formato: "pdf" | "excel") => {
    setGenerando(formato);
    try {
      const query = new URLSearchParams({ tipo });
      if (desde) query.append("desde", desde);
      if (hasta) query.append("hasta", hasta);
      const res = await fetch(`/api/reportes/generar?${query.toString()}`);
      if (!res.ok) throw new Error(await mensajeDeError(res, "No se pudo generar el archivo"));
      const datos = await res.json();
      if (formato === "pdf") generarReportePDF(datos, tipo, desde || undefined, hasta || undefined);
      else generarReporteExcel(datos, tipo, desde || undefined, hasta || undefined);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "No se pudo generar el archivo");
    } finally {
      setGenerando(null);
    }
  };

  return (
    <>
      <button type="button" onClick={() => generar("pdf")} disabled={generando !== null} title="Generar el PDF de este libro para imprimir" className={estilo}>
        {generando === "pdf" ? <Loader2 className="h-4 w-4 animate-spin" /> : <Printer className="h-4 w-4" />}
        Imprimir PDF
      </button>
      <button type="button" onClick={() => generar("excel")} disabled={generando !== null} title="Descargar este libro como planilla de Excel" className={estilo}>
        {generando === "excel" ? <Loader2 className="h-4 w-4 animate-spin" /> : <FileSpreadsheet className="h-4 w-4" />}
        Exportar Excel
      </button>
    </>
  );
}
