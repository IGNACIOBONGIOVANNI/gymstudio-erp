import { useState, useEffect } from "react";
import toast from "react-hot-toast";
import { Plus, Trash2, Loader2 } from "lucide-react";
import { useCrearFactura } from "../hooks/useCrearFactura";
import { useProductos } from "../../../hooks/useProductos";

interface FacturaItem {
  id: string;
  productoId: number;
  descripcion: string;
  cantidad: number;
  precioUnitario: number;
}

const generarId = () => Math.random().toString(36).substr(2, 9);

const ITEM_VACIO = (): FacturaItem => ({
  id: generarId(),
  productoId: 0,
  descripcion: "",
  cantidad: 1,
  precioUnitario: 0,
});

export function Facturacion() {
  const [tipoOperacion, setTipoOperacion] = useState<'Venta' | 'Compra'>('Venta');
  const [fecha, setFecha] = useState(new Date().toISOString().slice(0, 10));
  const [tipoComprobante, setTipoComprobante] = useState("A");
  
  // ESTADOS DE CLIENTES Y PROVEEDORES
  const [clienteId, setClienteId] = useState<number | "">("");
  const [clientes, setClientes] = useState<any[]>([]);
  const [proveedorId, setProveedorId] = useState<number | "">("");
  const [proveedores, setProveedores] = useState<any[]>([]);
  
  // ESTADOS NUEVOS
  const [condicionPago, setCondicionPago] = useState("ContadoCaja");
  const [porcentajeIva, setPorcentajeIva] = useState<number>(21);
  const [items, setItems] = useState<FacturaItem[]>([ITEM_VACIO()]);
  
  const crearFacturaMutation = useCrearFactura();
  const enviando = crearFacturaMutation.isPending;
  const { data: productos = [], isLoading: cargandoProductos } = useProductos();

  // 1. Carga inicial de datos
  useEffect(() => {
    fetch('/api/clientes')
      .then(res => res.json())
      .then(data => setClientes(data))
      .catch(err => console.error(err));
      
    fetch('/api/proveedores')
      .then(res => res.json())
      .then(data => setProveedores(data))
      .catch(err => console.error(err));
  }, []);

  // 2. MAGIA DE ARCA: AUTOMATIZACIÓN DE TIPO DE FACTURA
  useEffect(() => {
    if (tipoOperacion === 'Venta' && clienteId !== "") {
      const clienteObj = clientes.find(c => c.id === clienteId);
      const condicion = (clienteObj?.condicionIva || clienteObj?.condicionIVA || "").toLowerCase();
      
      if (condicion.includes("inscripto") || condicion.includes("monotributo")) {
        setTipoComprobante("A");
        setPorcentajeIva(21); // Vuelve a 21% por defecto para Factura A
      } else {
        setTipoComprobante("B");
        setPorcentajeIva(21); // Factura B discrimina internamente el IVA
      }
    } 
    else if (tipoOperacion === 'Compra' && proveedorId !== "") {
      const provObj = proveedores.find(p => p.id === proveedorId);
      const condicion = (provObj?.condicionIva || provObj?.condicionIVA || "").toLowerCase();
      
      if (condicion.includes("inscripto")) {
        setTipoComprobante("A");
        setPorcentajeIva(21);
      } else {
        setTipoComprobante("C");
        setPorcentajeIva(0); // La factura C no discrimina IVA
      }
    }
  }, [tipoOperacion, clienteId, proveedorId, clientes, proveedores]);

  const handleAgregarItem = () => {
    setItems([...items, ITEM_VACIO()]);
  };

  const handleEliminarItem = (id: string) => {
    if (items.length > 1) {
      setItems(items.filter((item) => item.id !== id));
    }
  };

  const handleChangeItem = (id: string, field: keyof FacturaItem, value: string | number) => {
    setItems(items.map((item) => {
      if (item.id === id) {
        if (field === "descripcion") {
          const prod = productos.find(p => p.nombre === value);
          return { 
            ...item, 
            descripcion: value as string, 
            productoId: prod ? prod.id : 0,
            precioUnitario: prod ? (tipoOperacion === 'Venta' ? prod.precioVenta : prod.precioUnitario) : item.precioUnitario 
          };
        }
        return { ...item, [field]: value };
      }
      return item;
    }));
  };

  const handleLimpiar = () => {
    setTipoOperacion("Venta");
    setFecha(new Date().toISOString().slice(0, 10));
    setTipoComprobante("A");
    setClienteId("");
    setProveedorId("");
    setCondicionPago("ContadoCaja"); 
    setPorcentajeIva(21);
    setItems([ITEM_VACIO()]);
  };

  // Cálculos
  const subtotalGeneral = items.reduce((acc, item) => acc + (item.cantidad * item.precioUnitario), 0);
  const iva = (tipoComprobante === "A" || tipoComprobante === "B") ? subtotalGeneral * (porcentajeIva / 100) : 0;
  const total = subtotalGeneral + iva;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    // Las filas que quedaron en blanco no se facturan.
    const itemsCargados = items.filter(i => i.descripcion.trim() !== "" || Number(i.precioUnitario) > 0);
    if (itemsCargados.length === 0)
      return void toast.error("Agregá al menos un ítem a la factura");
    const itemInvalido = itemsCargados.find(i =>
      i.descripcion.trim() === "" || !Number.isInteger(Number(i.cantidad)) || Number(i.cantidad) < 1 || !(Number(i.precioUnitario) >= 0));
    if (itemInvalido)
      return void toast.error("Revisá los ítems: cada uno necesita descripción, una cantidad entera de 1 o más y un precio válido");

    const tercero = tipoOperacion === 'Venta'
      ? clientes.find(c => c.id === clienteId)
      : proveedores.find(p => p.id === proveedorId);
    if (!tercero)
      return void toast.error(tipoOperacion === 'Venta' ? "Seleccioná el cliente" : "Seleccioná el proveedor");

    const subtotalItems = itemsCargados.reduce((acc, i) => acc + Number(i.cantidad) * Number(i.precioUnitario), 0);
    const ivaItems = (tipoComprobante === "A" || tipoComprobante === "B") ? subtotalItems * (porcentajeIva / 100) : 0;

    const payload = {
      fecha: new Date(fecha).toISOString(), 
      tipoOperacion,
      tipoComprobante,
      cliente: tercero.razonSocial,
      clienteId: tipoOperacion === 'Venta' ? (clienteId === "" ? null : Number(clienteId)) : null,
      proveedorId: tipoOperacion === 'Compra' ? (proveedorId === "" ? null : Number(proveedorId)) : null,
      condicionPago,
      subtotal: Number(subtotalItems),
      iva: Number(ivaItems),
      total: Number(subtotalItems + ivaItems),
      items: itemsCargados.map(item => ({
        productoId: item.productoId,
        descripcion: item.descripcion,
        cantidad: Number(item.cantidad),
        precioUnitario: Number(item.precioUnitario),
        subtotal: Number(item.cantidad) * Number(item.precioUnitario)
      }))
    };

    crearFacturaMutation.mutate(payload, {
      onSuccess: () => {
        handleLimpiar();
      }
    });
  };

  const currencyFormatter = new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "ARS",
  });

  return (
    <form onSubmit={handleSubmit} className="mx-auto flex max-w-5xl flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h2 className="text-lg font-semibold text-slate-900">Nueva Factura</h2>
        <p className="text-sm text-slate-500">
          Cargá los detalles del comprobante y los ítems a facturar.
        </p>
      </header>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-5">
        <div>
          <label htmlFor="tipoOperacion" className="mb-1 block text-sm font-medium text-slate-700">
            Tipo de Operación
          </label>
          <select
            id="tipoOperacion"
            disabled={enviando}
            value={tipoOperacion}
            onChange={(e) => {
              setTipoOperacion(e.target.value as 'Venta' | 'Compra');
              // Limpiamos los selectores al cambiar el tipo de operación
              setClienteId("");
              setProveedorId("");
            }}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:bg-slate-50"
          >
            <option value="Venta">Venta</option>
            <option value="Compra">Compra</option>
          </select>
        </div>

        <div>
          <label htmlFor="fecha" className="mb-1 block text-sm font-medium text-slate-700">
            Fecha
          </label>
          <input
            id="fecha"
            type="date"
            disabled={enviando}
            value={fecha}
            onChange={(e) => setFecha(e.target.value)}
            required
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:bg-slate-50"
          />
        </div>

        <div>
          <label htmlFor="tipoComprobante" className="mb-1 block text-sm font-medium text-slate-700">
            Tipo
          </label>
          <select
            id="tipoComprobante"
            disabled={true} // 🔥 BLOQUEADO: Lo maneja el sistema automáticamente
            value={tipoComprobante}
            onChange={(e) => setTipoComprobante(e.target.value)}
            className="w-full rounded-md border border-slate-300 bg-slate-100 px-3 py-2 text-sm font-semibold text-slate-600 cursor-not-allowed focus:outline-none"
          >
            <option value="A">Factura A</option>
            <option value="B">Factura B</option>
            <option value="C">Factura C</option>
          </select>
        </div>

        <div>
          {tipoOperacion === 'Venta' ? (
            <>
              <label htmlFor="cliente" className="mb-1 block text-sm font-medium text-slate-700">Cliente</label>
              <select id="cliente" disabled={enviando} value={clienteId} onChange={(e) => setClienteId(e.target.value === "" ? "" : Number(e.target.value))} required className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:bg-slate-50"> 
                <option value="" disabled>Seleccione un cliente...</option> 
                {clientes.map(c => ( <option key={c.id} value={c.id}>{c.razonSocial}</option> ))} 
              </select>
            </>
          ) : (
            <>
              <label htmlFor="proveedor" className="mb-1 block text-sm font-medium text-slate-700">Proveedor</label>
              <select id="proveedor" disabled={enviando} value={proveedorId} onChange={(e) => setProveedorId(e.target.value === "" ? "" : Number(e.target.value))} required className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:bg-slate-50"> 
                <option value="" disabled>Seleccione un proveedor...</option> 
                {proveedores.map(p => ( <option key={p.id} value={p.id}>{p.razonSocial}</option> ))} 
              </select>
            </>
          )}
        </div>

        <div>
          <label htmlFor="condicionPago" className="mb-1 block text-sm font-medium text-slate-700">
            Condición de Pago
          </label>
          <select
            id="condicionPago"
            disabled={enviando}
            value={condicionPago}
            onChange={(e) => setCondicionPago(e.target.value)}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:bg-slate-50"
          >
            <option value="ContadoCaja">Contado (Caja)</option>
            <option value="TransferenciaBanco">Transferencia (Banco)</option>
            <option value="CuentaCorriente">Cuenta Corriente</option>
          </select>
        </div>
      </div>

      <div className="overflow-x-auto rounded-lg border border-slate-200">
        <table className="w-full min-w-[640px] border-collapse bg-white">
          <thead>
            <tr className="border-b border-slate-200 bg-slate-50 text-left text-xs font-medium uppercase tracking-wide text-slate-500">
              <th className="px-3 py-2">Descripción</th>
              <th className="px-3 py-2 w-24">Cantidad</th>
              <th className="px-3 py-2 w-32 text-right">Precio Unit.</th>
              <th className="px-3 py-2 w-32 text-right">Subtotal</th>
              <th className="w-10 px-2 py-2" />
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50/50">
                <td className="p-2">
                  <select
                    value={item.descripcion}
                    onChange={(e) => handleChangeItem(item.id, "descripcion", e.target.value)}
                    disabled={enviando || cargandoProductos}
                    required
                    className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                  >
                    <option value="" disabled>Seleccione un producto...</option>
                    {productos.map(p => (
                      <option key={p.id} value={p.nombre}>{p.nombre}</option>
                    ))}
                  </select>
                </td>
                <td className="p-2">
                  <input
                    type="number"
                    min="1"
                    step="1"
                    value={item.cantidad || ""}
                    onChange={(e) => handleChangeItem(item.id, "cantidad", parseFloat(e.target.value))}
                    disabled={enviando}
                    required
                    className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                  />
                </td>
                <td className="p-2">
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={item.precioUnitario || ""}
                    onChange={(e) => handleChangeItem(item.id, "precioUnitario", parseFloat(e.target.value))}
                    disabled={enviando}
                    required
                    className="w-full text-right rounded-md border border-slate-300 px-2 py-1.5 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                  />
                </td>
                <td className="p-2 text-right text-sm font-medium text-slate-900">
                  {currencyFormatter.format((item.cantidad || 0) * (item.precioUnitario || 0))}
                </td>
                <td className="p-2 text-center">
                  <button
                    type="button"
                    onClick={() => handleEliminarItem(item.id)}
                    disabled={enviando || items.length === 1}
                    className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-red-600 disabled:opacity-50 transition-colors"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className="border-t border-slate-100 px-2 py-2">
          <button
            type="button"
            onClick={handleAgregarItem}
            disabled={enviando}
            className="inline-flex items-center gap-1.5 rounded-md px-2 py-1.5 text-sm font-medium text-slate-600 transition hover:bg-slate-50 disabled:opacity-40"
          >
            <Plus className="h-4 w-4" />
            Agregar ítem
          </button>
        </div>
      </div>

      <div className="flex justify-end">
        <div className="w-full sm:w-1/3 rounded-lg border border-slate-200 bg-slate-50 p-4 space-y-2">
          <div className="flex justify-between text-sm text-slate-600">
            <span>Subtotal</span>
            <span>{currencyFormatter.format(subtotalGeneral)}</span>
          </div>
          <div className="flex items-center justify-between text-sm text-slate-600 border-b border-slate-200 pb-2">
            <div className="flex items-center gap-2">
              <span>IVA</span>
              <select
                value={porcentajeIva}
                onChange={(e) => setPorcentajeIva(Number(e.target.value))}
                disabled={true} // 🔥 BLOQUEADO: El porcentaje lo determina la operación
                className="rounded-md border border-slate-300 bg-slate-100 px-2 py-0.5 text-sm font-semibold text-slate-600 cursor-not-allowed focus:outline-none"
              >
                <option value={0}>0% (Exento)</option>
                <option value={10.5}>10.5%</option>
                <option value={21}>21%</option>
                <option value={27}>27%</option>
              </select>
            </div>
            <span>{currencyFormatter.format(iva)}</span>
          </div>
          <div className="flex justify-between text-base font-semibold text-slate-900 pt-1">
            <span>Total a Pagar</span>
            <span>{currencyFormatter.format(total)}</span>
          </div>
        </div>
      </div>

      <div className="flex justify-end gap-3 border-t border-slate-200 pt-6">
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
          disabled={enviando || items.length === 0 || subtotalGeneral === 0}
          className="inline-flex items-center gap-2 rounded-md bg-slate-900 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-300"
        >
          {enviando && <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />}
          Emitir Factura
        </button>
      </div>
    </form>
  );
}