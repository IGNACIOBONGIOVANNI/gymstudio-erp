// src/features/asientos/components/AsientoForm.tsx
import { useState } from "react";
import toast from "react-hot-toast";
import { useForm, useFieldArray, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Plus, Loader2 } from "lucide-react";
import { asientoFormSchema, type AsientoFormValues } from "./schema";
import type { CuentaContable } from "./types";
import { useBalanceAsiento } from "./useBalanceAsiento";
import { BalanceIndicator } from "./BalanceIndicator";
import { LineaAsientoRow } from "./LineaAsientoRow";
import { crearAsiento, confirmarAsiento, ApiError } from "./asientosApi";

interface AsientoFormProps {
  clienteId: string;
  cuentas: CuentaContable[];
  /** Se dispara cuando el asiento fue creado Y confirmado exitosamente en el backend. */
  onConfirmado?: (asientoId: string) => void;
}

const LINEA_VACIA = { cuentaId: "", descripcion: "", debe: "", haber: "" };

export function AsientoForm({ clienteId, cuentas, onConfirmado }: AsientoFormProps) {
  const [enviando, setEnviando] = useState(false);
  const [errorServidor, setErrorServidor] = useState<string | null>(null);

  const {
    register,
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<AsientoFormValues>({
    resolver: zodResolver(asientoFormSchema),
    mode: "onBlur",
    defaultValues: {
      fecha: new Date().toISOString().slice(0, 10),
      descripcion: "",
      lineas: [LINEA_VACIA, LINEA_VACIA],
    },
  });

  const { fields, append, remove } = useFieldArray({ control, name: "lineas" });

  // useWatch (no `watch()`) evita re-renderizar todo el form en cada tecla:
  // sólo los consumidores de `lineasObservadas` (indicador + botón) se actualizan.
  const lineasObservadas = useWatch({ control, name: "lineas" });
  const balance = useBalanceAsiento(lineasObservadas ?? []);

  const puedeConfirmar = balance.estaBalanceado && !enviando;

  const onSubmit = handleSubmit(async (data) => {
    setErrorServidor(null);
    setEnviando(true);
    try {
      const { asientoId } = await crearAsiento({
        clienteId: Number(clienteId),
        fecha: data.fecha,
        descripcion: data.descripcion,
        lineas: data.lineas.map(l => ({
          cuentaId: Number(l.cuentaId),
          descripcion: l.descripcion,
          debe: Number(l.debe) || 0,
          haber: Number(l.haber) || 0
        })),
      });
      // 2) Dispara la confirmación (ConfirmarAsientoCommand en el backend).
      //    Si por alguna razón llegara desbalanceado, el backend lo rechaza
      //    con 422 aunque el botón ya lo haya prevenido en el cliente.
      await confirmarAsiento(asientoId.toString());
      
      toast.success("¡Asiento guardado y balanceado con éxito!");
      
      reset({
        fecha: new Date().toISOString().slice(0, 10),
        descripcion: "",
        lineas: [LINEA_VACIA, LINEA_VACIA],
      });

      onConfirmado?.(asientoId.toString());
    } catch (err) {
      setErrorServidor(
        err instanceof ApiError
          ? err.problem.detail ?? err.problem.title
          : "No se pudo confirmar el asiento. Intentá nuevamente."
      );
    } finally {
      setEnviando(false);
    }
  });

  const handleLimpiar = () => {
    reset({
      fecha: new Date().toISOString().slice(0, 10),
      descripcion: "",
      lineas: [LINEA_VACIA, LINEA_VACIA],
    });
  };

  return (
    <form onSubmit={onSubmit} className="mx-auto flex max-w-4xl flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h2 className="text-lg font-semibold text-slate-900">Nuevo asiento contable</h2>
        <p className="text-sm text-slate-500">
          Cargá las líneas del Debe y del Haber. El asiento debe quedar balanceado antes de confirmarse.
        </p>
        <p className="text-xs text-slate-400">
          Caja y Banco no aparecen acá: sus movimientos se cargan en el Libro Caja y el Libro Banco
          (incluidas las transferencias entre ellos), que generan el asiento automáticamente.
        </p>
      </header>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div>
          <label htmlFor="fecha" className="mb-1 block text-sm font-medium text-slate-700">
            Fecha
          </label>
          <input
            id="fecha"
            type="date"
            disabled={enviando}
            {...register("fecha")}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:bg-slate-50"
          />
          {errors.fecha && <p className="mt-1 text-xs text-red-600">{errors.fecha.message}</p>}
        </div>

        <div>
          <label htmlFor="descripcion" className="mb-1 block text-sm font-medium text-slate-700">
            Descripción
          </label>
          <input
            id="descripcion"
            type="text"
            placeholder="Ej: Pago de alquiler julio 2026"
            disabled={enviando}
            {...register("descripcion")}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:bg-slate-50"
          />
          {errors.descripcion && (
            <p className="mt-1 text-xs text-red-600">{errors.descripcion.message}</p>
          )}
        </div>
      </div>

      <div className="overflow-x-auto rounded-lg border border-slate-200">
        <table className="w-full min-w-[640px] border-collapse">
          <thead>
            <tr className="border-b border-slate-200 bg-slate-50 text-left text-xs font-medium uppercase tracking-wide text-slate-500">
              <th className="px-2 py-2">Cuenta</th>
              <th className="px-2 py-2">Descripción</th>
              <th className="px-2 py-2 text-right">Debe</th>
              <th className="px-2 py-2 text-right">Haber</th>
              <th className="w-10 px-2 py-2" />
            </tr>
          </thead>
          <tbody>
            {fields.map((field, index) => (
              <LineaAsientoRow
                key={field.id}
                index={index}
                register={register}
                control={control}
                errors={errors}
                cuentas={cuentas}
                disabled={enviando}
                onEliminar={() => remove(index)}
              />
            ))}
          </tbody>
        </table>

        <div className="border-t border-slate-100 px-2 py-2">
          <button
            type="button"
            onClick={() => append(LINEA_VACIA)}
            disabled={enviando}
            className="inline-flex items-center gap-1.5 rounded-md px-2 py-1.5 text-sm font-medium text-slate-600 transition hover:bg-slate-50 disabled:opacity-40"
          >
            <Plus className="h-4 w-4" />
            Agregar línea
          </button>
        </div>
      </div>

      {/* Error de zod a nivel de array (ej. "se requieren al menos 2 líneas") */}
      {errors.lineas?.message && (
        <p className="text-sm text-red-600">{errors.lineas.message}</p>
      )}

      <BalanceIndicator balance={balance} />

      {errorServidor && (
        <div
          role="alert"
          className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
        >
          {errorServidor}
        </div>
      )}

      <div className="flex justify-end gap-3">
        <button
          type="button"
          onClick={handleLimpiar}
          disabled={enviando}
          className="inline-flex items-center gap-2 rounded-md border border-slate-300 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50"
        >
          Limpiar
        </button>
        <button
          type="submit"
          disabled={!puedeConfirmar}
          className="inline-flex items-center gap-2 rounded-md bg-slate-900 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-300"
        >
          {enviando && <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />}
          Confirmar asiento
        </button>
      </div>
    </form>
  );
}
