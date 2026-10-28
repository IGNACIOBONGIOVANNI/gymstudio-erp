import { useEffect, useState } from "react";

interface Props {
  tipo: "cliente" | "proveedor";
  value: string;
  onChange: (id: string) => void;
}

interface Opcion { id: number; razonSocial: string; cuit: string }

/** Lista de clientes o proveedores para vincular un cobro o un pago a su cuenta corriente. */
export function SelectorDeTercero({ tipo, value, onChange }: Props) {
  const [opciones, setOpciones] = useState<Opcion[]>([]);
  const [cargando, setCargando] = useState(true);
  const etiqueta = tipo === "cliente" ? "Cliente" : "Proveedor";

  useEffect(() => {
    let vigente = true;
    setCargando(true);
    fetch(tipo === "cliente" ? "/api/clientes" : "/api/proveedores")
      .then((r) => (r.ok ? r.json() : []))
      .then((d: Opcion[]) => vigente && setOpciones(d))
      .catch(() => vigente && setOpciones([]))
      .finally(() => vigente && setCargando(false));
    return () => { vigente = false; };
  }, [tipo]);

  // Un tercero dado de baja no figura en la lista pero sí puede ser el de un movimiento viejo.
  const faltante = value && !opciones.some((o) => String(o.id) === value);

  return (
    <div>
      <label className="block text-sm font-medium text-slate-700">{etiqueta}</label>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={cargando}
        className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:bg-slate-50"
      >
        <option value="">{cargando ? "Cargando..." : `Elegí un ${tipo}`}</option>
        {faltante && <option value={value}>{etiqueta} actual (dado de baja)</option>}
        {opciones.map((o) => (
          <option key={o.id} value={o.id}>{o.razonSocial}{o.cuit ? ` - ${o.cuit}` : ""}</option>
        ))}
      </select>
      <p className="mt-1 text-xs text-slate-500">
        {tipo === "cliente"
          ? "Se descuenta de lo que este cliente debe en su cuenta corriente."
          : "Se descuenta de lo que se le debe a este proveedor en su cuenta corriente."}
      </p>
      {!cargando && opciones.length === 0 && (
        <p className="mt-1 text-xs text-amber-700">Todavía no hay {tipo === "cliente" ? "clientes" : "proveedores"} cargados.</p>
      )}
    </div>
  );
}
