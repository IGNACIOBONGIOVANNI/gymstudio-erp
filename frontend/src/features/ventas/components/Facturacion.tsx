import { useState, useEffect } from "react";
import toast from "react-hot-toast";
import { Plus, Trash2, Loader2 } from "lucide-react";
import { useCrearFactura } from "../hooks/useCrearFactura";
import { useProductos } from "../../../hooks/useProductos";
import { obtenerConfiguracion } from "../../configuracion/configuracionStorage";
import { hoyLocal } from "../../../utils/formatters";
import { precioSugerido, preciosAlCambiarOperacion } from "../../../utils/precios";
import { VistaPreviaAsiento } from "./VistaPreviaAsiento";

interface FacturaItem {
  id: string;
  productoId: number;
  descripcion: string;
  cantidad: number;
  precioUnitario: number;
  agregarAlInventario: boolean;
  /** Verdadero si la persona escribió el precio: al cambiar entre Venta y Compra ese no se reemplaza. */
  precioManual?: boolean;
}

// "Martillo", "martillo " y "MARTILLO" son el mismo producto.
const normalizar = (texto: string) => texto.trim().toLocaleLowerCase("es");

const generarId = () => Math.random().toString(36).substr(2, 9);

// La alícuota sugerida sale de la Configuración (21 % si no se cambió).
const IVA_POR_DEFECTO = () => Number(obtenerConfiguracion().alicuotaIvaDefecto) || 21;
const PUNTO_VENTA_PROPIO = () => Math.max(1, parseInt(obtenerConfiguracion().puntoVenta, 10) || 1);

const ITEM_VACIO = (): FacturaItem => ({
  id: generarId(),
  productoId: 0,
  descripcion: "",
  cantidad: 1,
  precioUnitario: 0,
  agregarAlInventario: true,
});

