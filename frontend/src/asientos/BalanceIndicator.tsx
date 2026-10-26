// src/features/asientos/components/BalanceIndicator.tsx
import { CheckCircle2, AlertTriangle } from "lucide-react";
import { formatMonto } from "./decimal";
import type { BalanceAsiento } from "./useBalanceAsiento";

interface BalanceIndicatorProps {
  balance: BalanceAsiento;
}

export function BalanceIndicator({ balance }: BalanceIndicatorProps) {
  const { totalDebe, totalHaber, diferencia, estaBalanceado } = balance;

  return (
    <div
      role="status"
      aria-live="polite"
      className={`flex flex-col gap-3 rounded-lg border px-4 py-3 transition-colors sm:flex-row sm:items-center sm:justify-between ${
        estaBalanceado
          ? "border-emerald-200 bg-emerald-50"
          : "border-amber-200 bg-amber-50"
      }`}
    >
      <div className="flex gap-6">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-slate-500">Debe</p>
          <p className="font-mono text-lg tabular-nums text-slate-900">{formatMonto(totalDebe)}</p>
        </div>
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-slate-500">Haber</p>
          <p className="font-mono text-lg tabular-nums text-slate-900">{formatMonto(totalHaber)}</p>
        </div>
      </div>

      <div className="flex items-center gap-2">
        {estaBalanceado ? (
          <>
            <CheckCircle2 className="h-4 w-4 text-emerald-600" aria-hidden="true" />
            <span className="text-sm font-medium text-emerald-700">Asiento balanceado</span>
          </>
        ) : (
          <>
            <AlertTriangle className="h-4 w-4 text-amber-600" aria-hidden="true" />
            <span className="text-sm font-medium text-amber-700">
              Diferencia: {formatMonto(diferencia.abs())}
            </span>
          </>
        )}
      </div>
    </div>
  );
}
