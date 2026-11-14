// src/features/asientos/components/LineaAsientoRow.tsx
import { Trash2 } from "lucide-react";
import type { Control, FieldErrors, UseFormRegister } from "react-hook-form";
import type { AsientoFormValues, CuentaContable } from "./types";
import { numeroDecimal } from '../utils/entradaNumerica';

interface LineaAsientoRowProps {
  index: number;
  register: UseFormRegister<AsientoFormValues>;
  control: Control<AsientoFormValues>;
  errors: FieldErrors<AsientoFormValues>;
  cuentas: CuentaContable[];
  disabled: boolean;
  onEliminar: () => void;
}

const inputClass =
  "w-full rounded-md border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900 " +
  "focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 " +
  "disabled:bg-slate-50 disabled:text-slate-400";

/** El campo solo acepta un importe: se limpia lo escrito antes de que lo lea React Hook Form. */
function conSoloImportes<T extends { onChange: (evento: { target: unknown; type?: unknown }) => unknown }>(registrado: T): T {
  return {
    ...registrado,
    onChange: (evento: React.ChangeEvent<HTMLInputElement>) => {
      evento.target.value = numeroDecimal(evento.target.value);
      return registrado.onChange(evento);
    },
  };
}

export function LineaAsientoRow({
  index,
  register,
  errors,
  cuentas,
  disabled,
  onEliminar,
}: LineaAsientoRowProps) {
  const lineaErrors = errors.lineas?.[index];

  return (
    <tr className="border-b border-slate-100 align-top last:border-b-0">
      <td className="py-2 pr-2">
        <select
          {...register(`lineas.${index}.cuentaId` as const)}
          disabled={disabled}
          className={inputClass}
        >
          <option value="">Seleccioná una cuenta…</option>
          {cuentas.map((cuenta) => (
            <option key={cuenta.id} value={cuenta.id}>
              {cuenta.codigo} — {cuenta.nombre}
            </option>
          ))}
        </select>
        {lineaErrors?.cuentaId && (
          <p className="mt-1 text-xs text-red-600">{lineaErrors.cuentaId.message}</p>
        )}
      </td>

      <td className="py-2 pr-2">
        <input
          type="text"
          placeholder="Opcional"
          {...register(`lineas.${index}.descripcion` as const)}
          disabled={disabled}
          className={inputClass}
        />
      </td>

      <td className="py-2 pr-2">
        <input
          type="text"
          inputMode="decimal"
          placeholder="0.00"
          {...conSoloImportes(register(`lineas.${index}.debe` as const))}
          disabled={disabled}
          className={`${inputClass} w-28 text-right font-mono tabular-nums`}
        />
        {lineaErrors?.debe && (
          <p className="mt-1 text-xs text-red-600">{lineaErrors.debe.message}</p>
        )}
      </td>

      <td className="py-2 pr-2">
        <input
          type="text"
          inputMode="decimal"
          placeholder="0.00"
          {...conSoloImportes(register(`lineas.${index}.haber` as const))}
          disabled={disabled}
          className={`${inputClass} w-28 text-right font-mono tabular-nums`}
        />
        {lineaErrors?.haber && (
          <p className="mt-1 text-xs text-red-600">{lineaErrors.haber.message}</p>
        )}
      </td>

      <td className="py-2 pl-1 text-right">
        <button
          type="button"
          onClick={onEliminar}
          disabled={disabled}
          aria-label="Eliminar línea"
          className="rounded-md p-1.5 text-slate-400 transition hover:bg-red-50 hover:text-red-600 disabled:opacity-40"
        >
          <Trash2 className="h-4 w-4" />
        </button>
      </td>
    </tr>
  );
}
