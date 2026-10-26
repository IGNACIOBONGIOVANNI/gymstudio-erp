import { useState } from "react";
import { Plus, Trash2, Loader2, X } from "lucide-react";
import { useEditarFactura } from "../hooks/useEditarFactura"; 

interface FacturaItem {
  id?: string;
  productoId?: number | null;
  descripcion: string;
  cantidad: number;
  precioUnitario: number;
}

export function ModalEditarFactura({ factura, onClose }: { factura: any; onClose: () => void }) {
  const [fecha, setFecha] = useState(factura.fecha ? factura.fecha.slice(0, 10) : "");
  const [tipoOperacion, setTipoOperacion] = useState(factura.tipoOperacion);
  const [tipoComprobante, setTipoComprobante] = useState(factura.tipoComprobante);
  const [cliente, setCliente] = useState(factura.cliente);
  const [condicionPago, setCondicionPago] = useState(factura.condicionPago || "ContadoCaja");
  
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
  const iva = (tipoComprobante === "A" || tipoComprobante === "B") ? subtotalGeneral * 0.21 : 0;
  const total = subtotalGeneral + iva;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const payload = {
      fecha: new Date(fecha).toISOString(),
      tipoOperacion,
      tipoComprobante,
      cliente,
      condicionPago,
      subtotal: Number(subtotalGeneral),
      iva: Number(iva),
      total: Number(total),
      items: items.map(item => ({
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
          <h2 className="text-lg font-semibold text-slate-800">Editar Factura #{factura.id}</h2>
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
              <select value={tipoComprobante} onChange={(e) => setTipoComprobante(e.target.value)} disabled={enviando} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-500 focus:ring-1 focus:ring-blue-500 bg-white">
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

          <div className="overflow-hidden rounded-lg border border-slate-200">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-slate-500 text-left">
                <tr>
                  <th className="px-3 py-2 font-medium">Descripción</th>
                  <th className="px-3 py-2 font-medium w-24">Cant.</th>
                  <th className="px-3 py-2 font-medium w-32 text-right">Precio</th>
                  <th className="px-3 py-2 font-medium w-32 text-right">Subtotal</th>
                  <th className="w-10 px-2 py-2"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {items.map((item) => (
                  <tr key={item.id} className="hover:bg-slate-50/50">
                    <td className="p-2"><input type="text" value={item.descripcion} onChange={(e) => handleChangeItem(item.id!, "descripcion", e.target.value)} required className="w-full rounded border-slate-300 px-2 py-1 focus:ring-blue-500" /></td>
                    <td className="p-2"><input type="number" min="1" value={item.cantidad || ""} onChange={(e) => handleChangeItem(item.id!, "cantidad", parseFloat(e.target.value))} required className="w-full rounded border-slate-300 px-2 py-1 focus:ring-blue-500" /></td>
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
              <div>IVA (21%): <span className="font-medium text-slate-700">{formatARS(iva)}</span></div>
            </div>
            <div className="flex items-center gap-4">
              <div className="text-right">
                <span className="block text-xs uppercase tracking-wide text-slate-500 font-semibold">Total Modificado</span>
                <span className="text-xl font-bold text-slate-900">{formatARS(total)}</span>
              </div>
              <div className="h-10 border-l border-slate-200 mx-2"></div>
              <button type="button" onClick={onClose} disabled={enviando} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancelar</button>
              <button type="submit" disabled={enviando || items.length === 0} className="flex items-center gap-2 rounded-md bg-blue-600 px-6 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50">
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