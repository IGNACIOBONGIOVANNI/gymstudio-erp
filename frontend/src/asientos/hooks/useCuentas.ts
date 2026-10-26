import { useQuery } from "@tanstack/react-query";
import type { CuentaContable } from "../types";
import { mensajeDeError } from "../../api/errores";

/** Plan de cuentas real (GET /api/cuentas), para el selector del asiento manual. */
export function useCuentas(habilitado = true) {
  return useQuery<CuentaContable[], Error>({
    queryKey: ["cuentas"],
    // Sólo con sesión iniciada: sin token la API responde 401.
    enabled: habilitado,
    queryFn: async () => {
      const res = await fetch("/api/cuentas?clienteId=1");
      if (!res.ok) throw new Error(await mensajeDeError(res, "No se pudo cargar el plan de cuentas"));
      return res.json();
    },
  });
}
