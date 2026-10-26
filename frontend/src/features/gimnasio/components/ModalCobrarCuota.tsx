import { useState, useEffect } from 'react';
import { X, Loader2, CreditCard } from 'lucide-react';
import toast from 'react-hot-toast';
import { generarComprobante } from "../../../utils/generarComprobante";
import { mensajeDeError, ERROR_DE_CONEXION } from '../../../api/errores';

interface PlanDto {
  id: string;
  nombre: string;
  precioMensual: number;
  duracionMeses: number;
}

interface SocioSeleccionado {
  id: string;
  nombre: string;
  apellido: string;
  dni: string;
}

interface Props {
  socio: SocioSeleccionado;
  onClose: () => void;
  onSuccess: () => void;
}

export function ModalCobrarCuota({ socio, onClose, onSuccess }: Props) {
  const [planes, setPlanes] = useState<PlanDto[]>([]);
  const [planSeleccionado, setPlanSeleccionado] = useState<string>('');
  const [metodoPago, setMetodoPago] = useState<string>('1');
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    const fetchPlanes = async () => {
      try {
        const apiUrl = import.meta.env.VITE_API_URL || '';
        const response = await fetch(`${apiUrl}/api/planes`);
        if (response.ok) {
          const data = await response.json();
          setPlanes(data);
          if (data.length > 0) {
            setPlanSeleccionado(String(data[0].id));
          }
        } else {
          toast.error("Error al cargar los planes");
        }
      } catch (error) {
        toast.error("Error de conexión al cargar los planes");
      } finally {
        setIsLoading(false);
      }
    };

    fetchPlanes();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!planSeleccionado) {
      toast.error("Seleccioná un plan válido");
      return;
    }

    setIsSubmitting(true);
    try {
      const apiUrl = import.meta.env.VITE_API_URL || '';

      const planActual = planes.find(p => String(p.id) === planSeleccionado);

      // 🔥 PAYLOAD ACTUALIZADO: Mandamos el metodoPago como texto ("caja" o "banco") para C#
      const payload = {
        socioId: Number(socio.id),
        planId: Number(planSeleccionado),
        condicionPago: Number(metodoPago),
        metodoPago: metodoPago === "1" ? "caja" : "banco"
      };

      const response = await fetch(`${apiUrl}/api/Membresias/asignar-y-cobrar`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (response.ok) {
        toast.success(`Pago registrado para ${socio.nombre} ${socio.apellido}`);

        // Imprimir comprobante
        if (planActual) {
          generarComprobante({
            socio: socio,
            plan: {
              nombre: planActual.nombre,
              precioMensual: planActual.precioMensual
            }
          });
        }

        onSuccess();
      } else {
        toast.error(await mensajeDeError(response, "Error al procesar el pago"));
      }
    } catch (error) {
      toast.error(ERROR_DE_CONEXION);
    } finally {
      setIsSubmitting(false);
    }
  };

  const planActual = planes.find(p => String(p.id) === planSeleccionado);
  const formatter = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-md overflow-hidden animate-in fade-in zoom-in-95 duration-200">

        {/* Header */}
        <div className="bg-slate-900 px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-emerald-500/20 rounded-lg text-emerald-400">
              <CreditCard className="w-5 h-5" />
            </div>
            <h3 className="text-lg font-bold text-white">Cobrar Membresía</h3>
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
          <div className="mb-6 p-4 bg-slate-50 border border-slate-100 rounded-lg">
            <p className="text-sm text-slate-500">Socio</p>
            <p className="font-semibold text-slate-900 text-lg">
              {socio.apellido}, {socio.nombre}
            </p>
            <p className="text-sm text-slate-500 mt-1">DNI: {socio.dni}</p>
          </div>

          {isLoading ? (
            <div className="flex justify-center py-8">
              <Loader2 className="w-8 h-8 animate-spin text-slate-400" />
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-2">
                  Seleccionar Plan
                </label>
                <select
                  value={planSeleccionado}
                  onChange={(e) => setPlanSeleccionado(e.target.value)}
                  className="w-full border border-slate-300 rounded-lg px-4 py-3 bg-white focus:outline-none focus:ring-2 focus:ring-slate-900 focus:border-slate-900 transition-shadow"
                  required
                >
                  <option value="" disabled>-- Elegir Plan --</option>
                  {planes.map(plan => (
                    <option key={plan.id} value={plan.id}>
                      {plan.nombre} - {formatter.format(plan.precioMensual)}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-slate-700 mb-2">
                  Método de Pago
                </label>
                <select
                  value={metodoPago}
                  onChange={(e) => setMetodoPago(e.target.value)}
                  className="w-full border border-slate-300 rounded-lg px-4 py-3 bg-white focus:outline-none focus:ring-2 focus:ring-slate-900 focus:border-slate-900 transition-shadow"
                  required
                >
                  <option value="1">Efectivo (Caja)</option>
                  <option value="2">Transferencia / Depósito (Banco)</option>
                </select>
              </div>

              {planActual && (
                <div className="bg-emerald-50 border border-emerald-100 rounded-lg p-4 flex justify-between items-center mt-2">
                  <span className="text-sm font-medium text-emerald-800">Total a cobrar:</span>
                  <span className="text-xl font-bold text-emerald-700">
                    {formatter.format(planActual.precioMensual)}
                  </span>
                </div>
              )}

              <div className="flex justify-end gap-3 pt-4 border-t border-slate-100 mt-2">
                <button
                  type="button"
                  onClick={onClose}
                  disabled={isSubmitting}
                  className="px-5 py-2.5 text-sm font-medium text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 transition-colors disabled:opacity-50"
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting || !planSeleccionado}
                  className="inline-flex items-center px-5 py-2.5 text-sm font-medium text-white bg-marca-600 rounded-lg hover:bg-marca-700 transition-colors disabled:opacity-70"
                >
                  {isSubmitting && <Loader2 className="w-4 h-4 mr-2 animate-spin" />}
                  Confirmar Cobro
                </button>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}