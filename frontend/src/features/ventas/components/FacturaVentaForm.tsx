import { useState } from "react";
import { Loader2, Plus, X } from "lucide-react";
import { useCrearFacturaVenta } from "../hooks/useCrearFacturaVenta";
import { useProductos } from "../../../hooks/useProductos";

interface FacturaVentaFormProps {
  clienteId: string;
  onClose: () => void;
}

export function FacturaVentaForm({ clienteId, onClose }: FacturaVentaFormProps) {
  const [fecha, setFecha] = useState("");
  const [tipoFactura, setTipoFactura] = useState("A");
  const [numeroFactura, setNumeroFactura] = useState("");
  const [clienteNombre, setClienteNombre] = useState("");
  const [cuit, setCuit] = useState("");
  const [condicionPago, setCondicionPago] = useState(0); // 0=CC, 1=Caja, 2=Banco
  const [items, setItems] = useState([{ productoId: 1, cantidad: 1 }]);

  // Fetch productos desde la API
  const { data: productos = [], isLoading: cargandoProductos } = useProductos();

  // Cálculo de totales dinámico
  const neto = items.reduce((acc, item) => {
    const prod = productos.find(p => p.id === item.productoId);
    return acc + (prod ? prod.precioVenta * item.cantidad : 0);
  }, 0);
  const iva = tipoFactura === "A" ? neto * 0.21 : 0;
  const total = neto + iva;

  const { mutate: crear, isPending } = useCrearFacturaVenta();

  const handleLimpiar = () => {
    setFecha("");
    setTipoFactura("A");
    setNumeroFactura("");
    setClienteNombre("");
    setCuit("");
    setCondicionPago(0);
    setItems([{ productoId: 1, cantidad: 1 }]);
  };

  const handleAddItem = () => {
    setItems([...items, { productoId: 1, cantidad: 1 }]);
  };

  const handleRemoveItem = (index: number) => {
    setItems(items.filter((_, i) => i !== index));
  };

  const handleItemChange = (index: number, field: string, value: number) => {
    const newItems = [...items];
    newItems[index] = { ...newItems[index], [field]: value };
    setItems(newItems);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    crear(
      {
        clienteId: Number(clienteId),
        fecha: new Date(fecha).toISOString(),
        tipoFactura,
        numeroFactura,
        clienteNombre,
        cuit,
        condicionPago,
        items,
      },
      {
        onSuccess: () => {
          onClose();
        },
      }
    );
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4 sm:p-0">
      <div className="w-full max-w-2xl rounded-xl bg-white shadow-xl ring-1 ring-slate-900/5 max-h-[90vh] flex flex-col">
        <div className="bg-white flex items-center justify-between border-b border-slate-100 px-6 py-4 z-10 shrink-0">
          <h2 className="text-lg font-semibold text-slate-900">
            Nueva Factura de Venta
          </h2>
          <button
            onClick={onClose}
            className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-500 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="overflow-y-auto p-6 flex-1">
          <form id="factura-form" onSubmit={handleSubmit} className="space-y-6">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-slate-700">Fecha</label>
                <input
                  type="date"
                  required
                  value={fecha}
                  onChange={(e) => setFecha(e.target.value)}
                  className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700">Tipo Factura</label>
                <select
                  value={tipoFactura}
                  onChange={(e) => setTipoFactura(e.target.value)}
                  className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                >
                  <option value="A">A</option>
                  <option value="B">B</option>
                  <option value="C">C</option>
                  <option value="M">M</option>
                </select>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-slate-700">Número de Factura</label>
                <input
                  type="text"
                  required
                  placeholder="Ej: 0001-00001234"
                  value={numeroFactura}
                  onChange={(e) => setNumeroFactura(e.target.value)}
                  className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700">Condición de Pago</label>
                <select
                  value={condicionPago}
                  onChange={(e) => setCondicionPago(Number(e.target.value))}
                  className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                >
                  <option value={0}>Cuenta Corriente</option>
                  <option value={1}>Contado (Caja)</option>
                  <option value={2}>Transferencia (Banco)</option>
                </select>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-slate-700">Cliente</label>
                <input
                  type="text"
                  required
                  placeholder="Nombre o Razón Social"
                  value={clienteNombre}
                  onChange={(e) => setClienteNombre(e.target.value)}
                  className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700">CUIT</label>
                <input
                  type="text"
                  required
                  placeholder="Ej: 20-12345678-9"
                  value={cuit}
                  onChange={(e) => setCuit(e.target.value)}
                  className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                />
              </div>
            </div>

            {/* Sección de Ítems */}
            <div className="border-t border-slate-100 pt-4">
              <div className="flex items-center justify-between mb-2">
                <h3 className="text-sm font-medium text-slate-900">Ítems de Factura</h3>
                <button
                  type="button"
                  onClick={handleAddItem}
                  className="inline-flex items-center text-xs font-medium text-slate-600 hover:text-slate-900"
                >
                  <Plus className="h-3 w-3 mr-1" /> Agregar Ítem
                </button>
              </div>
              <div className="space-y-3">
                {items.map((item, index) => {
                  const prod = productos.find(p => p.id === item.productoId);
                  const subtotal = prod ? prod.precioVenta * item.cantidad : 0;
                  
                  return (
                    <div key={index} className="flex items-center gap-3 bg-slate-50 p-3 rounded-md border border-slate-100">
                      <div className="flex-1">
                        <select
                          value={item.productoId}
                          onChange={(e) => handleItemChange(index, "productoId", Number(e.target.value))}
                          className="block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                        >
                          {productos.map(p => (
                            <option key={p.id} value={p.id}>{p.nombre} (${p.precioVenta})</option>
                          ))}
                        </select>
                      </div>
                      <div className="w-24">
                        <label className="block text-xs text-slate-500 mb-1">CANTIDAD</label>
                        <input
                          type="number"
                          required
                          min="1"
                          value={item.cantidad}
                          onChange={(e) => handleItemChange(index, "cantidad", Number(e.target.value))}
                          className="block w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                        />
                      </div>
                      <div className="w-24">
                        <label className="block text-xs text-slate-500 mb-1">PRECIO UNIT.</label>
                        <input
                          type="number"
                          readOnly
                          value={prod ? prod.precioVenta : 0}
                          className="block w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-slate-100 text-slate-600 focus:outline-none cursor-not-allowed"
                        />
                      </div>
                      <div className="w-24 text-right">
                        <label className="block text-xs text-slate-500 mb-1">SUBTOTAL</label>
                        <div className="text-sm font-medium text-slate-700 mt-2">
                          ${subtotal.toFixed(2)}
                        </div>
                      </div>
                      {items.length > 1 && (
                        <button
                          type="button"
                          onClick={() => handleRemoveItem(index)}
                          className="p-1 text-slate-400 hover:text-red-500"
                        >
                          <X className="h-4 w-4" />
                        </button>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Totales */}
            <div className="grid grid-cols-3 gap-4 border-t border-slate-100 pt-4 bg-slate-50 p-4 rounded-md">
              <div>
                <span className="block text-xs font-medium text-slate-500">Neto</span>
                <span className="text-sm font-semibold text-slate-900">${neto.toFixed(2)}</span>
              </div>
              <div>
                <span className="block text-xs font-medium text-slate-500">IVA (21%)</span>
                <span className="text-sm font-semibold text-slate-900">${iva.toFixed(2)}</span>
              </div>
              <div>
                <span className="block text-xs font-medium text-slate-500">Total</span>
                <span className="text-lg font-bold text-slate-900">${total.toFixed(2)}</span>
              </div>
            </div>
          </form>
        </div>

        <div className="bg-white px-6 py-4 border-t border-slate-100 flex justify-end space-x-3 shrink-0">
          <button
            type="button"
            onClick={handleLimpiar}
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Limpiar
          </button>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Cancelar
          </button>
          <button
            type="submit"
            form="factura-form"
            disabled={isPending || items.length === 0 || cargandoProductos}
            className="inline-flex items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
          >
            {(isPending || cargandoProductos) && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            <Plus className="mr-2 h-4 w-4" />
            Guardar Factura
          </button>
        </div>
      </div>
    </div>
  );
}