export function Facturacion() {
  const [tipoOperacion, setTipoOperacion] = useState<'Venta' | 'Compra'>('Venta');
  const [fecha, setFecha] = useState(hoyLocal);
  const [tipoComprobante, setTipoComprobante] = useState("A");

  // ESTADOS DE CLIENTES Y PROVEEDORES
  const [clienteId, setClienteId] = useState<number | "">("");
  const [clientes, setClientes] = useState<any[]>([]);
  const [proveedorId, setProveedorId] = useState<number | "">("");
  const [proveedores, setProveedores] = useState<any[]>([]);

  // ESTADOS NUEVOS
  const [condicionPago, setCondicionPago] = useState("ContadoCaja");
  const [porcentajeIva, setPorcentajeIva] = useState<number>(IVA_POR_DEFECTO);
  // Número de la factura del proveedor (sólo compras; las ventas se numeran solas).
  const [puntoVentaCompra, setPuntoVentaCompra] = useState("");
  const [numeroCompra, setNumeroCompra] = useState("");
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
        cambiarTipoComprobante("A");
      } else {
        cambiarTipoComprobante("B");
      }
    }
    else if (tipoOperacion === 'Compra' && proveedorId !== "") {
      const provObj = proveedores.find(p => p.id === proveedorId);
      const condicion = (provObj?.condicionIva || provObj?.condicionIVA || "").toLowerCase();

      if (condicion.includes("inscripto")) {
        cambiarTipoComprobante("A");
      } else {
        cambiarTipoComprobante("C");
      }
    }
  }, [tipoOperacion, clienteId, proveedorId, clientes, proveedores]);

  // La factura C no discrimina IVA; al volver a A o B se repone la alícuota sugerida.
  function cambiarTipoComprobante(tipo: string) {
    setTipoComprobante(tipo);
    if (tipo === "C") setPorcentajeIva(0);
    else setPorcentajeIva(actual => (actual === 0 ? IVA_POR_DEFECTO() : actual));
  }

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
          const prod = productos.find(p => normalizar(p.nombre) === normalizar(String(value)));
          const sugerido = precioSugerido(prod, tipoOperacion);
          return {
            ...item,
            descripcion: value as string,
            productoId: prod ? prod.id : 0,
            // Si el producto no tiene precio cargado se respeta el que se escribió.
            precioUnitario: sugerido > 0 ? sugerido : item.precioUnitario,
            precioManual: sugerido > 0 ? false : item.precioManual
          };
        }
        // Un precio escrito a mano no se reemplaza si después se cambia entre Venta y Compra.
        if (field === "precioUnitario") return { ...item, precioUnitario: value as number, precioManual: true };
        return { ...item, [field]: value };
      }
      return item;
    }));
  };

  const handleLimpiar = () => {
    setTipoOperacion("Venta");
    setFecha(hoyLocal());
    setTipoComprobante("A");
    setClienteId("");
    setProveedorId("");
    setCondicionPago("ContadoCaja");
    setPorcentajeIva(IVA_POR_DEFECTO());
    setPuntoVentaCompra("");
    setNumeroCompra("");
    setItems([ITEM_VACIO()]);
  };

  // Cálculos (la factura C no discrimina IVA)
  const alicuotaAplicada = (tipoComprobante === "A" || tipoComprobante === "B") ? porcentajeIva : 0;
  const subtotalGeneral = items.reduce((acc, item) => acc + (item.cantidad * item.precioUnitario), 0);
  const iva = (tipoComprobante === "A" || tipoComprobante === "B") ? subtotalGeneral * (porcentajeIva / 100) : 0;
  const total = subtotalGeneral + iva;
  // Mismos importes (redondeados a centavos) que se envían al emitir, para la vista previa del asiento.
  const subtotalPrevia = Math.round(subtotalGeneral * 100) / 100;
  const ivaPrevia = (tipoComprobante === "A" || tipoComprobante === "B") ? Math.round(subtotalGeneral * porcentajeIva) / 100 : 0;
  const totalPrevia = Math.round((subtotalPrevia + ivaPrevia) * 100) / 100;

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

    // El número del proveedor es opcional: con los dos campos vacíos el sistema asigna un número interno.
    const puntoVentaNum = Number(puntoVentaCompra);
    const numeroNum = Number(numeroCompra);
    const sinNumero = puntoVentaCompra === "" && numeroCompra === "";
    if (tipoOperacion === 'Compra' && !sinNumero && !(Number.isInteger(puntoVentaNum) && puntoVentaNum >= 1 && puntoVentaNum <= 99999
        && Number.isInteger(numeroNum) && numeroNum >= 1 && numeroNum <= 99999999))
      return void toast.error("Completá el punto de venta (1 a 99999) y el número (1 a 99999999) de la factura del proveedor, o dejá los dos vacíos para que el sistema asigne un número interno");

    const subtotalItems = itemsCargados.reduce((acc, i) => acc + Number(i.cantidad) * Number(i.precioUnitario), 0);
    const ivaItems = (tipoComprobante === "A" || tipoComprobante === "B") ? Math.round(subtotalItems * porcentajeIva) / 100 : 0;

    const payload = {
      fecha: new Date(fecha).toISOString(),
      tipoOperacion,
      tipoComprobante,
      cliente: tercero.razonSocial,
      clienteId: tipoOperacion === 'Venta' ? (clienteId === "" ? null : Number(clienteId)) : null,
      proveedorId: tipoOperacion === 'Compra' ? (proveedorId === "" ? null : Number(proveedorId)) : null,
      condicionPago,
      puntoVenta: tipoOperacion === 'Compra' ? (sinNumero ? null : puntoVentaNum) : PUNTO_VENTA_PROPIO(),
      numeroComprobante: tipoOperacion === 'Compra' ? (sinNumero ? null : numeroNum) : null,
      subtotal: Number(subtotalItems),
      iva: Number(ivaItems),
      total: Number(subtotalItems + ivaItems),
      items: itemsCargados.map(item => ({
        productoId: item.productoId,
        agregarAlInventario: tipoOperacion === 'Compra' && !item.productoId && item.agregarAlInventario,
        descripcion: item.descripcion.trim(),
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
              const operacion = e.target.value as 'Venta' | 'Compra';
              setTipoOperacion(operacion);
              // Los productos ya elegidos pasan al precio de la nueva operación: de venta al vender, de compra al comprar.
              setItems((actuales) => preciosAlCambiarOperacion(actuales, productos, operacion));
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
          {/* Se sugiere según la condición de IVA del cliente o proveedor, pero se puede cambiar. */}
          <select
            id="tipoComprobante"
            disabled={enviando}
            value={tipoComprobante}
            onChange={(e) => cambiarTipoComprobante(e.target.value)}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm font-semibold focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:bg-slate-50"
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

      {tipoOperacion === 'Compra' ? (
        <fieldset className="flex flex-wrap items-end gap-3">
          <legend className="mb-1 text-sm font-medium text-slate-700">Número de la factura del proveedor (opcional)</legend>
          <div>
            <label htmlFor="puntoVentaCompra" className="mb-1 block text-xs text-slate-500">Punto de venta</label>
            <input
              id="puntoVentaCompra"
              inputMode="numeric"
              placeholder="Automático"
              maxLength={5}
              disabled={enviando}
              value={puntoVentaCompra}
              onChange={(e) => setPuntoVentaCompra(e.target.value.replace(/\D/g, ""))}
              onBlur={() => puntoVentaCompra && setPuntoVentaCompra(puntoVentaCompra.padStart(4, "0"))}
              className="w-24 rounded-md border border-slate-300 px-3 py-2 text-sm tabular-nums focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
          </div>
          <span className="pb-2 text-slate-400">-</span>
          <div>
            <label htmlFor="numeroCompra" className="mb-1 block text-xs text-slate-500">Número</label>
            <input
              id="numeroCompra"
              inputMode="numeric"
              placeholder="Automático"
              maxLength={8}
              disabled={enviando}
              value={numeroCompra}
              onChange={(e) => setNumeroCompra(e.target.value.replace(/\D/g, ""))}
              onBlur={() => numeroCompra && setNumeroCompra(numeroCompra.padStart(8, "0"))}
              className="w-32 rounded-md border border-slate-300 px-3 py-2 text-sm tabular-nums focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
          </div>
          <p className="max-w-sm pb-2 text-xs text-slate-500">
            Dejalos vacíos y el sistema asigna un número interno (punto de venta 9999). Si tenés la factura del proveedor, escribí su número tal como figura.
          </p>
        </fieldset>
      ) : (
        <p className="text-xs text-slate-500">
          El número de la factura se asigna solo, correlativo, en el punto de venta {String(PUNTO_VENTA_PROPIO()).padStart(4, "0")}.
        </p>
      )}

      <div className="overflow-x-auto rounded-lg border border-slate-200">
        <table className="w-full min-w-[820px] border-collapse bg-white">
          <thead>
            <tr className="border-b border-slate-200 bg-slate-50 text-left text-xs font-medium text-slate-500">
              <th className="px-3 py-2 w-20">Cant.</th>
              <th className="px-3 py-2">Descripción</th>
              <th className="px-3 py-2 w-32 text-right">P. Unitario</th>
              <th className="px-3 py-2 w-28 text-right">Subtotal</th>
              <th className="px-3 py-2 w-28 text-right">IVA {String(alicuotaAplicada).replace(".", ",")}%</th>
              <th className="px-3 py-2 w-28 text-right">Total</th>
              <th className="w-10 px-2 py-2" />
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id} className="border-b border-slate-100 last:border-0 hover:bg-slate-50/50">
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
                  {/* Texto libre: si coincide con un producto, mueve stock y toma su precio;
                      si no (un servicio, un gasto), se factura sólo como concepto. */}
                  <input
                    list="productos-facturables"
                    value={item.descripcion}
                    onChange={(e) => handleChangeItem(item.id, "descripcion", e.target.value)}
                    disabled={enviando}
                    placeholder={cargandoProductos ? "Cargando productos..." : "Producto o concepto"}
                    maxLength={200}
                    className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
                  />
                  {item.descripcion.trim() !== "" && (item.productoId ? (
                    <span className="mt-0.5 block text-[11px] text-slate-500">
                      Producto del inventario: mueve stock.{" "}
                      {(() => {
                        const sugerido = precioSugerido(productos.find(p => p.id === item.productoId), tipoOperacion);
                        const cual = tipoOperacion === 'Venta' ? 'venta' : 'compra';
                        if (sugerido <= 0)
                          return tipoOperacion === 'Venta'
                            ? "Todavía no tiene precio de venta: escribilo acá y queda guardado con la primera venta."
                            : "Todavía no tiene precio de compra: escribilo acá.";
                        return Number(item.precioUnitario) === sugerido
                          ? `Precio de ${cual} cargado en Productos: ${currencyFormatter.format(sugerido)}.`
                          : `Lo cambiaste a mano (el precio de ${cual} en Productos es ${currencyFormatter.format(sugerido)}).`;
                      })()}
                    </span>
                  ) : tipoOperacion === 'Compra' ? (
                    <label className="mt-0.5 flex items-center gap-1.5 text-[11px] text-slate-600">
                      <input
                        type="checkbox"
                        checked={item.agregarAlInventario}
                        onChange={(e) => setItems(items.map(i => i.id === item.id ? { ...i, agregarAlInventario: e.target.checked } : i))}
                        disabled={enviando}
                        className="h-3 w-3 accent-marca-600"
                      />
                      Producto nuevo: agregarlo al stock
                    </label>
                  ) : (
                    <span className="mt-0.5 block text-[11px] text-slate-500">Concepto libre: no mueve stock</span>
                  ))}
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
                <td className="p-2 text-right text-sm font-medium text-slate-900 tabular-nums">
                  {currencyFormatter.format((item.cantidad || 0) * (item.precioUnitario || 0))}
                </td>
                <td className="p-2 text-right text-sm text-slate-600 tabular-nums">
                  {currencyFormatter.format((item.cantidad || 0) * (item.precioUnitario || 0) * alicuotaAplicada / 100)}
                </td>
                <td className="p-2 text-right text-sm font-semibold text-slate-900 tabular-nums">
                  {currencyFormatter.format((item.cantidad || 0) * (item.precioUnitario || 0) * (1 + alicuotaAplicada / 100))}
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
        <datalist id="productos-facturables">
          {productos.map(p => (
            <option key={p.id} value={p.nombre} />
          ))}
        </datalist>

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
                disabled={enviando || tipoComprobante === "C"}
                title={tipoComprobante === "C" ? "La factura C no discrimina IVA" : undefined}
                className="rounded-md border border-slate-300 px-2 py-0.5 text-sm font-semibold focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500"
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

      <VistaPreviaAsiento tipoOperacion={tipoOperacion} condicionPago={condicionPago} subtotal={subtotalPrevia} iva={ivaPrevia} total={totalPrevia} />

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
          className="inline-flex items-center gap-2 rounded-md bg-marca-600 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-marca-700 disabled:cursor-not-allowed disabled:bg-slate-300"
        >
          {enviando && <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />}
          Emitir Factura
        </button>
      </div>
    </form>
  );
}