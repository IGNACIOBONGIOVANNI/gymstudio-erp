import toast from "react-hot-toast";

const pesos = new Intl.NumberFormat("es-AR", { style: "currency", currency: "ARS" });

/**
 * La Caja puede quedar en negativo (por ejemplo, si se cargan las compras de contado
 * antes que las ventas): la operación se registra igual y acá se avisa el saldo.
 */
export async function avisarSiCajaNegativa() {
  try {
    const res = await fetch("/api/caja?clienteId=1");
    if (!res.ok) return;
    const { saldoActual } = await res.json();
    if (typeof saldoActual === "number" && saldoActual < 0) {
      toast(`Atención: la Caja quedó en ${pesos.format(saldoActual)}. Se registró igual; se compensa con los próximos ingresos de efectivo.`,
        { icon: "⚠️", duration: 7000, id: "caja-negativa" });
    }
  } catch {
    // El aviso es informativo: si no se puede consultar, no se interrumpe nada.
  }
}
