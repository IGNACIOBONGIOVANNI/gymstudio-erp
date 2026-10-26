import React, { useState } from 'react';
import { X, Loader2, FileText } from 'lucide-react';
import toast from 'react-hot-toast';
import { generarReportePDF } from '../../utils/generarReportePDF';

interface Props {
  onClose: () => void;
}

export function ModalGenerarReporte({ onClose }: Props) {
  const [tipo, setTipo] = useState<string>('diario');
  const [desde, setDesde] = useState<string>('');
  const [hasta, setHasta] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!tipo) {
      toast.error('Selecciona el tipo de libro');
      return;
    }

    setIsSubmitting(true);
    try {
      const apiUrl = import.meta.env.VITE_API_URL || '';

      const obtenerDatosDiario = async (filtroDesde?: string, filtroHasta?: string) => {
        const queryParams = new URLSearchParams({ tipo: 'diario' });
        if (filtroDesde) queryParams.append('desde', filtroDesde);
        if (filtroHasta) queryParams.append('hasta', filtroHasta);

        let asientosData: any[] = [];
        try {
          const res = await fetch(`${apiUrl}/api/reportes/generar?${queryParams.toString()}`);
          if (res.ok) {
            asientosData = await res.json();
          }
        } catch (e) {
          console.error(e);
        }

        // Si el endpoint de reporte no trajo líneas completas, recurrimos a /api/asientos (el servicio completo de Libro Diario)
        if (!asientosData || !asientosData.length || !asientosData[0]?.lineas || asientosData[0]?.lineas.length === 0) {
          try {
            const resAsientos = await fetch(`${apiUrl}/api/asientos`);
            if (resAsientos.ok) {
              let full = await resAsientos.json();
              if (filtroDesde) full = full.filter((a: any) => a.fecha >= filtroDesde);
              if (filtroHasta) full = full.filter((a: any) => a.fecha <= filtroHasta);
              asientosData = full;
            }
          } catch (e) {
            console.error(e);
          }
        }

        return asientosData;
      };

      // 🔥 NUEVA LÓGICA: RECOPILAR TODOS LOS DATOS Y ARMAR UN SOLO PDF
      if (tipo === 'todos') {
        const todosLosTipos = ['diario', 'mayor', 'balance', 'venta', 'compra', 'caja', 'banco'];
        toast.loading('Recopilando datos de todos los reportes...', { id: 'lote-toast' });

        const todosLosDatos = [];
        let exitoCount = 0;

        for (const t of todosLosTipos) {
          if (t === 'diario') {
            const datosDiario = await obtenerDatosDiario(desde, hasta);
            todosLosDatos.push({ tipo: t, datos: datosDiario });
            exitoCount++;
            continue;
          }

          const queryParams = new URLSearchParams({ tipo: t });
          if (desde) queryParams.append('desde', desde);
          if (hasta) queryParams.append('hasta', hasta);

          const response = await fetch(`${apiUrl}/api/reportes/generar?${queryParams.toString()}`);
          if (response.ok) {
            const datos = await response.json();
            // Guardamos el tipo de libro junto con sus datos
            todosLosDatos.push({ tipo: t, datos: datos });
            exitoCount++;
          }
        }

        toast.dismiss('lote-toast');

        if (exitoCount > 0) {
          // Mandamos el mega-array al generador de PDF
          generarReportePDF(todosLosDatos, 'todos', desde, hasta);
          toast.success('Reporte consolidado generado exitosamente');
          onClose();
        } else {
          toast.error('Error al generar los reportes en lote');
        }

      } else {
        // Lógica individual original
        if (tipo === 'diario') {
          const datos = await obtenerDatosDiario(desde, hasta);
          generarReportePDF(datos, tipo, desde, hasta);
          toast.success('Reporte generado exitosamente');
          onClose();
        } else {
          const queryParams = new URLSearchParams({ tipo });
          if (desde) queryParams.append('desde', desde);
          if (hasta) queryParams.append('hasta', hasta);

          const response = await fetch(`${apiUrl}/api/reportes/generar?${queryParams.toString()}`);

          if (response.ok) {
            const datos = await response.json();
            generarReportePDF(datos, tipo, desde, hasta);
            toast.success('Reporte generado exitosamente');
            onClose();
          } else {
            const errorData = await response.json().catch(() => ({}));
            toast.error(errorData.detail || errorData.Mensaje || 'Error al generar el reporte');
          }
        }
      }
    } catch (error) {
      toast.dismiss('lote-toast');
      toast.error('Ocurrió un problema de red');
      console.error(error);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
      <div className="bg-white rounded-xl shadow-2xl w-[95%] max-w-md mx-auto overflow-hidden animate-in fade-in zoom-in-95 duration-200">

        {/* Header */}
        <div className="bg-slate-900 px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-indigo-500/20 rounded-lg text-indigo-400">
              <FileText className="w-5 h-5" />
            </div>
            <h3 className="text-lg font-bold text-white">Generar Reporte (PDF)</h3>
          </div>
          <button
            onClick={onClose}
            className="text-slate-400 hover:text-white transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-6">
          <form onSubmit={handleSubmit} className="space-y-5">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">
                Tipo de Libro
              </label>
              <select
                value={tipo}
                onChange={(e) => setTipo(e.target.value)}
                className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:border-slate-900 transition-shadow"
                required
              >
                {/* 🔥 NUEVA OPCIÓN AGREGADA */}
                <option value="todos" className="font-bold text-indigo-600">* TODOS LOS REPORTES</option>
                <option value="diario">Libro Diario</option>
                <option value="mayor">Libro Mayor</option>
                <option value="balance">Balance General</option>
                <option value="venta">Libro de Ventas</option>
                <option value="compra">Libro de Compras</option>
                <option value="caja">Libro Caja</option>
                <option value="banco">Libro Banco</option>
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">
                Fecha Desde (Opcional)
              </label>
              <input
                type="date"
                value={desde}
                onChange={(e) => setDesde(e.target.value)}
                className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:border-slate-900 transition-shadow"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">
                Fecha Hasta (Opcional)
              </label>
              <input
                type="date"
                value={hasta}
                onChange={(e) => setHasta(e.target.value)}
                className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:border-slate-900 transition-shadow"
              />
            </div>

            <div className="flex justify-end gap-3 pt-4 border-t border-slate-100 mt-6">
              <button
                type="button"
                onClick={onClose}
                disabled={isSubmitting}
                className="px-4 py-2 text-sm font-medium text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 transition-colors disabled:opacity-50"
              >
                Cancelar
              </button>
              <button
                type="submit"
                disabled={isSubmitting}
                className="inline-flex items-center px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 transition-colors disabled:opacity-70"
              >
                {isSubmitting && <Loader2 className="w-4 h-4 mr-2 animate-spin" />}
                Generar
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}