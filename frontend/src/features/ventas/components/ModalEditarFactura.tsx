import { useState } from "react";
import toast from "react-hot-toast";
import { Plus, Trash2, Loader2, X } from "lucide-react";
import { useEditarFactura } from "../hooks/useEditarFactura";

interface FacturaItem {
  id?: string;
  productoId?: number | null;
  descripcion: string;
  cantidad: number;
  precioUnitario: number;
}

const ALICUOTAS = [0, 10.5, 21, 27];

// Alícuota con la que se emitió la factura (la más cercana a IVA / Subtotal).
function alicuotaDe(factura: any): number {
  if (!(factura.subtotal > 0)) return 21;
  const real = (factura.iva / factura.subtotal) * 100;
  return ALICUOTAS.reduce((mejor, a) => (Math.abs(a - real) < Math.abs(mejor - real) ? a : mejor), 21);
}

export function ModalEditarFactura({ factura, onClose }: { factura: any; onClose: () => void }) {
  const [fecha, setFecha] = useState(factura.fecha ? factura.fecha.slice(0, 10) : "");
  const [tipoOperacion, setTipoOperacion] = useState(factura.tipoOperacion);
  const [tipoComprobante, setTipoComprobante] = useState(factura.tipoComprobante);
  const [cliente, setCliente] = useState(factura.cliente);
  const [condicionPago, setCondicionPago] = useState(factura.condicionPago || "ContadoCaja");
  const [porcentajeIva, setPorcentajeIva] = useState<number>(() => alicuotaDe(factura));
  const [puntoVentaCompra, setPuntoVentaCompra] = useState(factura.puntoVenta ? String(factura.puntoVenta).padStart(4, "0") : "");
  const [numeroCompra, setNumeroCompra] = useState(factura.numeroComprobante ? String(factura.numeroComprobante).padStart(8, "0") : "");

  const [items, setItems] = useState<FacturaItem[]>(
    factura.items?.length > 0
      ? factura.items.map((i: any) => ({ ...i, id: Math.random().toString(36).substr(2, 9) }))
      : [{ id: "nuevo", descripcion: "", cantidad: 1, precioUnitario: 0 }]
  );

  const editarFacturaMutation = useEditarFactura();
  const enviando = editarFacturaMutation.isPending;

  const handleChangeItem = (id: string, field: keyof FacturaItem, value: string | number) => {
    setItems(items.map(item => item.id === id ? { ...item, [field]: value } : item));
  };

  const handleAgregarItem = () => {
    setItems([...items, { id: Math.random().toString(36).substr(2, 9), descripcion: "", cantidad: 1, precioUnitario: 0 }]);
  };

  const handleEliminarItem = (id: string) => {
    if (items.length > 1) setItems(items.filter(item => item.id !== id));
  };

  const subtotalGeneral = items.reduce((acc, item) => acc + (item.cantidad * item.precioUnitario), 0);
  const ivaAplicado = tipoComprobante === "C" ? 0 : porcentajeIva;
  const iva = subtotalGeneral * (ivaAplicado / 100);
  const total = subtotalGeneral + iva;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    const itemsCargados = items.filter(i => i.descripcion.trim() !== "" || Number(i.precioUnitario) > 0);
    if (itemsCargados.length === 0) return void toast.error("La factura debe tener al menos un ítem");
    if (itemsCargados.some(i => i.descripcion.trim() === "" || !Number.isInteger(Number(i.cantidad)) || Number(i.cantidad) < 1 || !(Number(i.precioUnitario) >= 0)))
      return void toast.error("Revisá los ítems: cada uno necesita descripción, una cantidad entera de 1 o más y un precio válido");
    const subtotalItems = itemsCargados.reduce((acc, i) => acc + Number(i.cantidad) * Number(i.precioUnitario), 0);
    const ivaItems = Math.round(subtotalItems * ivaAplicado) / 100;

    const puntoVentaNum = Number(puntoVentaCompra);
    const numeroNum = Number(numeroCompra);
    if (tipoOperacion === "Compra" && !(Number.isInteger(puntoVentaNum) && puntoVentaNum >= 1 && puntoVentaNum <= 99999
        && Number.isInteger(numeroNum) && numeroNum >= 1 && numeroNum <= 99999999))
      return void toast.error("Cargá el número de la factura del proveedor: punto de venta (1 a 99999) y número (1 a 99999999)");
    // Una venta conserva su número; si cambia el tipo de comprobante, el sistema le asigna uno nuevo.
    const mismaNumeracion = tipoOperacion === factura.tipoOperacion && tipoComprobante === factura.tipoComprobante;

    const payload = {
      fecha: new Date(fecha).toISOString(),
      tipoOperacion,
      tipoComprobante,
      cliente,
      condicionPago,
      puntoVenta: tipoOperacion === "Compra" ? puntoVentaNum : (factura.puntoVenta ?? null),
      numeroComprobante: tipoOperacion === "Compra" ? numeroNum : (mismaNumeracion ? factura.numeroComprobante ?? null : null),
      subtotal: Number(subtotalItems),
      iva: Number(ivaItems),
      total: Number(subtotalItems + ivaItems),
      items: itemsCargados.map(item => ({
        productoId: item.productoId ?? 0,
        descripcion: item.descripcion,
        cantidad: Number(item.cantidad),
        precioUnitario: Number(item.precioUnitario),
        subtotal: Number(item.cantidad) * Number(item.precioUnitario)
      }))
    };

    // 1. Mandamos la mutación al backend de fondo
    editarFacturaMutation.mutate({
      id: Number(factura.id),
      payload: payload as any
    });

    // 2. 🔥 Cerramos el modal instantáneamente sin esperar el refetch
    onClose();
  };

  const formatARS = (num: number) => new Intl.NumberFormat("es-AR", { style: "currency", currency: "ARS" }).format(num);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4 overflow-y-auto">
      <div className="relative w-full max-w-4xl rounded-xl bg-white shadow-2xl my-8">

        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-slate-800">
            Editar factura {factura.tipoComprobante} {factura.numeroFactura ?? `#${factura.id}`}
          </h2>
          <button onClick={onClose} className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-600 transition">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 flex flex-col gap-6">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-5">
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">Operación</label>
              <select value={tipoOperacion} onChange={(e) => setTipoOperacion(e.target.value)} disabled={enviando} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:ring-1 focus:ring-blue-500 bg-white">
                <option value="Venta">Venta</option>
                <option value="Compra">Compra</option>
              </select>
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">Fecha</label>
              <input type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} disabled={enviando} required className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:ring-1 focus:ring-blue-500 bg-white" />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">Tipo</label>
              <select value={tipoComprobante} onChange={(e) => { setTipoComprobante(e.target.value); if (e.target.value !== "C" && porcentajeIva === 0) setPorcentajeIva(21); }} disabled={enviando} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:ring-1 focus:ring-blue-500 bg-white">
                <option value="A">Factura A</option>
                <option value="B">Factura B</option>
                <option value="C">Factura C</option>
              </select>
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">Cliente/Prov.</label>
              <input type="text" value={cliente} onChange={(e) => setCliente(e.target.value)} disabled={enviando} required className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:ring-1 focus:ring-blue-500 bg-white" />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">Pago</label>
              <select value={condicionPago} onChange={(e) => setCondicionPago(e.target.value)} disabled={enviando} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:ring-1 focus:ring-blue-500 bg-white">
                <option value="ContadoCaja">Contado (Caja)</option>
                <option value="TransferenciaBanco">Transferencia (Banco)</option>
                <option value="CuentaCorriente">Cta. Corriente</option>
              </select>
            </div>
          </div>

          {tipoOperacion === "Compra" ? (
            <div className="flex flex-wrap items-end gap-3">
              <div>
                <label htmlFor="editarPuntoVenta" className="mb-1 block text-sm font-medium text-slate-700">Punto de venta</label>
                <input id="editarPuntoVenta" inputMode="numeric" maxLength={5} value={puntoVentaCompra} disabled={enviando} required
                  onChange={(e) => setPuntoVentaCompra(e.target.value.replace(/\D/g, ""))}
                  className="w-24 rounded-md border border-slate-300 px-3 py-2 text-sm tabular-nums focus:border-blue-500 focus:ring-1 focus:ring-blue-500 bg-white" />
              </div>
              <div>
                <label htmlFor="editarNumero" className="mb-1 block text-sm font-medium text-slate-700">Número del proveedor</label>
                <input id="editarNumero" inputMode="numeric" maxLength={8} value={numeroCompra} disabled={enviando} required
                  onChange={(e) => setNumeroCompra(e.target.value.replace(/\D/g, ""))}
                  className="w-32 rounded-md border border-slate-300 px-3 py-2 text-sm tabular-nums focus:border-blue-500 focus:ring-1 focus:ring-blue-500 bg-white" />
              </div>
            </div>
          ) : (
            <p className="text-xs text-slate-500">
              {tipoOperacion === factura.tipoOperacion && tipoComprobante === factura.tipoComprobante && factura.numeroFactura
                ? `Conserva su número: ${factura.numeroFactura}.`
                : "Al cambiar el tipo de comprobante, el sistema le asigna el próximo número de ese tipo."}
            </p>
          )}

          <div className="overflow-hidden rounded-lg border border-slate-200">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-slate-500 text-left">
                <tr>
                  <th className="px-3 py-2 font-medium w-24">Cant.</th>
                  <th className="px-3 py-2 font-medium">Descripción</th>
                  <th className="px-3 py-2 font-medium w-32 text-right">P. Unitario</th>
                  <th className="px-3 py-2 font-medium w-32 text-right">Subtotal</th>
                  <th className="w-10 px-2 py-2"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {items.map((item) => (
                  <tr key={item.id} className="hover:bg-slate-50/50">
                    <td className="p-2"><input type="number" min="1" value={item.cantidad || ""} onChange={(e) => handleChangeItem(item.id!, "cantidad", parseFloat(e.target.value))} required className="w-full rounded border-slate-300 px-2 py-1 focus:ring-blue-500" /></td>
                    <td className="p-2"><input type="text" value={item.descripcion} onChange={(e) => handleChangeItem(item.id!, "descripcion", e.target.value)} required className="w-full rounded border-slate-300 px-2 py-1 focus:ring-blue-500" /></td>
                    <td className="p-2"><input type="number" min="0" step="0.01" value={item.precioUnitario || ""} onChange={(e) => handleChangeItem(item.id!, "precioUnitario", parseFloat(e.target.value))} required className="w-full rounded border-slate-300 px-2 py-1 text-right focus:ring-blue-500" /></td>
                    <td className="p-2 text-right font-medium text-slate-800">{formatARS((item.cantidad || 0) * (item.precioUnitario || 0))}</td>
                    <td className="p-2 text-center"><button type="button" onClick={() => handleEliminarItem(item.id!)} disabled={items.length === 1} className="text-slate-400 hover:text-red-500 disabled:opacity-50"><Trash2 className="h-4 w-4" /></button></td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="bg-slate-50 p-2 border-t border-slate-200">
              <button type="button" onClick={handleAgregarItem} className="text-sm font-medium text-blue-600 hover:text-blue-700 flex items-center gap-1 px-2 py-1 rounded hover:bg-blue-50"><Plus className="h-4 w-4" /> Agregar ítem</button>
            </div>
          </div>

          <div className="flex justify-between items-end border-t border-slate-100 pt-4">
            <div className="text-sm text-slate-500">
              <div>Subtotal: <span className="font-medium text-slate-700">{formatARS(subtotalGeneral)}</span></div>
              <div className="flex items-center gap-2">
                <label htmlFor="editarIva">IVA</label>
                <select id="editarIva" value={ivaAplicado} onChange={(e) => setPorcentajeIva(Number(e.target.value))}
                  disabled={enviando || tipoComprobante === "C"}
                  className="rounded-md border border-slate-300 bg-white px-2 py-0.5 text-sm disabled:bg-slate-100">
                  {ALICUOTAS.map(a => <option key={a} value={a}>{String(a).replace(".", ",")} %</option>)}
                </select>
                <span className="font-medium text-slate-700">{formatARS(iva)}</span>
              </div>
            </div>
            <div className="flex items-center gap-4">
              <div className="text-right">
                <span className="block text-xs text-slate-500 font-semibold">Total Modificado</span>
                <span className="text-xl font-bold text-slate-900">{formatARS(total)}</span>
              </div>
              <div className="h-10 border-l border-slate-200 mx-2"></div>
              <button type="button" onClick={onClose} disabled={enviando} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancelar</button>
              <button type="submit" disabled={enviando || items.length === 0} className="flex items-center gap-2 rounded-md bg-marca-600 px-6 py-2 text-sm font-medium text-white hover:bg-marca-700 disabled:opacity-50">
                {enviando ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                Guardar Cambios
              </button>
            </div>
          </div>
        </form>

      </div>
    </div>
  );
}