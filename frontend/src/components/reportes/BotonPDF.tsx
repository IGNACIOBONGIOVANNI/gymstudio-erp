import { useState } from "react";
import { Loader2, Printer } from "lucide-react";
import toast from "react-hot-toast";
import { generarReportePDF } from "../../utils/generarReportePDF";
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

/** Imprime en PDF el libro que se está viendo, con el mismo formato que Reportes en PDF. */
export function BotonPDF({ tipo, fecha, desde = fecha, hasta = fecha }: Props) {
  const [generando, setGenerando] = useState(false);

  const imprimir = async () => {
    setGenerando(true);
    try {
      const query = new URLSearchParams({ tipo });
      if (desde) query.append("desde", desde);
      if (hasta) query.append("hasta", hasta);
      const res = await fetch(`/api/reportes/generar?${query.toString()}`);
      if (!res.ok) throw new Error(await mensajeDeError(res, "No se pudo generar el PDF"));
      generarReportePDF(await res.json(), tipo, desde || undefined, hasta || undefined);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "No se pudo generar el PDF");
    } finally {
      setGenerando(false);
    }
  };

  return (
    <button
      type="button"
      onClick={imprimir}
      disabled={generando}
      title="Generar el PDF de este libro para imprimir"
      className="inline-flex items-center justify-center gap-2 rounded-lg border border-slate-300 bg-white px-4 py-2 min-h-[40px] text-sm font-semibold text-slate-700 shadow-sm transition-all hover:bg-slate-50 active:scale-95 disabled:opacity-60 shrink-0"
    >
      {generando ? <Loader2 className="h-4 w-4 animate-spin" /> : <Printer className="h-4 w-4" />}
      Imprimir PDF
    </button>
  );
}
