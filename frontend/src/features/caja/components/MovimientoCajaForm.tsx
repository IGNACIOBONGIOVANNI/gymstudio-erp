import { useState, useEffect } from "react";
import toast from "react-hot-toast";
import { Loader2, Plus, X, Pencil } from "lucide-react";
import { useCrearMovimientoCaja } from "../hooks/useCrearMovimientoCaja";
// 🔥 Asegurate de tener este hook creado para actualizar (te dejo el código más abajo por las dudas)
import { useEditarMovimientoCaja } from "../hooks/useEditarMovimientoCaja";

interface MovimientoCajaFormProps {
  clienteId: string;
  onClose: () => void;
  movimientoAEditar?: any; // Agregamos la prop para recibir el movimiento
}

export function MovimientoCajaForm({ clienteId, onClose, movimientoAEditar }: MovimientoCajaFormProps) {
  const [fecha, setFecha] = useState("");
  const [detalle, setDetalle] = useState("");
  const [tipoMovimiento, setTipoMovimiento] = useState(1);
  const [importe, setImporte] = useState("");
  // Cuenta contra la que se registra el asiento ("" = la de siempre: Otros Ingresos /
  // Gastos Generales al crear, o la que ya tenía al editar).
  const [contrapartida, setContrapartida] = useState("");

  const { mutate: crear, isPending: isCreating } = useCrearMovimientoCaja();
  const { mutate: editar, isPending: isEditing } = useEditarMovimientoCaja();

  const isPending = isCreating || isEditing;

  // Si nos pasan un movimiento, rellenamos los campos automáticamente
  useEffect(() => {
    if (movimientoAEditar) {
      // Formateamos la fecha a YYYY-MM-DD para el input type="date"
      const fechaFormateada = movimientoAEditar.fecha
        ? new Date(movimientoAEditar.fecha).toISOString().split('T')[0]
        : "";
      setFecha(fechaFormateada);
      setDetalle(movimientoAEditar.detalle || "");

      // Si tiene ingreso, es tipo 1. Si tiene egreso, es tipo 2.
      if (movimientoAEditar.ingreso != null && movimientoAEditar.ingreso > 0) {
        setTipoMovimiento(1);
        setImporte(String(movimientoAEditar.ingreso));
      } else if (movimientoAEditar.egreso != null && movimientoAEditar.egreso > 0) {
        setTipoMovimiento(2);
        setImporte(String(movimientoAEditar.egreso));
      }
    }
  }, [movimientoAEditar]);

  const handleLimpiar = () => {
    setFecha("");
    setDetalle("");
    setTipoMovimiento(1);
    setImporte("");
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    const monto = Number(importe);
    if (!fecha) return void toast.error("Indicá la fecha del movimiento");
    if (!detalle.trim()) return void toast.error("Indicá el detalle del movimiento");
    if (!Number.isFinite(monto) || monto <= 0) return void toast.error("El importe debe ser un número mayor a cero");

    const fechaIso = new Date(fecha).toISOString();
    const cuentaContrapartidaId = contrapartida ? Number(contrapartida) : null;

    if (movimientoAEditar) {
      // El endpoint de edición recibe el importe como ingreso o como egreso.
      editar(
        {
          id: movimientoAEditar.id,
          data: {
            id: movimientoAEditar.id,
            fecha: fechaIso,
            detalle,
            ingreso: tipoMovimiento === 1 ? monto : null,
            egreso: tipoMovimiento === 2 ? monto : null,
            cuentaContrapartidaId,
          },
        },
        { onSuccess: () => onClose() }
      );
    } else {
      crear(
        { clienteId, fecha: fechaIso, detalle, tipoMovimiento, importe: monto, cuentaContrapartidaId },
        { onSuccess: () => onClose() }
      );
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4 sm:p-0">
      <div className="w-full max-w-md rounded-xl bg-white shadow-xl ring-1 ring-slate-900/5">
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-slate-900">
            {movimientoAEditar ? "Editar Movimiento" : "Nuevo Movimiento"}
          </h2>
          <button
            onClick={onClose}
            className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-500 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700">
              Fecha
            </label>
            <input
              type="date"
              required
              value={fecha}
              onChange={(e) => setFecha(e.target.value)}
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700">
              Tipo de Movimiento
            </label>
            <select
              value={tipoMovimiento}
              onChange={(e) => {
                setTipoMovimiento(Number(e.target.value));
                setContrapartida("");
              }}
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            >
              <option value={1}>Ingreso</option>
              <option value={2}>Egreso</option>
            </select>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700">
              Cuenta contable (contrapartida)
            </label>
            <select
              value={contrapartida || (movimientoAEditar ? "" : tipoMovimiento === 1 ? "106" : "107")}
              onChange={(e) => setContrapartida(e.target.value)}
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            >
              {tipoMovimiento === 1 ? (
                <>
                  {movimientoAEditar && <option value="">Mantener la actual</option>}
                  <option value="106">Otros Ingresos</option>
                  <option value="103">Cobro a clientes (Deudores por Ventas)</option>
                </>
              ) : (
                <>
                  {movimientoAEditar && <option value="">Mantener la actual</option>}
                  <option value="107">Gastos Generales</option>
                  <option value="202">Pago a proveedores</option>
                </>
              )}
            </select>
            <p className="mt-1 text-xs text-slate-500">
              El movimiento genera su asiento en el Libro Diario contra esta cuenta.
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700">
              Detalle
            </label>
            <input
              type="text"
              required
              placeholder="Ej: Factura N° 0001"
              value={detalle}
              onChange={(e) => setDetalle(e.target.value)}
              className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700">
              Importe
            </label>
            <div className="relative mt-1">
              <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
                <span className="text-slate-500 sm:text-sm">$</span>
              </div>
              <input
                type="number"
                required
                min="0.01"
                step="0.01"
                value={importe}
                onChange={(e) => setImporte(e.target.value)}
                className="block w-full rounded-md border border-slate-300 pl-7 pr-12 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                placeholder="0.00"
              />
            </div>
          </div>

          <div className="mt-6 flex justify-end space-x-3">
            {!movimientoAEditar && (
              <button
                type="button"
                onClick={handleLimpiar}
                className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
              >
                Limpiar
              </button>
            )}
            <button
              type="button"
              onClick={onClose}
              className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="inline-flex items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {movimientoAEditar ? (
                <>
                  <Pencil className="mr-2 h-4 w-4" /> Actualizar Movimiento
                </>
              ) : (
                <>
                  <Plus className="mr-2 h-4 w-4" /> Guardar Movimiento
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}