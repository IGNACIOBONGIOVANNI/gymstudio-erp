import React, { useState } from 'react';
import { X, Loader2, FileText } from 'lucide-react';
import toast from 'react-hot-toast';
import { generarReportePDF } from '../../utils/generarReportePDF';
import { mensajeDeError } from '../../api/errores';

const TODOS_LOS_LIBROS = ['diario', 'mayor', 'balance', 'venta', 'compra', 'caja', 'banco', 'stock'];

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
    if (desde && hasta && desde > hasta) {
      toast.error('La fecha desde no puede ser posterior a la fecha hasta');
      return;
    }

    const obtener = async (t: string) => {
      const query = new URLSearchParams({ tipo: t });
      if (desde) query.append('desde', desde);
      if (hasta) query.append('hasta', hasta);
      const res = await fetch(`/api/reportes/generar?${query.toString()}`);
      if (!res.ok) throw new Error(await mensajeDeError(res, `No se pudo obtener el ${t}`));
      return res.json();
    };

    setIsSubmitting(true);
    try {
      if (tipo === 'todos') {
        const todos = [];
        for (const t of TODOS_LOS_LIBROS) todos.push({ tipo: t, datos: await obtener(t) });
        generarReportePDF(todos, 'todos', desde, hasta);
      } else {
        generarReportePDF(await obtener(tipo), tipo, desde, hasta);
      }
      toast.success('Reporte generado');
      onClose();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'No se pudo generar el reporte');
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
            <div className="p-2 bg-white/10 rounded-lg text-white">
              <FileText className="w-5 h-5" />
            </div>
            <h3 className="text-lg font-bold text-white">Generar Reporte (PDF)</h3>
          </div>
          <button
            onClick={onClose}
            aria-label="Cerrar"
            className="text-slate-400 hover:text-white transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-6">
          <form onSubmit={handleSubmit} className="space-y-5">
            <div>
              <label htmlFor="reporteTipo" className="block text-sm font-medium text-slate-700 mb-1">
                Tipo de Libro
              </label>
              <select
                id="reporteTipo"
                value={tipo}
                onChange={(e) => setTipo(e.target.value)}
                className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:border-slate-900 transition-shadow"
                required
              >
                <option value="todos">Todos los libros (un solo PDF)</option>
                <option value="diario">Libro Diario</option>
                <option value="mayor">Libro Mayor</option>
                <option value="balance">Balance de Sumas y Saldos</option>
                <option value="venta">Libro IVA Ventas</option>
                <option value="compra">Libro IVA Compras</option>
                <option value="caja">Libro Caja</option>
                <option value="banco">Libro Banco</option>
                <option value="stock">Libro Stock</option>
              </select>
            </div>

            <div>
              <label htmlFor="reporteDesde" className="block text-sm font-medium text-slate-700 mb-1">
                Fecha Desde (Opcional)
              </label>
              <input
                id="reporteDesde"
                type="date"
                value={desde}
                onChange={(e) => setDesde(e.target.value)}
                className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:border-slate-900 transition-shadow"
              />
            </div>

            <div>
              <label htmlFor="reporteHasta" className="block text-sm font-medium text-slate-700 mb-1">
                Fecha Hasta (Opcional)
              </label>
              <input
                id="reporteHasta"
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
                className="inline-flex items-center px-4 py-2 text-sm font-medium text-white bg-marca-600 rounded-lg hover:bg-marca-700 transition-colors disabled:opacity-70"
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